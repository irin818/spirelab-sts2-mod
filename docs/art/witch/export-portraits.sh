#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../../.."

source_dir=docs/art/witch
output_dir="${1:-SpireLab/SpireLab/images/witch/cards}"
mkdir -p "$output_dir"

# The source sheets use equal rows and columns in the following row-major order.
sheets=(
  'b01 b02 b03 b04 c01 c02 c03 c04 c05 c06 c07 c08 c09 c10 c11 c12'
  'c13 c14 c15 c16 c17 c18 c19 c20 u01 u02 u03 u04 u05 u06 u07 u08'
  'u09 u10 u11 u12 u13 u14 u15 u16 u17 u18 u19 u20 u21 u22 u23 u24'
  'u25 u26 u27 u28 u29 u30 u31 u32 u33 u34 u35 u36 u37 u38 r01 r02'
  'r03 r04 r05 r06 r07 r08 r09 r10 r11 r12 r13 r14 r15 r16 r17 r18'
  'r19 r20 r21 r22 r23 r24 r25 r26 r27 a01 a02 - - - - -'
)
for index in "${!sheets[@]}"; do
    sheet="$source_dir/cards-$((index + 1)).png"
    width="$(sips -g pixelWidth "$sheet" | awk '/pixelWidth:/ {print $2}')"
    height="$(sips -g pixelHeight "$sheet" | awk '/pixelHeight:/ {print $2}')"
    read -r -a ids <<< "${sheets[index]}"
    for tile in "${!ids[@]}"; do
        id="${ids[tile]}"
        [[ "$id" == "-" || "$id" == a0* ]] && continue
        left=$((width * (tile % 4) / 4))
        right=$((width * (tile % 4 + 1) / 4))
        top=$((height * (tile / 4) / 4))
        bottom=$((height * (tile / 4 + 1) / 4))
        # Stay inside the tile border and preserve the source's aspect ratio.
        w=$((right - left - 4))
        h=$((bottom - top - 4))
        crop_w=$((h * 500 / 380))
        crop_h="$h"
        if ((crop_w > w)); then
            crop_w="$w"
            crop_h=$((w * 380 / 500))
        fi
        x=$((left + (right - left - crop_w) / 2))
        y=$((top + (bottom - top - crop_h) / 3))
        ffmpeg -v error -y -i "$sheet" -vf "crop=$crop_w:$crop_h:$x:$y,scale=500:380:flags=lanczos" \
            -frames:v 1 -update 1 "$output_dir/$id.png"
    done
done

ancient="$source_dir/ancient-source.png"
width="$(sips -g pixelWidth "$ancient" | awk '/pixelWidth:/ {print $2}')"
height="$(sips -g pixelHeight "$ancient" | awk '/pixelHeight:/ {print $2}')"
for index in 0 1; do
    tile_w=$((width / 2))
    x=$((index * tile_w + 2))
    tile_w=$((tile_w - 4))
    tile_h=$((height - 4))
    ffmpeg -v error -y -i "$ancient" -filter_complex \
        "[0:v]crop=$tile_w:$tile_h:$x:2,split[full][ambient];[full]scale=-1:852[fg];[ambient]scale=606:852:force_original_aspect_ratio=increase,crop=606:852,boxblur=22[bg];[bg][fg]overlay=(W-w)/2:(H-h)/2" \
        -frames:v 1 -update 1 "$output_dir/a0$((index + 1)).png"
done
