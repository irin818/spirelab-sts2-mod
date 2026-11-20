#!/usr/bin/env python3
"""Validate and import Witch art without modifying the source images."""

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "artwork"
GAME_ART = ROOT / "SpireLab/SpireLab/images/witch"
PACKAGE = ROOT / "SpireLab/bin/mods/ExportRelease/SpireLab"
CROPS = {"cover", "contain", "contain-bottom", "grayscale", "outline", "svg"}


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, cwd=ROOT, **kwargs)


def digest(path):
    with path.open("rb") as image:
        return hashlib.file_digest(image, "sha256").hexdigest()


def inside(root, relative):
    path = relative.resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f"path escapes its directory: {relative}")
    return path


def metadata(path):
    fields = run("sips", "-g", "format", "-g", "pixelWidth", "-g", "pixelHeight", "-g", "hasAlpha", path,
                 capture_output=True).stdout
    return dict(re.findall(r"^\s*(format|pixelWidth|pixelHeight|hasAlpha):\s+(\w+)", fields, re.M))


def check_svg(path, width, height):
    document = ET.parse(path).getroot()
    if document.tag != "{http://www.w3.org/2000/svg}svg":
        raise ValueError("expected an SVG document")
    dimensions = tuple(document.get(key, "").removesuffix("px") for key in ("width", "height"))
    if dimensions != (str(width), str(height)):
        raise ValueError(f"SVG needs explicit {width}×{height} width and height")


def check_alpha(path):
    result = run(
        "ffmpeg", "-v", "error", "-i", path, "-vf", "alphaextract,signalstats,metadata=print:file=-",
        "-frames:v", "1", "-f", "null", "-", capture_output=True
    ).stdout
    minimum = re.search(r"signalstats.YMIN=(\d+)", result)
    maximum = re.search(r"signalstats.YMAX=(\d+)", result)
    if not minimum or not maximum or int(minimum[1]) == 255 or int(maximum[1]) == 0:
        raise ValueError("expected both visible pixels and transparency")


def check_source(source, row):
    width, height = int(row["required_width"]), int(row["required_height"])
    if row["crop_mode"] == "svg":
        check_svg(source, width, height)
        return

    info = metadata(source)
    if info.get("format") not in {"png", "jpeg"} or not int(info.get("pixelWidth", 0)) or not int(info.get("pixelHeight", 0)):
        raise ValueError("could not read a PNG or JPEG image")
    if row["alpha_required"] == "true":
        if info.get("hasAlpha") != "yes":
            raise ValueError("an alpha channel is required")
        check_alpha(source)


def render(source, target, row):
    width, height = int(row["required_width"]), int(row["required_height"])
    mode = row["crop_mode"]
    if mode == "svg":
        shutil.copy2(source, target)
        return

    if mode in ("cover", "grayscale"):
        transform = f"scale={width}:{height}:force_original_aspect_ratio=increase:flags=lanczos,crop={width}:{height}"
        if mode == "grayscale":
            transform += ",hue=s=0,eq=brightness=-0.14"
    else:
        offset = "oh-ih" if mode == "contain-bottom" else "(oh-ih)/2"
        transform = (f"format=rgba,scale={width}:{height}:force_original_aspect_ratio=decrease:flags=lanczos,"
                     f"pad={width}:{height}:(ow-iw)/2:{offset}:color=0x00000000")
    if mode == "outline":
        graph = (f"[0:v]{transform},format=rgba,alphaextract,format=gray,dilation,dilation[mask];"
                 "[1:v]format=rgba[base];[base][mask]alphamerge")
        run("ffmpeg", "-v", "error", "-y", "-i", source, "-f", "lavfi", "-i", f"color=white:s={width}x{height}",
            "-filter_complex", graph, "-frames:v", "1", "-update", "1", target)
    else:
        run("ffmpeg", "-v", "error", "-y", "-i", source, "-vf", transform, "-frames:v", "1", "-update", "1", target)
    info = metadata(target)
    expected_format = "jpeg" if target.suffix == ".jpg" else "png"
    if info.get("format") != expected_format or (info.get("pixelWidth"), info.get("pixelHeight")) != (str(width), str(height)):
        raise ValueError("exported image has an unexpected format or size")
    if row["alpha_required"] == "true":
        check_alpha(target)


