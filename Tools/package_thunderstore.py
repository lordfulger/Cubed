#!/usr/bin/env python3
"""Zip a folder into a Thunderstore / r2modman package.

Usage:
    python Tools/package_thunderstore.py Modding/ExampleDataPack [--author YourTeam] [--out dist]

The folder must contain manifest.json and README.md. If icon.png (256x256) is missing, a placeholder
cube icon is generated. The zip is named <Author>-<Name>-<version>.zip.
"""
import argparse
import json
import os
import re
import struct
import sys
import zipfile
import zlib


def make_icon(path, rgb=(90, 160, 255)):
    """Write a 256x256 PNG of a simple isometric-ish cube."""
    size = 256
    rows = []
    for y in range(size):
        row = bytearray([0])
        for x in range(size):
            r, g, b, a = 20, 22, 32, 255
            cx, cy = x - 128, y - 128
            if abs(cx) < 70 and abs(cy) < 70:
                shade = 1.0 if cy < -20 else (0.8 if cx < 0 else 0.6)
                r, g, b = (int(c * shade) for c in rgb)
                if abs(cx) > 64 or abs(cy) > 64:
                    r, g, b = 240, 240, 250
            row += bytes((r, g, b, a))
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(kind, data):
        c = struct.pack(">I", len(data)) + kind + data
        return c + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--author", default="CubedModder", help="Thunderstore team name (used in the zip name)")
    ap.add_argument("--out", default="dist")
    args = ap.parse_args()

    folder = args.folder
    manifest_path = os.path.join(folder, "manifest.json")
    if not os.path.isfile(manifest_path):
        sys.exit("manifest.json not found in " + folder)
    with open(manifest_path, encoding="utf-8") as f:
        manifest = json.load(f)

    errors = []
    for key in ("name", "version_number", "website_url", "description", "dependencies"):
        if key not in manifest:
            errors.append("manifest.json is missing '%s'" % key)
    if not re.fullmatch(r"[A-Za-z0-9_]+", manifest.get("name", "")):
        errors.append("name may only contain letters, digits and underscores")
    if not re.fullmatch(r"\d+\.\d+\.\d+", manifest.get("version_number", "")):
        errors.append("version_number must look like 1.0.0")
    if len(manifest.get("description", "")) > 250:
        errors.append("description must be 250 characters or less")
    if not os.path.isfile(os.path.join(folder, "README.md")):
        errors.append("README.md is required")
    if errors:
        sys.exit("\n".join(errors))

    icon = os.path.join(folder, "icon.png")
    if not os.path.isfile(icon):
        make_icon(icon)
        print("generated placeholder icon.png")

    os.makedirs(args.out, exist_ok=True)
    zip_name = "%s-%s-%s.zip" % (args.author, manifest["name"], manifest["version_number"])
    zip_path = os.path.join(args.out, zip_name)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for root, _, files in os.walk(folder):
            for name in files:
                full = os.path.join(root, name)
                rel = os.path.relpath(full, folder).replace(os.sep, "/")
                if rel.startswith(("bin/", "obj/")) or name.endswith((".cs", ".csproj")):
                    continue
                z.write(full, rel)
    print("wrote", zip_path)


if __name__ == "__main__":
    main()
