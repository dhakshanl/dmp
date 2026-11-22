"""MCP server that exposes a live Mission Planner MAVLink link as tools.

Mission Planner must be mirroring its MAVLink stream to a local UDP/TCP port
(Ctrl+F -> "Output Mavlink" -> "UDP Host - 14550", write enabled). See
README.md for the one-time setup. This process then connects to that mirror
as an independent MAVLink endpoint and exposes read/command tools over MCP.
"""
from __future__ import annotations

import os
from typing import Any

from mcp.server.fastmcp import FastMCP

from mavlink_bridge import MAVLinkBridge

CONNECTION_STRING = os.environ.get("MP_MAVLINK_CONN", "udpout:127.0.0.1:14550")
CONNECT_TIMEOUT = float(os.environ.get("MP_MAVLINK_CONNECT_TIMEOUT", "5"))

mcp = FastMCP("mission-planner")
bridge = MAVLinkBridge(CONNECTION_STRING)
bridge.start()


def _ready() -> None:
    bridge.require_connection(timeout=CONNECT_TIMEOUT)


@mcp.tool()
def get_telemetry() -> dict[str, Any]:
    """Get the latest full telemetry snapshot: mode, armed state, attitude,
    speed/altitude, GPS fix, battery and position."""
    return bridge.snapshot()


@mcp.tool()
def get_position() -> dict[str, Any]:
    """Get the vehicle's current lat/lon/altitude and velocity."""
    return bridge.snapshot()["position"]


@mcp.tool()
def get_battery() -> dict[str, Any]:
    """Get battery voltage, current, remaining percentage and per-cell voltages if available."""
    snap = bridge.snapshot()
    return {"sys_status": snap["sys_status"], "battery_status": snap["battery"]}


@mcp.tool()
def get_gps() -> dict[str, Any]:
    """Get GPS fix type and satellite count."""
    return bridge.snapshot()["gps"]


@mcp.tool()
def get_flight_status() -> dict[str, Any]:
    """Get whether the vehicle is connected, armed, and its current flight mode."""
    snap = bridge.snapshot()
    return {
        "connected": snap["connected"],
        "armed": snap["armed"],
        "mode": snap["mode"],
    }


@mcp.tool()
def arm(force: bool = False) -> str:
    """Arm the vehicle's motors. Set force=True to bypass arming safety checks
    (use with caution)."""
    _ready()
    bridge.arm(force=force)
    return "armed"


@mcp.tool()
def disarm(force: bool = False) -> str:
    """Disarm the vehicle's motors."""
    _ready()
    bridge.disarm(force=force)
    return "disarmed"


@mcp.tool()
def set_mode(mode: str) -> str:
    """Set the vehicle's flight mode by name (e.g. GUIDED, LOITER, RTL, LAND,
    STABILIZE, AUTO - exact names depend on vehicle type)."""
    _ready()
    bridge.set_mode(mode)
    return f"mode set to {mode}"


@mcp.tool()
def takeoff(altitude_m: float) -> str:
    """Command a takeoff to the given relative altitude in meters. The vehicle
    must already be armed and in GUIDED mode."""
    _ready()
    bridge.takeoff(altitude_m)
    return f"takeoff to {altitude_m}m commanded"


@mcp.tool()
def land() -> str:
    """Switch the vehicle to LAND mode."""
    _ready()
    bridge.land()
    return "landing"


@mcp.tool()
def return_to_launch() -> str:
    """Switch the vehicle to RTL (return-to-launch) mode."""
    _ready()
    bridge.return_to_launch()
    return "returning to launch"


@mcp.tool()
def goto(lat: float, lon: float, alt_relative_m: float) -> str:
    """Fly to a lat/lon/relative-altitude position. Vehicle must be in GUIDED mode."""
    _ready()
    bridge.goto(lat, lon, alt_relative_m)
    return f"heading to {lat},{lon} @ {alt_relative_m}m"


@mcp.tool()
def get_param(name: str) -> float:
    """Read a single ArduPilot parameter value by name."""
    _ready()
    return bridge.get_param(name)


@mcp.tool()
def set_param(name: str, value: float) -> float:
    """Set a single ArduPilot parameter value. Returns the confirmed value."""
    _ready()
    return bridge.set_param(name, value)


@mcp.tool()
def upload_mission(waypoints: list[dict]) -> str:
    """Upload and replace the vehicle's mission. Each waypoint is a dict with
    at least lat, lon, alt (relative meters); optional command (MAV_CMD id,
    defaults to NAV_WAYPOINT) and param1-4."""
    _ready()
    bridge.upload_mission(waypoints)
    return f"uploaded {len(waypoints)} waypoint(s)"


@mcp.tool()
def download_mission() -> list[dict]:
    """Download the vehicle's currently stored mission."""
    _ready()
    return bridge.download_mission()


if __name__ == "__main__":
    mcp.run()