def load_manifest():
    with (ART / "manifest.csv").open(newline="", encoding="utf-8-sig") as table:
        rows = list(csv.DictReader(table))
    ids, targets = set(), set()
    for row in rows:
        asset = row["asset_id"]
        target = inside(GAME_ART, ROOT / row["target_path"])
        if not re.fullmatch(r"[A-Z][A-Z0-9_]*", asset) or asset in ids or target in targets:
            raise ValueError(f"invalid or duplicate asset or target: {asset}")
        if (row["crop_mode"] not in CROPS or row["alpha_required"] not in {"true", "false"}
                or int(row["required_width"]) <= 0 or int(row["required_height"]) <= 0
                or not row["game_object_id"] or not row["license"] or not row["status"]):
            raise ValueError(f"invalid mapping: {asset}")
        if row["fallback_path"]:
            inside(GAME_ART, ROOT / row["fallback_path"])
        Path(row["source_path"]).relative_to("source")
        ids.add(asset)
        targets.add(target)
    return rows


def import_batch(changes, temporary):
    backups = {}
    affected = [path for _, target in changes for path in (target, Path(str(target) + ".import"))]
    affected += [PACKAGE / f"SpireLab.{extension}" for extension in ("dll", "pck", "json", "pdb")]
    for index, path in enumerate(dict.fromkeys(affected)):
        backup = temporary / f"previous-{index}"
        if path.is_file():
            shutil.copy2(path, backup)
            backups[path] = backup
        else:
            backups[path] = None
    try:
        for output, target in changes:
            target.parent.mkdir(parents=True, exist_ok=True)
            os.replace(output, target)
        env = os.environ | {"SPIRELAB_FORCE_ART_IMPORT": "1"}
        run("bash", ROOT / "scripts/dev-macos.sh", "pack", env=env)
    except BaseException:
        for target, backup in backups.items():
            if backup is None:
                target.unlink(missing_ok=True)
            else:
                os.replace(backup, target)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="check files and mappings without writing or packing")
    parser.add_argument("--source", type=Path, default=ART / "source", help="directory containing character/, cards/, etc.")
    args = parser.parse_args()
    source_root = args.source.resolve()
    if not source_root.is_dir():
        raise ValueError(f"source directory not found: {source_root}")
    rows = load_manifest()
    known = {Path(row["source_path"]).relative_to("source") for row in rows}
    for path in sorted(source_root.rglob("*")):
        if path.is_file() and path.suffix.lower() in {".png", ".jpg", ".jpeg", ".svg"} and path.relative_to(source_root) not in known:
            print(f"Unmapped source (ignored): {path.relative_to(source_root)}")

    generated = ART / "generated"
    cache_file = generated / "sync-state.json"
    cache = json.loads(cache_file.read_text()) if cache_file.exists() else {}
    prepared, errors, use_existing = [], [], 0
    checked_sources = set()
    for row in rows:
        asset = row["asset_id"]
        try:
            source = inside(source_root, source_root / Path(row["source_path"]).relative_to("source"))
            target = inside(GAME_ART, ROOT / row["target_path"])
            if not source.is_file():
                fallback = row["fallback_path"] and (ROOT / row["fallback_path"]).is_file()
                if target.is_file() or fallback:
                    use_existing += 1
                else:
                    errors.append(f"{asset}: no source or existing game asset")
                continue
            key = (source, row["alpha_required"], row["crop_mode"] == "svg", row["required_width"], row["required_height"])
            if key not in checked_sources:
                check_source(source, row)
                checked_sources.add(key)
            fingerprint = hashlib.sha256(json.dumps([1, row, digest(source)], sort_keys=True).encode()).hexdigest()
            if target.is_file() and cache.get(asset) == [fingerprint, digest(target)]:
                print(f"Current: {asset}")
                continue
            print(f"Ready: {asset} -> {row['target_path']}")
            prepared.append((row, source, target, fingerprint))
        except (ValueError, RuntimeError, ET.ParseError, subprocess.CalledProcessError) as error:
            errors.append(f"{asset}: {error}")
    print(f"{len(rows)} mappings, {use_existing} using shipped resources, {len(prepared)} to import, {len(errors)} errors.")
    if errors:
        raise ValueError("\n".join(errors))
    if args.dry_run or not prepared:
        return

    generated.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(dir=generated) as directory:
        temporary = Path(directory)
        changes = []
        for row, source, target, fingerprint in prepared:
            output = temporary / (row["asset_id"] + target.suffix)
            try:
                render(source, output, row)
            except (ValueError, subprocess.CalledProcessError) as error:
                raise ValueError(f"{row['asset_id']}: {error}") from error
            result = digest(output)
            if not target.is_file() or digest(target) != result:
                changes.append((output, target))
            cache[row["asset_id"]] = [fingerprint, result]
        if changes:
            import_batch(changes, temporary)
        next_cache = temporary / "sync-state.json"
        next_cache.write_text(json.dumps(cache, indent=2) + "\n")
        os.replace(next_cache, cache_file)
    print(f"Imported {len(changes)} game textures. Originals kept in {source_root}.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"Art sync stopped: {error}") from None
