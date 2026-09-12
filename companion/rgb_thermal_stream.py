#!/usr/bin/env python3
"""
Companion-computer sender for the NavXDrone Mission page - SIH26177 (Team Asunama)
hardware stack: an RGB camera and a thermal camera on the onboard Raspberry Pi 5,
not the ZED stereo camera companion/zed_stream.py was written for (that script is
NIDAR-era; keep it only if a ZED is still in use on some other airframe).

Grabs a frame from each camera with OpenCV, encodes both to H.264, and sends them
to the ground station as two RTP/UDP streams. The Mission page's two video tiles
pick them up with their default pipelines; see GCSViews\\Mission.cs
(DefaultRgbPipeline / DefaultThermalPipeline).

    RGB      -> udp/5600
    THERMAL  -> udp/5601

Frames are pushed into two `gst-launch-1.0` child processes over stdin, the same
approach zed_stream.py uses - gst-launch ships with the GStreamer install the
ground station already needs, so nothing extra to get working cross-platform.

THERMAL CAMERA NOTE: most hackathon-friendly thermal modules (FLIR Lepton on a
PureThermal2/3 breakout, Seek Thermal Compact/CompactPRO) enumerate as an
ordinary UVC webcam - `v4l2-ctl --list-devices` will show it next to the RGB
camera, and it just needs a --thermal-device index/path like the RGB one. Two
frame shapes are handled:
    - Already-colourised (the board's own AGC/palette mode): passed straight
      through - most PureThermal boards default to this.
    - Raw single-channel (radiometric/greyscale mode): false-coloured here with
      an OpenCV colormap (--colormap, default INFERNO) before encoding, so the
      GCS tile reads as a proper thermal image rather than a grey blob.
If your thermal module instead exposes only a raw sensor SDK (some MLX90640
breakouts do, over I2C rather than UVC) that capture step needs swapping in for
grab_thermal() below - everything downstream (encode/send) stays the same.

Requirements on this machine (Raspberry Pi OS or any Linux/Windows box with the
cameras attached):
    - opencv-python          (pip install opencv-python)
    - numpy
    - GStreamer 1.x with the gst-launch-1.0 CLI (already required by the ZED
      companion script; `sudo apt install gstreamer1.0-tools` on the Pi)

Usage:
    python rgb_thermal_stream.py --host 192.168.1.50 --rgb-device 0 --thermal-device 1
    # Raw/greyscale thermal module, Pi hardware encoder:
    python rgb_thermal_stream.py --host 192.168.1.50 --thermal-device 1 \\
        --thermal-raw --encoder v4l2h264enc
"""

import argparse
import os
import shutil
import subprocess
import sys

try:
    import cv2
except ImportError:
    sys.exit("opencv-python not found: python -m pip install opencv-python")


