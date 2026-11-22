shader_type canvas_item;
render_mode unshaded;

uniform float fracture = 0.0;
uniform float defocus = 0.0;
varying vec4 tint;

void vertex() {
    tint = COLOR;
}

void fragment() {
    vec2 uv = UV;
    vec4 spirit = textureLod(TEXTURE, uv, defocus);
    vec2 step_uv = TEXTURE_PIXEL_SIZE * 2.0;
    float inside = min(min(texture(TEXTURE, uv + vec2(step_uv.x, 0.0)).a,
                           texture(TEXTURE, uv - vec2(step_uv.x, 0.0)).a),
                       min(texture(TEXTURE, uv + vec2(0.0, step_uv.y)).a,
                           texture(TEXTURE, uv - vec2(0.0, step_uv.y)).a));
    float border = max(0.0, spirit.a - inside) / (1.0 + defocus);
    float split = abs(uv.x - 0.5 + (uv.y - 0.5) * 0.37 + sin(uv.y * 26.0) * 0.023);
    float branch = abs(uv.y - 0.46 - abs(uv.x - 0.53) * 0.5 + sin(uv.x * 39.0) * 0.015);
    float vein = max(exp(-split * 120.0), exp(-branch * 146.0)) * fracture;
    vec3 rgb = vec3(0.075, 0.037, 0.135) + spirit.rgb * 0.34;
    rgb += vec3(0.58, 0.52, 0.76) * border;
    rgb += vec3(0.83, 0.075, 0.21) * vein;
    COLOR = vec4(rgb * tint.rgb, spirit.a * tint.a);
}
