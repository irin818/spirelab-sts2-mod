shader_type canvas_item;
render_mode unshaded;

uniform float recoil = 0.0;
varying vec4 tint;

void vertex() {
    tint = COLOR;
}

void fragment() {
    vec4 ink = texture(TEXTURE, UV);
    // The cuff and the foreshortened fingers keep their painted edges.
    float seam = 1.0 - smoothstep(0.79, 0.995, UV.y);
    seam *= 1.0 - smoothstep(0.87, 0.999, UV.x);
    float violet = smoothstep(0.66, 0.98, UV.y) * 0.28;
    ink.rgb = mix(ink.rgb, ink.rgb * vec3(0.71, 0.58, 0.93), violet);
    ink.rgb += vec3(0.18, 0.063, 0.11) * recoil;
    COLOR = vec4(ink.rgb, ink.a * seam) * tint;
}
