# mission-planner MCP server

Exposes a live MAVLink link to an MCP client (Claude Desktop, Claude Code,
etc.) as tools: telemetry, GPS, battery, arm/disarm, mode changes, takeoff,
land, RTL, goto, parameters, and mission upload/download.

It does **not** patch Mission Planner itself. It connects as an independent
MAVLink endpoint to a port that Mission Planner mirrors its existing vehicle
link to, using Mission Planner's built-in relay feature.

## 1. Enable the MAVLink mirror in Mission Planner

1. Connect Mission Planner to your vehicle (or SITL) as usual.
2. Open the hidden dev screen: focus Mission Planner and press `Ctrl+F`.
3. Click **"Output Mavlink"**. This opens the Serial Output Pass dialog
   ([Controls/SerialOutputPass.cs](../Controls/SerialOutputPass.cs)).
4. Choose **"UDP Host - 14550"** and accept the default port (14550).
5. Check **"write"** (`chk_write`) so commands sent back on this port are
   relayed to the vehicle, not just telemetry out. Without this, arm/mode/
   mission tools will silently have no effect.
6. Click **Connect**.

Mission Planner is now listening on UDP 14550 and will both send telemetry
to, and accept MAVLink from, anything that connects there.

## 2. Install dependencies

```
cd mcp-server
python -m pip install -r requirements.txt
```

## 3. Run it standalone (sanity check)

```
python server.py
```

It should sit waiting for an MCP client on stdio. Ctrl+C to stop. If you want
to confirm the MAVLink side works before wiring up an LLM client, run a quick
check with pymavlink separately, or just watch Mission Planner's HUD for a
few seconds after connecting - the server itself doesn't print telemetry.

## 4. Point an MCP client at it

Claude Desktop / Claude Code config (`claude_desktop_config.json` or your
project's `.mcp.json`):

```json
{
  "mcpServers": {
    "mission-planner": {
      "command": "python",
      "args": ["C:\\Users\\lnara\\Desktop\\testmp\\dmp\\mcp-server\\server.py"]
    }
  }
}
```

## Configuration

Environment variables, both optional:

- `MP_MAVLINK_CONN` - pymavlink connection string. Default
  `udpout:127.0.0.1:14550`, matching the "UDP Host - 14550" preset above. If
  you instead pick "UDP Client" in Mission Planner and give it a host/port to
  connect out to, set this to `udpin:0.0.0.0:<port>` here and have Mission
  Planner connect to you.
- `MP_MAVLINK_CONNECT_TIMEOUT` - seconds to wait for a heartbeat before a
  command tool gives up and reports "not connected". Default `5`.

## Safety notes

This server can arm the vehicle and command takeoff. There is no built-in
geofence or confirmation step beyond what the vehicle/ArduPilot itself
enforces - normal ArduPilot arming checks and failsafes still apply. Treat
tool calls from an LLM the way you'd treat a script: review mission uploads
and mode changes before running them near a real vehicle, and prefer testing
against SITL first.

## Tools

| Tool | Description |
| --- | --- |
| `get_telemetry` | Full snapshot: mode, armed, attitude, speed/alt, GPS, battery, position |
| `get_position` | lat/lon/alt/velocity |
| `get_battery` | Voltage, current, remaining %, per-cell voltages |
| `get_gps` | Fix type, satellite count |
| `get_flight_status` | connected / armed / mode |
| `arm(force=False)` | Arm motors |
| `disarm(force=False)` | Disarm motors |
| `set_mode(mode)` | Change flight mode by name |
| `takeoff(altitude_m)` | Command takeoff (must be armed + GUIDED) |
| `land()` | Switch to LAND |
| `return_to_launch()` | Switch to RTL |
| `goto(lat, lon, alt_relative_m)` | Fly to a position (must be GUIDED) |
| `get_param(name)` / `set_param(name, value)` | Read/write ArduPilot params |
| `upload_mission(waypoints)` / `download_mission()` | Replace/read the stored mission |
