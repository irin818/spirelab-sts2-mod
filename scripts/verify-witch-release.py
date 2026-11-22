"""Validate Workshop content against the local ExportRelease build without contacting Steam."""

import argparse
import filecmp
import json
import os
import re
import shutil
import struct
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RELEASE = ROOT / "SpireLab/bin/mods/ExportRelease/SpireLab"
FILES = {"SpireLab.dll", "SpireLab.pck", "SpireLab.json"}
STEAM_PREVIEW_LIMIT = 1_000_000
# Reject retired development builds that may still exist outside the current checkout.
TEST_LAB_MARKERS = re.compile(
    rb"SpireLab[._-]?TestLab|DebugTrace|LabEndTurnHit|LabDummy|WITCH_LAB_SCRIPT|witch-test-lab|/testlab/",
    re.IGNORECASE,
)


def _check_regular_file(path):
    if path.is_symlink() or not path.is_file() or path.stat().st_size == 0:
        raise ValueError(f"Expected a nonempty regular file: {path}")


def check_png(path, *, expected_size=None, max_bytes=None):
    _check_regular_file(path)
    size_bytes = path.stat().st_size
    if max_bytes is not None and size_bytes >= max_bytes:
        raise ValueError(f"{path} exceeds {max_bytes} bytes ({size_bytes} bytes)")
    header = path.read_bytes()[:24]
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"Expected a valid PNG image: {path}")
    width, height = struct.unpack(">II", header[16:24])
    if expected_size is not None and (width, height) != expected_size:
        raise ValueError(f"{path} expected {expected_size[0]}x{expected_size[1]}, got {width}x{height}")


def check_gif(path, *, max_bytes=STEAM_PREVIEW_LIMIT):
    _check_regular_file(path)
    size_bytes = path.stat().st_size
    if size_bytes >= max_bytes:
        raise ValueError(f"{path} exceeds {max_bytes} bytes ({size_bytes} bytes)")
    data = path.read_bytes()
    if len(data) < 10 or data[:6] not in (b"GIF87a", b"GIF89a"):
        raise ValueError(f"Expected a valid GIF image: {path}")
    width, height = struct.unpack("<HH", data[6:10])
    if width != height:
        raise ValueError(f"{path} must be 1:1 square, got {width}x{height}")
    # Count Graphic Control Extension blocks to verify genuine multi-frame animation.
    if data.count(b"\x21\xf9\x04") < 2:
        raise ValueError(f"{path} must contain multiple animation frames")


def _resolve_ffprobe():
    found = shutil.which("ffprobe")
    if found:
        return found
    for prefix in ("/opt/homebrew/bin", "/usr/local/bin"):
        candidate = Path(prefix) / "ffprobe"
        if candidate.is_file() and os.access(candidate, os.X_OK):
            return str(candidate)
    raise ValueError("ffprobe is required to validate trailer.mp4")


def check_media(directory):
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError(f"Expected a real media directory: {directory}")
    check_png(directory / "image.png", expected_size=(800, 800), max_bytes=STEAM_PREVIEW_LIMIT)
    check_png(directory / "cover-master-1600.png", expected_size=(1600, 1600))
    check_png(directory / "video-cover.png", expected_size=(1920, 1080))
    trailer = directory / "trailer.mp4"
    _check_regular_file(trailer)
    probe = json.loads(subprocess.run(
        [
            _resolve_ffprobe(), "-v", "error",
            "-show_entries", "format=duration:stream=codec_name,codec_type,width,height",
            "-of", "json", str(trailer),
        ],
        check=True, text=True, stdout=subprocess.PIPE,
    ).stdout)
    streams = probe.get("streams", [])
    video = next((s for s in streams if s.get("codec_type") == "video"), None)
    audio = next((s for s in streams if s.get("codec_type") == "audio"), None)
    duration = float(probe.get("format", {}).get("duration", 0))
    if not video or video.get("codec_name") != "h264" or (video.get("width"), video.get("height")) != (1920, 1080):
        raise ValueError(f"{trailer} must be 1920x1080 H.264 video")
    if not audio or audio.get("codec_name") != "aac":
        raise ValueError(f"{trailer} must include AAC audio")
    if not (9.0 <= duration <= 11.5):
        raise ValueError(f"{trailer} duration out of range: {duration:.2f}s")
    gif = directory / "cover.gif"
    if gif.exists():
        check_gif(gif, max_bytes=STEAM_PREVIEW_LIMIT)


