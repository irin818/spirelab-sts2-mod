#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
mkdir -p preview

# Keep the opaque full-bleed selection background and match the current 16:9 slot.
ffmpeg -v error -y -i source/character-select-reframed.png \
    -vf 'crop=1664:936,scale=1600:900:flags=lanczos' \
    -q:v 2 -frames:v 1 -update 1 preview/witch_select.jpg

# Crop to exactly 25:19 without stretching. Keep each source independently archived.
for card in r01-pinnacle-of-mana r10-the-mirror-self r25-fallen-confidence; do
    ffmpeg -v error -y -i "source/$card.png" \
        -vf 'crop=25*floor(min(iw/25\,ih/19)):19*floor(min(iw/25\,ih/19)),scale=500:380:flags=lanczos' \
        -frames:v 1 -update 1 "preview/${card%%-*}.png"
done
