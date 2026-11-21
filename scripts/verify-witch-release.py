"""Validate Workshop content against the local ExportRelease build without contacting Steam."""

import argparse
import filecmp
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RELEASE = ROOT / "SpireLab/bin/mods/ExportRelease/SpireLab"
FILES = {"SpireLab.dll", "SpireLab.pck", "SpireLab.json"}
TEST_LAB_MARKERS = re.compile(
    rb"SpireLab[._-]?TestLab|DebugTrace|LabEndTurnHit|LabDummy|WITCH_LAB_SCRIPT|witch-test-lab|/testlab/",
    re.IGNORECASE,
)


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
    check_release(RELEASE)
    if not filecmp.cmp(RELEASE / "SpireLab.json", ROOT / "SpireLab/SpireLab.json", shallow=False):
        raise ValueError("The current Mod manifest has changed. Rebuild using ./scripts/dev-macos.sh pack.")
    if mode == "tested-mods":
        if any(entry.name.casefold() == "spirelab.testlab" for entry in path.iterdir()):
            raise ValueError(f"Test Lab session cannot be used as a release source: {path}")
        check_candidate(path / "SpireLab", RELEASE)
    elif mode == "workshop":
        check_candidate(path / "content", RELEASE)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="mode", required=True)
    subparsers.add_parser("pack", help="check the local ExportRelease build")
    subparsers.add_parser("tested-mods", help="check the isolated release test's mods directory").add_argument("path", type=Path)
    subparsers.add_parser("workshop", help="check the staged SteamWorkshop workspace").add_argument("path", type=Path)
    options = parser.parse_args()
    try:
        verify(options.mode, getattr(options, "path", None))
    except (OSError, ValueError) as error:
        parser.exit(1, f"Workshop release rejected: {error}\n")
    print("Verified Workshop Release: SpireLab.dll, SpireLab.pck, SpireLab.json")


if __name__ == "__main__":
    main()