GST_FALLBACK_DIRS = [
    r"C:\gstreamer\1.0\msvc_x86_64\bin",
    r"C:\gstreamer\1.0\mingw_x86_64\bin",
    "/usr/bin",
    "/usr/local/bin",
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

    sys.exit("gst-launch-1.0 not found. Install GStreamer, or pass --gst with "
             "the full path to gst-launch-1.0.exe.")


def open_capture(device, width, height, fps):
    """--rgb-device/--thermal-device take either a plain index ("0") or a device
    path ("/dev/video2") - most Pi setups end up needing the path once both
    cameras are plugged in, since V4L2 index order isn't guaranteed stable
    across reboots."""
    cap = cv2.VideoCapture(int(device) if device.isdigit() else device)
    if not cap.isOpened():
        sys.exit("Could not open camera: {0}".format(device))

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    cap.set(cv2.CAP_PROP_FPS, fps)
    return cap


def encoder_chain(encoder, bitrate, fps):
    """
    v4l2h264enc is the Pi's own hardware H.264 block - the one to use on-drone,
    since a Pi 5 has no GPU encoder of the kind nvh264enc needs. x264enc is the
    portable software fallback for a dev-machine test without a Pi. Both are
    tuned for low latency rather than quality - a stale frame is worse than a
    soft one on a live feed.
    """
    if encoder == "v4l2h264enc":
        return ("v4l2h264enc extra-controls=\"controls,video_bitrate={0},"
                "h264_i_frame_period=1\"").format(bitrate * 1000)
    return ("x264enc tune=zerolatency speed-preset=ultrafast "
            "bitrate={0} key-int-max={1}".format(bitrate, fps * 2))


def spawn_sender(gst, width, height, fps, host, port, encoder, bitrate, label):
    """One gst-launch reading raw BGR on stdin and emitting RTP/H.264 on a UDP port."""
    pipeline = (
        "fdsrc fd=0 ! "
        "rawvideoparse width={w} height={h} format=bgr framerate={fps}/1 ! "
        "videoconvert ! video/x-raw,format=I420 ! "
        "{enc} ! "
        "h264parse config-interval=-1 ! "
        "rtph264pay config-interval=1 pt=96 ! "
        "udpsink host={host} port={port} sync=false async=false"
    ).format(w=width, h=height, fps=fps, host=host, port=port,
             enc=encoder_chain(encoder, bitrate, fps))

    print("  {0}: udp/{1} <- {2}".format(label, port, encoder))

    return subprocess.Popen([gst, "-q"] + pipeline.split(), stdin=subprocess.PIPE)


def grab_rgb(cap):
    ok, frame = cap.read()
    if not ok:
        return None
    return frame  # already BGR


def grab_thermal(cap, raw, colormap):
    """
    Returns a BGR frame ready to encode. --thermal-raw treats whatever the
    camera hands back as single-channel and false-colours it; otherwise the
    frame is assumed already colourised by the board's own firmware and is
    passed through untouched (only reshaped to BGR if it came back grayscale
    anyway, which some boards do even in "colour" mode over UVC).
    """
    ok, frame = cap.read()
    if not ok:
        return None

    if raw or frame.ndim == 2:
        gray = frame if frame.ndim == 2 else cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        return cv2.applyColorMap(gray, colormap)

    return frame


COLORMAPS = {
    "inferno": cv2.COLORMAP_INFERNO,
    "jet": cv2.COLORMAP_JET,
    "hot": cv2.COLORMAP_HOT,
}


def main():
    ap = argparse.ArgumentParser(
        description="Stream RGB + thermal cameras to a NavXDrone ground station.")
    ap.add_argument("--host", required=True,
                    help="Ground-station IP (the machine running NavXDrone).")
    ap.add_argument("--rgb-device", default="0", help="Index or /dev/videoN path.")
    ap.add_argument("--thermal-device", default="1", help="Index or /dev/videoN path.")
    ap.add_argument("--rgb-port", type=int, default=5600)
    ap.add_argument("--thermal-port", type=int, default=5601)
    ap.add_argument("--width", type=int, default=1280)
    ap.add_argument("--height", type=int, default=720)
    ap.add_argument("--thermal-width", type=int, default=None,
                    help="Defaults to --width; most thermal modules are far lower-res "
                         "(a Lepton 3.5 is 160x120) so the camera clamps this itself.")
    ap.add_argument("--thermal-height", type=int, default=None)
    ap.add_argument("--fps", type=int, default=25)
    ap.add_argument("--bitrate", type=int, default=3000, help="kbit/s per stream.")
    ap.add_argument("--encoder", default="v4l2h264enc",
                    choices=["v4l2h264enc", "x264enc"],
                    help="v4l2h264enc for the Pi's hardware encoder; x264enc to test on "
                         "a dev machine without one.")
    ap.add_argument("--thermal-raw", action="store_true",
                    help="Pass if the thermal camera streams raw greyscale/radiometric "
                         "frames rather than an already-colourised palette.")
    ap.add_argument("--colormap", default="inferno", choices=sorted(COLORMAPS),
                    help="False-colour palette used only with --thermal-raw.")
    ap.add_argument("--gst", default=None, help="Path to gst-launch-1.0 if not on PATH.")
    args = ap.parse_args()

    gst = find_gst_launch(args.gst)
    colormap = COLORMAPS[args.colormap]

    rgb_cap = open_capture(args.rgb_device, args.width, args.height, args.fps)
    thermal_cap = open_capture(
        args.thermal_device,
        args.thermal_width or args.width,
        args.thermal_height or args.height,
        args.fps,
    )

    rgb_proc = thermal_proc = None
    print("Streaming to {0} (Ctrl+C to stop)".format(args.host))

    try:
        while True:
            rgb_frame = grab_rgb(rgb_cap)
            thermal_frame = grab_thermal(thermal_cap, args.thermal_raw, colormap)
            if rgb_frame is None or thermal_frame is None:
                continue

            # Sizes taken from the first real frame - a thermal module usually reports
            # its native resolution regardless of the CAP_PROP_FRAME_* request above.
            if rgb_proc is None:
                rh, rw = rgb_frame.shape[:2]
                th, tw = thermal_frame.shape[:2]
                print("RGB {0}x{1} @ {2}fps, THERMAL {3}x{4}".format(rw, rh, args.fps, tw, th))
                rgb_proc = spawn_sender(gst, rw, rh, args.fps, args.host,
                                        args.rgb_port, args.encoder, args.bitrate, "RGB")
                thermal_proc = spawn_sender(gst, tw, th, args.fps, args.host,
                                            args.thermal_port, args.encoder, args.bitrate, "THERMAL")

            try:
                rgb_proc.stdin.write(rgb_frame.tobytes())
                thermal_proc.stdin.write(thermal_frame.tobytes())
            except (BrokenPipeError, OSError):
                sys.exit("A gst-launch process exited - rerun with the pipeline visible "
                         "(drop -q in spawn_sender) to see why.")

    except KeyboardInterrupt:
        print("\nStopping.")
    finally:
        for proc in (rgb_proc, thermal_proc):
            if proc is not None:
                try:
                    proc.stdin.close()
                except OSError:
                    pass
                proc.wait(timeout=5)
        rgb_cap.release()
        thermal_cap.release()


if __name__ == "__main__":
    main()
