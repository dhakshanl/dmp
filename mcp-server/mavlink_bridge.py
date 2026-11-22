"""Background MAVLink connection to Mission Planner's mirrored telemetry link.

A single reader thread owns the pymavlink connection and is the only thing
that ever calls recv_match on it. Everything else (tool calls from the MCP
server) sends messages on the same socket and then polls the shared,
lock-protected state that the reader thread keeps up to date - this avoids
two threads racing on pymavlink's internal recv/parsing state.
"""
from __future__ import annotations

import copy
import queue
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Optional

from pymavlink import mavutil


class MAVLinkTimeout(Exception):
    """Raised when a command/param/mission exchange doesn't get a reply in time."""


@dataclass
class _PendingMissionExchange:
    items: list
    requests: "queue.Queue[int]" = field(default_factory=queue.Queue)
    ack: "queue.Queue[Any]" = field(default_factory=queue.Queue)


class MAVLinkBridge:
    def __init__(self, connection_string: str, source_system: int = 254, source_component: int = 190):
        self.connection_string = connection_string
        self.source_system = source_system
        self.source_component = source_component

        self.master: Optional[mavutil.mavfile] = None
        self._lock = threading.Lock()
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, daemon=True, name="mavlink-reader")

        self._command_acks: "queue.Queue[Any]" = queue.Queue()
        self._param_values: dict[str, Any] = {}
        self._param_events: dict[str, threading.Event] = {}
        self._mission_exchange: Optional[_PendingMissionExchange] = None
        self._mission_download: Optional[dict] = None

        self.state: dict[str, Any] = {
            "connected": False,
            "last_heartbeat": 0.0,
            "armed": False,
            "mode": None,
            "autopilot_type": None,
            "vehicle_type": None,
            "attitude": {},
            "vfr_hud": {},
            "position": {},
            "gps": {},
            "battery": {},
            "sys_status": {},
            "home": None,
        }

    # ------------------------------------------------------------------ #
    # lifecycle
    # ------------------------------------------------------------------ #
    def start(self) -> None:
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()

    def require_connection(self, timeout: float = 5.0) -> None:
        deadline = time.time() + timeout
        while time.time() < deadline:
            if self.snapshot()["connected"]:
                return
            time.sleep(0.1)
        raise MAVLinkTimeout(
            f"No MAVLink heartbeat via '{self.connection_string}'. Is Mission Planner's "
            "MAVLink output/mirror (Ctrl+F -> Output Mavlink) running and connected to a vehicle?"
        )

    # ------------------------------------------------------------------ #
    # reader thread
    # ------------------------------------------------------------------ #
    def _run(self) -> None:
        while not self._stop.is_set():
            try:
                if self.master is None:
                    self.master = mavutil.mavlink_connection(
                        self.connection_string,
                        source_system=self.source_system,
                        source_component=self.source_component,
                    )
                msg = self.master.recv_match(blocking=True, timeout=1)
                if msg is None:
                    if self.state["connected"] and time.time() - self.state["last_heartbeat"] > 5:
                        with self._lock:
                            self.state["connected"] = False
                    continue
                self._handle_message(msg)
            except Exception:
                self.master = None
                with self._lock:
                    self.state["connected"] = False
                time.sleep(1)

    def _handle_message(self, msg) -> None:
        t = msg.get_type()

        if t == "HEARTBEAT" and msg.get_srcComponent() == 1:
            with self._lock:
                self.state["connected"] = True
                self.state["last_heartbeat"] = time.time()
                self.state["armed"] = bool(
                    msg.base_mode & mavutil.mavlink.MAV_MODE_FLAG_SAFETY_ARMED
                )
                self.state["autopilot_type"] = msg.autopilot
                self.state["vehicle_type"] = msg.type
                try:
                    self.state["mode"] = mavutil.mode_string_v10(msg)
                except Exception:
                    self.state["mode"] = str(msg.custom_mode)
        elif t == "ATTITUDE":
            with self._lock:
                self.state["attitude"] = {
                    "roll_deg": _rad2deg(msg.roll),
                    "pitch_deg": _rad2deg(msg.pitch),
                    "yaw_deg": _rad2deg(msg.yaw) % 360,
                    "rollspeed": msg.rollspeed,
                    "pitchspeed": msg.pitchspeed,
                    "yawspeed": msg.yawspeed,
                }
        elif t == "VFR_HUD":
            with self._lock:
                self.state["vfr_hud"] = {
                    "airspeed": msg.airspeed,
                    "groundspeed": msg.groundspeed,
                    "heading_deg": msg.heading,
                    "throttle_pct": msg.throttle,
                    "alt_m": msg.alt,
                    "climb_m_s": msg.climb,
                }
        elif t == "GLOBAL_POSITION_INT":
            with self._lock:
                self.state["position"] = {
                    "lat": msg.lat / 1e7,
                    "lon": msg.lon / 1e7,
                    "alt_amsl_m": msg.alt / 1000.0,
                    "relative_alt_m": msg.relative_alt / 1000.0,
                    "vx": msg.vx / 100.0,
                    "vy": msg.vy / 100.0,
                    "vz": msg.vz / 100.0,
                }
        elif t == "GPS_RAW_INT":
            with self._lock:
                self.state["gps"] = {
                    "fix_type": msg.fix_type,
                    "satellites_visible": msg.satellites_visible,
                    "eph": msg.eph,
                    "epv": msg.epv,
                }
        elif t == "SYS_STATUS":
            with self._lock:
                self.state["sys_status"] = {
                    "voltage_v": msg.voltage_battery / 1000.0 if msg.voltage_battery != -1 else None,
                    "current_a": msg.current_battery / 100.0 if msg.current_battery != -1 else None,
                    "battery_remaining_pct": msg.battery_remaining if msg.battery_remaining != -1 else None,
                }
        elif t == "BATTERY_STATUS":
            with self._lock:
                self.state["battery"] = {
                    "id": msg.id,
                    "voltages_mv": [v for v in msg.voltages if v != 65535],
                    "current_a": msg.current_battery / 100.0 if msg.current_battery != -1 else None,
                    "remaining_pct": msg.battery_remaining if msg.battery_remaining != -1 else None,
                    "temperature_c": msg.temperature / 100.0 if msg.temperature != 32767 else None,
                }
        elif t == "HOME_POSITION":
            with self._lock:
                self.state["home"] = {
                    "lat": msg.latitude / 1e7,
                    "lon": msg.longitude / 1e7,
                    "alt_amsl_m": msg.altitude / 1000.0,
                }
        elif t == "COMMAND_ACK":
            self._command_acks.put(msg)
        elif t == "PARAM_VALUE":
            name = msg.param_id.rstrip("\x00")
            self._param_values[name] = msg.param_value
            ev = self._param_events.get(name)
            if ev is not None:
                ev.set()
        elif t in ("MISSION_REQUEST", "MISSION_REQUEST_INT"):
            ex = self._mission_exchange
            if ex is not None:
                ex.requests.put(msg.seq)
        elif t == "MISSION_ACK":
            ex = self._mission_exchange
            if ex is not None:
                ex.ack.put(msg)
        elif t == "MISSION_COUNT":
            dl = self._mission_download
            if dl is not None:
                dl["count"] = msg.count
                dl["count_event"].set()
        elif t in ("MISSION_ITEM_INT", "MISSION_ITEM"):
            dl = self._mission_download
            if dl is not None:
                dl["items"][msg.seq] = msg
                dl["item_event"].set()

    def snapshot(self) -> dict[str, Any]:
        with self._lock:
            return copy.deepcopy(self.state)

    # ------------------------------------------------------------------ #
    # commands
    # ------------------------------------------------------------------ #
    def _send_command_long(self, command: int, *params, timeout: float = 5.0):
        p = list(params) + [0] * (7 - len(params))
        self.master.mav.command_long_send(
            self.master.target_system,
            self.master.target_component,
            command,
            0,
            *p,
        )
        deadline = time.time() + timeout
        while time.time() < deadline:
            try:
                msg = self._command_acks.get(timeout=max(0.05, deadline - time.time()))
            except queue.Empty:
                break
            if msg.command == command:
                return msg
        raise MAVLinkTimeout(f"No COMMAND_ACK for command {command} within {timeout}s")

    def arm(self, force: bool = False) -> None:
        ack = self._send_command_long(
            mavutil.mavlink.MAV_CMD_COMPONENT_ARM_DISARM, 1, 21196 if force else 0
        )
        _raise_if_rejected(ack)

    def disarm(self, force: bool = False) -> None:
        ack = self._send_command_long(
            mavutil.mavlink.MAV_CMD_COMPONENT_ARM_DISARM, 0, 21196 if force else 0
        )
        _raise_if_rejected(ack)

    def set_mode(self, mode_name: str) -> None:
        mapping = self.master.mode_mapping()
        if not mapping:
            raise MAVLinkTimeout("No mode mapping available yet - waiting on a heartbeat from the vehicle")
        key = mode_name.upper()
        if key not in mapping:
            raise ValueError(f"Unknown mode '{mode_name}'. Available modes: {sorted(mapping.keys())}")
        self.master.mav.set_mode_send(
            self.master.target_system,
            mavutil.mavlink.MAV_MODE_FLAG_CUSTOM_MODE_ENABLED,
            mapping[key],
        )

    def takeoff(self, altitude_m: float) -> None:
        ack = self._send_command_long(mavutil.mavlink.MAV_CMD_NAV_TAKEOFF, 0, 0, 0, 0, 0, 0, altitude_m)
        _raise_if_rejected(ack)

    def land(self) -> None:
        self.set_mode("LAND")

    def return_to_launch(self) -> None:
        self.set_mode("RTL")

    def goto(self, lat: float, lon: float, alt_relative_m: float) -> None:
        type_mask = 0b0000111111111000  # position only
        self.master.mav.set_position_target_global_int_send(
            0,
            self.master.target_system,
            self.master.target_component,
            mavutil.mavlink.MAV_FRAME_GLOBAL_RELATIVE_ALT_INT,
            type_mask,
            int(lat * 1e7),
            int(lon * 1e7),
            alt_relative_m,
            0, 0, 0,
            0, 0, 0,
            0, 0,
        )

    # ------------------------------------------------------------------ #
    # parameters
    # ------------------------------------------------------------------ #
    def get_param(self, name: str, timeout: float = 5.0) -> float:
        ev = threading.Event()
        self._param_events[name] = ev
        self.master.mav.param_request_read_send(
            self.master.target_system, self.master.target_component, name.encode(), -1
        )
        if not ev.wait(timeout):
            raise MAVLinkTimeout(f"No PARAM_VALUE for '{name}' within {timeout}s")
        return self._param_values[name]

    def set_param(self, name: str, value: float, timeout: float = 5.0) -> float:
        ev = threading.Event()
        self._param_events[name] = ev
        self.master.mav.param_set_send(
            self.master.target_system,
            self.master.target_component,
            name.encode(),
            float(value),
            mavutil.mavlink.MAV_PARAM_TYPE_REAL32,
        )
        if not ev.wait(timeout):
            raise MAVLinkTimeout(f"No PARAM_VALUE confirmation for '{name}' within {timeout}s")
        return self._param_values[name]

    # ------------------------------------------------------------------ #
    # missions
    # ------------------------------------------------------------------ #
    def upload_mission(self, waypoints: list[dict], timeout: float = 20.0) -> None:
        """waypoints: list of {lat, lon, alt, command?, frame?} dicts, waypoint 0 = home/first item."""
        ex = _PendingMissionExchange(items=waypoints)
        self._mission_exchange = ex
        try:
            self.master.mav.mission_clear_all_send(self.master.target_system, self.master.target_component)
            self.master.mav.mission_count_send(
                self.master.target_system, self.master.target_component, len(waypoints)
            )
            deadline = time.time() + timeout
            sent = 0
            while sent < len(waypoints) and time.time() < deadline:
                try:
                    seq = ex.requests.get(timeout=max(0.1, deadline - time.time()))
                except queue.Empty:
                    break
                wp = waypoints[seq]
                self.master.mav.mission_item_int_send(
                    self.master.target_system,
                    self.master.target_component,
                    seq,
                    wp.get("frame", mavutil.mavlink.MAV_FRAME_GLOBAL_RELATIVE_ALT_INT),
                    wp.get("command", mavutil.mavlink.MAV_CMD_NAV_WAYPOINT),
                    0,
                    1,
                    wp.get("param1", 0),
                    wp.get("param2", 0),
                    wp.get("param3", 0),
                    wp.get("param4", 0),
                    int(wp["lat"] * 1e7),
                    int(wp["lon"] * 1e7),
                    wp["alt"],
                )
                sent += 1
            try:
                ack = ex.ack.get(timeout=max(0.1, deadline - time.time()))
            except queue.Empty:
                raise MAVLinkTimeout("No MISSION_ACK received after uploading waypoints")
            if ack.type != mavutil.mavlink.MAV_MISSION_ACCEPTED:
                raise RuntimeError(f"Mission upload rejected: MAV_MISSION result {ack.type}")
        finally:
            self._mission_exchange = None

    def download_mission(self, timeout: float = 20.0) -> list[dict]:
        dl = {
            "count": None,
            "count_event": threading.Event(),
            "items": {},
            "item_event": threading.Event(),
        }
        self._mission_download = dl
        try:
            self.master.mav.mission_request_list_send(
                self.master.target_system, self.master.target_component
            )
            if not dl["count_event"].wait(timeout):
                raise MAVLinkTimeout("No MISSION_COUNT received")
            count = dl["count"]
            deadline = time.time() + timeout
            for seq in range(count):
                self.master.mav.mission_request_int_send(
                    self.master.target_system, self.master.target_component, seq
                )
                dl["item_event"].clear()
                while seq not in dl["items"] and time.time() < deadline:
                    dl["item_event"].wait(max(0.1, deadline - time.time()))
            if len(dl["items"]) < count:
                raise MAVLinkTimeout(f"Only received {len(dl['items'])}/{count} mission items")
            self.master.mav.mission_ack_send(
                self.master.target_system,
                self.master.target_component,
                mavutil.mavlink.MAV_MISSION_ACCEPTED,
            )
            result = []
            for _, item in sorted(dl["items"].items()):
                is_int_frame = item.get_type() == "MISSION_ITEM_INT"
                result.append(
                    {
                        "seq": item.seq,
                        "command": item.command,
                        "frame": item.frame,
                        "lat": item.x / 1e7 if is_int_frame else item.x,
                        "lon": item.y / 1e7 if is_int_frame else item.y,
                        "alt": item.z,
                        "param1": item.param1,
                        "param2": item.param2,
                        "param3": item.param3,
                        "param4": item.param4,
                    }
                )
            return result
        finally:
            self._mission_download = None


def _raise_if_rejected(ack) -> None:
    if ack.result != mavutil.mavlink.MAV_RESULT_ACCEPTED:
        raise RuntimeError(f"Command rejected: MAV_RESULT {ack.result}")


def _rad2deg(rad: float) -> float:
    return rad * 57.29577951308232
