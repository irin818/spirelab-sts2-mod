shader_type canvas_item;
render_mode unshaded;

uniform float eclipse = 0.0;
varying vec4 tint;

void vertex() {
    tint = COLOR;
}

void fragment() {
    vec4 moon = texture(TEXTURE, UV);
    float radius = length((UV - vec2(0.51, 0.52)) * 2.0);
    float ink = 1.0 - smoothstep(0.18, 0.98, radius);
    vec3 silver = moon.rgb * vec3(0.97, 0.95, 1.0);
    vec3 crimson = silver * vec3(0.98, 0.30, 0.44) * (1.0 - ink * 0.3);
    COLOR = vec4(mix(silver, crimson, eclipse * (0.6 + ink * 0.4)), moon.a) * tint;
}
