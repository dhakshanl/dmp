#!/usr/bin/env python3
"""
Companion-computer sender for the NavXDrone Mission page.

Runs on the machine the ZED 2 is plugged into (needs an NVIDIA GPU - the ZED SDK
computes depth on CUDA). Grabs the left RGB view and the depth visualisation,
encodes both to H.264, and sends them to the ground station as two RTP/UDP
streams. The Mission page's two video tiles pick them up with their default
pipelines; see GCSViews\\Mission.cs (DefaultRgbPipeline / DefaultDepthPipeline).

    RGB   left eye  -> udp/5600
    DEPTH view      -> udp/5601

Frames are pushed into two `gst-launch-1.0` child processes over stdin rather
than through PyGObject or an OpenCV-with-GStreamer build. Both of those are
painful to get working on Windows; gst-launch ships with the same GStreamer MSI
the ground station already needs, so this adds no new install on either side.

Requirements on this machine:
    - ZED SDK + the Python API   (python -m pip install pyzed  after the SDK installer)
    - GStreamer 1.x MSVC 64-bit, "Complete" install (for x264enc / nvh264enc)
    - numpy

Usage:
    python zed_stream.py --host 192.168.1.50
    python zed_stream.py --host 100.x.y.z --encoder x264enc --resolution HD1080
"""

import argparse
import os
import shutil
import subprocess
import sys

try:
    import pyzed.sl as sl
except ImportError:
    sys.exit("pyzed not found. Install the ZED SDK, then its Python API "
             "(see the SDK's get_python_api.py).")


# Where the Windows MSI puts gst-launch when it is not on PATH.
GST_FALLBACK_DIRS = [
    r"C:\gstreamer\1.0\msvc_x86_64\bin",
    r"C:\gstreamer\1.0\mingw_x86_64\bin",
    "/usr/bin",
]


def find_gst_launch(explicit):
    if explicit:
        return explicit

    found = shutil.which("gst-launch-1.0")
    if found:
        return found

    for d in GST_FALLBACK_DIRS:
        for name in ("gst-launch-1.0.exe", "gst-launch-1.0"):
            candidate = os.path.join(d, name)
            if os.path.isfile(candidate):
                return candidate

    sys.exit("gst-launch-1.0 not found. Install GStreamer (Complete), or pass --gst "
             "with the full path to gst-launch-1.0.exe.")


def encoder_chain(encoder, bitrate, fps):
    """
    nvh264enc runs on the GPU and is the reason to use a CUDA machine for this;
    x264enc is the portable fallback. Both are configured for low latency rather
    than quality - a stale frame is worse than a soft one on a live feed.
    """
    if encoder == "nvh264enc":
        return ("nvh264enc preset=low-latency-hq zerolatency=true "
                "rc-mode=cbr bitrate={0}".format(bitrate))
    return ("x264enc tune=zerolatency speed-preset=ultrafast "
            "bitrate={0} key-int-max={1}".format(bitrate, fps * 2))


def spawn_sender(gst, width, height, fps, host, port, encoder, bitrate):
    """
    One gst-launch reading raw BGRA on stdin and emitting RTP/H.264 on a UDP port.
    BGRA is what the ZED SDK hands back for both VIEW.LEFT and VIEW.DEPTH, so no
    conversion happens in Python.
    """
    pipeline = (
        "fdsrc fd=0 ! "
        "rawvideoparse width={w} height={h} format=bgra framerate={fps}/1 ! "
        "videoconvert ! video/x-raw,format=I420 ! "
        "{enc} ! "
        "h264parse config-interval=-1 ! "
        "rtph264pay config-interval=1 pt=96 ! "
        "udpsink host={host} port={port} sync=false async=false"
    ).format(w=width, h=height, fps=fps, host=host, port=port,
             enc=encoder_chain(encoder, bitrate, fps))

    print("  udp/{0} <- {1}".format(port, encoder))

    return subprocess.Popen(
        [gst, "-q"] + pipeline.split(),
        stdin=subprocess.PIPE,
    )


def main():
    ap = argparse.ArgumentParser(description="Stream ZED 2 RGB + depth to a NavXDrone ground station.")
    ap.add_argument("--host", required=True,
                    help="Ground-station IP (the machine running NavXDrone).")
    ap.add_argument("--rgb-port", type=int, default=5600)
    ap.add_argument("--depth-port", type=int, default=5601)
    ap.add_argument("--resolution", default="HD720",
                    choices=["VGA", "HD720", "HD1080", "HD2K"])
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--bitrate", type=int, default=4000, help="kbit/s per stream.")
    ap.add_argument("--encoder", default="nvh264enc", choices=["nvh264enc", "x264enc"])
    ap.add_argument("--depth-mode", default="PERFORMANCE",
                    choices=["PERFORMANCE", "QUALITY", "ULTRA", "NEURAL"],
                    help="NEURAL looks best but costs the most GPU; PERFORMANCE keeps "
                         "headroom for two H.264 encodes on the same card.")
    ap.add_argument("--gst", default=None, help="Path to gst-launch-1.0 if not on PATH.")
    args = ap.parse_args()

    gst = find_gst_launch(args.gst)

    init = sl.InitParameters()
    init.camera_resolution = getattr(sl.RESOLUTION, args.resolution)
    init.camera_fps = args.fps
    init.depth_mode = getattr(sl.DEPTH_MODE, args.depth_mode)
    init.coordinate_units = sl.UNIT.METER

    zed = sl.Camera()
    status = zed.open(init)
    if status != sl.ERROR_CODE.SUCCESS:
        sys.exit("Could not open ZED: {0}".format(status))

    runtime = sl.RuntimeParameters()
    mat_left = sl.Mat()
    mat_depth = sl.Mat()

    rgb_proc = None
    depth_proc = None

    print("Streaming to {0} (Ctrl+C to stop)".format(args.host))

    try:
        while True:
            if zed.grab(runtime) != sl.ERROR_CODE.SUCCESS:
                continue

            zed.retrieve_image(mat_left, sl.VIEW.LEFT)
            zed.retrieve_image(mat_depth, sl.VIEW.DEPTH)

            left = mat_left.get_data()
            depth = mat_depth.get_data()

            # Size is taken from the first real frame rather than from the SDK's
            # camera_information struct, whose path moved between SDK 3.x and 4.x.
            if rgb_proc is None:
                height, width = left.shape[0], left.shape[1]
                print("ZED open at {0}x{1} @ {2}fps".format(width, height, args.fps))
                rgb_proc = spawn_sender(gst, width, height, args.fps, args.host,
                                        args.rgb_port, args.encoder, args.bitrate)
                depth_proc = spawn_sender(gst, width, height, args.fps, args.host,
                                          args.depth_port, args.encoder, args.bitrate)

            try:
                rgb_proc.stdin.write(left.tobytes())
                depth_proc.stdin.write(depth.tobytes())
            except (BrokenPipeError, OSError):
                sys.exit("A gst-launch process exited - rerun with the pipeline visible "
                         "(drop -q in spawn_sender) to see why.")

    except KeyboardInterrupt:
        print("\nStopping.")
    finally:
        for proc in (rgb_proc, depth_proc):
            if proc is not None:
                try:
                    proc.stdin.close()
                except OSError:
                    pass
                proc.wait(timeout=5)
        zed.close()


if __name__ == "__main__":
    main()