def check_files(directory, *, allow_symbols=False):
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError(f"Expected a real content directory: {directory}")
    entries = {entry.name: entry for entry in directory.iterdir()}
    allowed = FILES | ({"SpireLab.pdb"} if allow_symbols else set())
    missing, extra = FILES - entries.keys(), entries.keys() - allowed
    if missing or extra:
        raise ValueError(f"{directory}: missing {sorted(missing)}, unexpected {sorted(extra)}")
    for entry in entries.values():
        if entry.is_symlink() or not entry.is_file() or entry.stat().st_size == 0:
            raise ValueError(f"Expected a nonempty regular file: {entry}")


def check_release(directory):
    check_files(directory, allow_symbols=True)
    # ECMA-335 custom-attribute blob for the two strings and zero named arguments.
    key, value = b"SpireLab.PackageKind", b"WorkshopRelease"
    stamp = b"\x01\x00" + bytes([len(key)]) + key + bytes([len(value)]) + value + b"\x00\x00"
    for name in sorted(FILES):
        data = (directory / name).read_bytes()
        if TEST_LAB_MARKERS.search(data):
            raise ValueError(f"Test Lab code or resource paths found in {directory / name}")
        if name == "SpireLab.dll" and stamp not in data:
            raise ValueError(f"Missing ExportRelease build stamp: {directory / name}")


def check_candidate(directory, release):
    check_files(directory)
    for name in sorted(FILES):
        if not filecmp.cmp(directory / name, release / name, shallow=False):
            raise ValueError(f"{directory / name} differs from the verified ExportRelease build")


def verify(mode, path):
    if mode == "media":
        check_media(path)
        return
    check_release(RELEASE)
    if not filecmp.cmp(RELEASE / "SpireLab.json", ROOT / "SpireLab/SpireLab.json", shallow=False):
        raise ValueError("The current Mod manifest has changed. Rebuild using ./scripts/dev-macos.sh pack.")
    if mode == "tested-mods":
        if any(entry.name.casefold() == "spirelab.testlab" for entry in path.iterdir()):
            raise ValueError(f"Test Lab session cannot be used as a release source: {path}")
        check_candidate(path / "SpireLab", RELEASE)
    elif mode == "workshop":
        check_candidate(path / "content", RELEASE)
        preview = path / "image.png"
        if preview.exists():
            check_png(preview, max_bytes=STEAM_PREVIEW_LIMIT)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="mode", required=True)
    subparsers.add_parser("pack", help="check the local ExportRelease build")
    subparsers.add_parser("tested-mods", help="check the isolated release test's mods directory").add_argument("path", type=Path)
    subparsers.add_parser("workshop", help="check the staged SteamWorkshop workspace").add_argument("path", type=Path)
    subparsers.add_parser("media", help="check Workshop cover, trailer, and optional GIF deliverables").add_argument("path", type=Path)
    options = parser.parse_args()
    try:
        verify(options.mode, getattr(options, "path", None))
    except (OSError, ValueError) as error:
        parser.exit(1, f"Workshop release rejected: {error}\n")
    if options.mode == "media":
        print("Verified Workshop Media: image.png, cover-master-1600.png, video-cover.png, trailer.mp4")
    else:
        print("Verified Workshop Release: SpireLab.dll, SpireLab.pck, SpireLab.json")


if __name__ == "__main__":
    main()
