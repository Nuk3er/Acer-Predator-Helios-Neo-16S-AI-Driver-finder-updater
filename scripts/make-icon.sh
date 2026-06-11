#!/usr/bin/env bash
# Renders assets/icon.svg into a multi-resolution Windows ICO and copies it
# where the App project expects it. Requires ImageMagick; uses rsvg-convert
# for rasterization when available (sharper output).
set -euo pipefail
cd "$(dirname "$0")/.."

workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT

sizes=(16 24 32 48 64 128 256)

for size in "${sizes[@]}"; do
  out="$workdir/icon-$size.png"
  if command -v rsvg-convert >/dev/null 2>&1; then
    rsvg-convert -w "$size" -h "$size" assets/icon.svg -o "$out"
  else
    convert -background none -density 300 assets/icon.svg -resize "${size}x${size}" "$out"
  fi
done

convert "$workdir"/icon-{16,24,32,48,64,128,256}.png assets/icon.ico
mkdir -p src/HeliosToolkit.App/Resources
cp assets/icon.ico src/HeliosToolkit.App/Resources/icon.ico
echo "Wrote assets/icon.ico and src/HeliosToolkit.App/Resources/icon.ico"
