shader_type canvas_item;
render_mode unshaded;

// Pixel-locked Live2D-style rig mask for witch_mirror_form.png (238x438, fix_alpha_border=false)
// R = inner oval mirror glass (excluding ornate silver frame)
// G = Mirror Self crimson eyes
// B = existing red glass cracks + ornate frame ruby gems
// A = deeply feathered inner Mirror Self breathing/sway zone
uniform sampler2D mirror_mask_tex : filter_linear, repeat_disable;

uniform float time_sec = 0.0;
uniform float mirror_breath = 0.0;    // [-1, 1] asynchronous breathing out of phase with real Witch
uniform float mirror_sway = 0.0;      // [-1, 1] subtle uncanny drift inside glass
uniform float mirror_awakened = 0.0;  // 0.0 = dormant phantom, 1.0 = R10 Mirror Form active
uniform float mirror_pulse = 0.0;     // [0, 1] temporary surge on Invert / Block / Reflect / Desire
uniform float inscription_glow = 0.0; // [0, 1] U21 Reflective Inscription persistent frame filigree glow
uniform float motion_scale = 1.0;

varying vec4 v_tint;

void vertex() {
    v_tint = COLOR;
}

void fragment() {
    vec2 uv = UV;
    vec4 m = texture(mirror_mask_tex, uv);
    float glass_w = m.r;
    float breath_w = m.a;

    // 1. Asynchronous Live2D inner Mirror Self motion strictly inside feathered glass (frame stays rigid!)
    vec2 p = uv;
    float sway_amp = (0.0038 + 0.0024 * mirror_awakened) * motion_scale;
    float breath_amp = (0.0046 + 0.0026 * mirror_awakened) * motion_scale;
    p.x -= mirror_sway * sway_amp * breath_w;
    p.y += mirror_breath * breath_amp * breath_w;
    // Slight ominous zoom-in lean when Mirror Form awakens or pulses
    vec2 glass_center = vec2(0.50, 0.57);
    float lean_zoom = (mirror_awakened * 0.014 + mirror_pulse * 0.018) * motion_scale * breath_w;
    p = mix(p, glass_center, lean_zoom);

    vec4 tex = texture(TEXTURE, clamp(p, vec2(0.002), vec2(0.998)));
    vec4 m_art = texture(mirror_mask_tex, clamp(p, vec2(0.002), vec2(0.998)));

    // 2. Periodic cool-silver diagonal glass sweep across inner oval mirror
    float sweep_phase = fract(time_sec / 4.8);
    float diag = uv.x * 0.85 + uv.y * 0.55;
    float sweep_pos = mix(-0.15, 1.55, sweep_phase);
    float sweep_band = exp(-pow((diag - sweep_pos) / 0.095, 2.0)) * glass_w * motion_scale;
    vec3 cool_silver = vec3(0.804, 0.847, 0.886); // #CDD8E2
    tex.rgb += cool_silver * sweep_band * (0.14 + 0.14 * mirror_awakened + 0.18 * mirror_pulse) * tex.a;

    // 3. Mirror Self crimson eyes glow (intensifies dramatically when Mirror Form awakens)
    float eye_wave = 0.55 + 0.45 * sin(time_sec * 2.35 + 1.1);
    float eye_intensity = (0.18 + 0.62 * mirror_awakened + 0.55 * mirror_pulse) * eye_wave;
    vec3 crimson = vec3(0.812, 0.282, 0.416); // #CF486A
    tex.rgb += crimson * m_art.g * eye_intensity * 0.82 * tex.a;

    // 4. Red glass cracks & ornate frame ruby gems shimmer
    float crack_wave = 0.4 + 0.6 * pow(0.5 + 0.5 * sin(time_sec * 1.85 - uv.y * 4.5), 2.0);
    float crack_intensity = (0.12 + 0.42 * mirror_awakened + 0.58 * mirror_pulse) * crack_wave;
    vec3 wine_red = vec3(0.68, 0.19, 0.34);
    tex.rgb += mix(wine_red, cool_silver, 0.28 * sweep_band) * m_art.b * crack_intensity * 0.65 * tex.a;

    // 5. Subtle cool-silver rim sheen & U21 Reflective Inscription glowing filigree along ornate frame
    float frame_w = clamp(tex.a - glass_w, 0.0, 1.0);
    float frame_glint = pow(0.5 + 0.5 * sin(time_sec * 1.4 + uv.y * 6.0), 3.0);
    float filigree_runes = pow(0.5 + 0.5 * sin(uv.y * 42.0 - time_sec * 2.2), 4.0) * inscription_glow;
    tex.rgb += cool_silver * frame_w * (frame_glint * (0.08 + 0.12 * mirror_awakened + 0.25 * mirror_pulse) + filigree_runes * 0.42);
    tex.rgb += crimson * frame_w * filigree_runes * 0.16;

    COLOR = tex * v_tint;
}
