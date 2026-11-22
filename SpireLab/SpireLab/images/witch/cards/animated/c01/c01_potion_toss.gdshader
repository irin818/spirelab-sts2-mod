shader_type canvas_item;
render_mode unshaded;

// Pixel-locked 4-channel mask for C01 Potion Toss (`c01_masks.png`, 500x380, fix_alpha_border=false)
// R: Crimson potion liquid inside glass flask
// G: Glass flask rim highlights & reflections
// B: Swirling crimson alchemy ribbons & floating spark droplets (strictly off Witch & hand)
// A: Floating flask gentle hover zone (strictly 0 on Witch face, hair, torso, hand)
uniform sampler2D mask_tex : filter_linear, repeat_disable;

uniform float loop_phase = 0.0;        // [0, 1) seamless 3.4s cycle
uniform float effect_strength = 0.0;   // [0, 1] smooth wake/settle
uniform float is_inspect = 1.0;        // 1.0 = Play/Full richness, 0.0 = restrained
uniform float reduced_motion = 0.0;    // 1.0 = disable displacement & fast flicker

void fragment() {
    vec2 uv = UV;
    vec4 mask = texture(mask_tex, uv);
    float s = clamp(effect_strength, 0.0, 1.0);
    float motion_ok = (1.0 - reduced_motion) * is_inspect;

    // 1. Subtle floating flask bob + micro-slosh strictly inside flask envelope (0 px on Witch!)
    float ang = loop_phase * TAU;
    vec2 p = uv;
    float bob_y = sin(ang) * 0.0042 * mask.a * s * motion_ok;
    float bob_x = cos(ang) * 0.0018 * mask.a * s * motion_ok;
    // Gentle internal crimson liquid slosh wave inside lower flask
    float slosh = sin(ang * 2.0 + uv.x * 26.0) * 0.0024 * mask.r * s * motion_ok;
    p.y += bob_y + slosh;
    p.x += bob_x;

    vec4 col = texture(TEXTURE, clamp(p, vec2(0.001), vec2(0.999)));
    vec4 m = texture(mask_tex, clamp(p, vec2(0.001), vec2(0.999)));

    // Locked Witch palette
    vec3 garnet = vec3(0.812, 0.282, 0.416);      // #CF486A
    vec3 wine_red = vec3(0.471, 0.137, 0.239);    // #78233D
    vec3 cool_silver = vec3(0.804, 0.847, 0.886); // #CDD8E2
    vec3 ivory = vec3(0.918, 0.867, 0.882);       // #EADDE1

    // 2. Luminous crimson liquid pulse inside flask
    float liq_wave = 0.5 + 0.5 * sin(ang + uv.x * 9.0 - uv.y * 6.0);
    col.rgb += mix(wine_red, garnet, liq_wave) * m.r * (0.20 + 0.22 * liq_wave) * s;

    // 3. Cool-silver specular sweep across glass flask rim (peaks cleanly around phase 0.30)
    float sweep_pos = mix(0.66, 1.04, fract(loop_phase + 0.15));
    float diag = uv.x * 0.85 + uv.y * 0.45;
    float glass_sweep = exp(-pow((diag - sweep_pos) / 0.055, 2.0));
    col.rgb += mix(cool_silver, ivory, 0.45) * m.g * (0.14 + 0.55 * glass_sweep) * s;

    // 4. Traveling crimson glint along swirling alchemy ribbons & floating spark droplets
    float ribbon_flow = 0.5 + 0.5 * sin(ang * 2.0 - uv.x * 16.0 - uv.y * 12.0);
    float ribbon_peak = pow(ribbon_flow, 2.2);
    col.rgb += mix(garnet, ivory, 0.25 * ribbon_peak) * m.b * (0.16 + 0.48 * ribbon_peak) * s;

    COLOR = vec4(col.rgb, col.a);
}
