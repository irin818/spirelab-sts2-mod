#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
output="${1:-../../../SpireLab/SpireLab/images/witch}"
mkdir -p "$output"/{cards,relics/big,potions}

png() {
    local input_count=$(($# - 1))
    ffmpeg -v error -y "${@:1:$input_count}" -frames:v 1 -update 1 "${!#}"
}

dimension() {
    sips -g "$1" "$2" | awk -v key="$1:" '$1 == key { print $2 }'
}

atlas_tile() {
    local source="$1" cols="$2" rows="$3" index="$4" margin="$5"
    local width height x0 y0 x1 y1
    width="$(dimension pixelWidth "$source")"
    height="$(dimension pixelHeight "$source")"
    x0=$((width * (index % cols) / cols + margin))
    y0=$((height * (index / cols) / rows + margin))
    x1=$((width * (index % cols + 1) / cols - margin))
    y1=$((height * (index / cols + 1) / rows - margin))
    printf 'crop=%d:%d:%d:%d' "$((x1-x0))" "$((y1-y0))" "$x0" "$y0"
}

bash export-previews.sh
cp preview/witch_select.jpg "$output/"
for id in r01 r10 r25; do
    cp "preview/$id.png" "$output/cards/"
done

while IFS=$'\t' read -r id atlas index; do
    id="$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')"
    image="source/$atlas"
    crop="$(atlas_tile "$image" 3 3 "$index" 7)"
    png -i "$image" -vf "$crop,crop=25*floor(min(iw/25\,ih/19)):19*floor(min(iw/25\,ih/19)),scale=500:380:flags=lanczos" \
        "$output/cards/$id.png"
done < <(tail -n +2 card-source-map.tsv)

for index in 0 1; do
    crop="$(atlas_tile source/ancient.png 2 1 "$index" 8)"
    png -i source/ancient.png -vf "$crop,crop=101*floor(min(iw/101\,ih/142)):142*floor(min(iw/101\,ih/142)),scale=606:852:flags=lanczos" \
        "$output/cards/a0$((index+1)).png"
done

png -i source/character-select.png -vf 'crop=280:280:930:45,scale=128:128:flags=lanczos' "$output/witch_portrait.png"
png -i "$output/witch_portrait.png" -vf 'hue=s=0,eq=brightness=-0.14' "$output/witch_portrait_locked.png"

for sprite in battle mirror-battle; do
    width=391
    name=witch_sprite
    if [[ "$sprite" == mirror-battle ]]; then
        width=238
        name=witch_mirror_form
    fi
    png -i "source/$sprite.png" \
        -vf "format=rgba,scale=$((width-16)):432:force_original_aspect_ratio=decrease:flags=lanczos,pad=$width:438:(ow-iw)/2:oh-ih-3:color=0x00000000" \
        "$output/$name.png"
done

items=(
    cracked_vanity_mirror twin_standing_mirror cracked_phial silver_mirror_frame
    lost_formula obsidian_distiller double_veil bloodstained_ring
    sealed_potion mist_draught twin_draught abyssal_distillate
)
for index in "${!items[@]}"; do
    item="${items[index]}"
    crop="$(atlas_tile source/items.png 4 3 "$index" 3)"
    if ((index < 9)); then
        png -i source/items.png -vf "format=rgba,$crop,scale=256:256:force_original_aspect_ratio=decrease:flags=lanczos,pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0x00000000" \
            "$output/relics/big/$item.png"
        png -i "$output/relics/big/$item.png" -vf 'scale=94:94:flags=lanczos' "$output/relics/$item.png"
        icon="$output/relics/$item"
        size=94
    else
        png -i source/items.png -vf "format=rgba,$crop,scale=256:256:force_original_aspect_ratio=decrease:flags=lanczos,pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0x00000000" \
            "$output/potions/$item.png"
        icon="$output/potions/$item"
        size=256
    fi
    png -i "$icon.png" -f lavfi -i "color=c=white:s=${size}x${size}" \
        -filter_complex '[0:v]alphaextract,dilation,dilation[mask];[1:v][mask]alphamerge' "${icon}_outline.png"
done
