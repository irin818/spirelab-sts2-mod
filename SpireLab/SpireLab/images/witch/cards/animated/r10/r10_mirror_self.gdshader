shader_type canvas_item;

// R10 "The Mirror Self" — Art-locked animated card shader.
// Preserves original r10.png linework, proportions, and gothic palette:
//   Ink #1C161D, Charcoal #302832, Ivory #EADDE1, Cool Silver #CDD8E2,
//   Wine Crimson #78233D, Pomegranate Crimson #CF486A.
//
// Packed RGBA mask (500x380, pixel-aligned to r10.png):
//   R = Mirror glass interior glare mask (excludes silver frame, real Witch, & protects Mirror Self face)
//   G = Mirror Self crimson iris mask (pixel-locked to x=[310..323], y=[132..146])
//   B = Existing painted glass cracks mask (lower-right mirror glass only)
//   A = Deeply feathered inner mirror breathing zone (0 at frame, fingers, & crack plane)

uniform sampler2D mask_tex : filter_linear, repeat_disable;
uniform float loop_phase : hint_range(0.0, 1.0) = 0.0;
uniform float effect_strength : hint_range(0.0, 1.0) = 0.0;
uniform float inspect_weight : hint_range(0.0, 1.0) = 0.0;
uniform float reduced_motion : hint_range(0.0, 1.0) = 0.0;

const float TAU_F = 6.28318530718;

// Smooth periodic pulse centered at `center` with half-width `width` on [0, 1) wrap.
float periodic_pulse(float phase, float center, float width) {
    float d = abs(fract(phase - center + 0.5) - 0.5);
    float x = clamp(1.0 - d / max(width, 0.0001), 0.0, 1.0);
    return x * x * (3.0 - 2.0 * x);
}

// Restrained drifting mirror dust mote strictly inside dark glass pockets.
float mirror_mote(vec2 uv, vec2 anchor, vec2 drift, float phase, float phase_offset, float radius) {
    float p = fract(phase + phase_offset);
    float life = sin(p * 3.14159265);
    life = life * life;
    vec2 pos = anchor + drift * (p - 0.5) + vec2(sin(p * TAU_F) * 0.008, cos(p * TAU_F) * 0.006);
    vec2 delta = (uv - pos) * vec2(1.0, 380.0 / 500.0);
    float dist = length(delta);
    float core = smoothstep(radius, radius * 0.15, dist);
    return core * life;
}

void fragment() {
    vec4 orig = texture(TEXTURE, UV);
    if (effect_strength <= 0.0005) {
        COLOR = orig;
    } else {
        vec4 m = texture(mask_tex, UV);
        float glass_mask = m.r;
        float iris_mask = m.g;
        float crack_mask = m.b;
        float breath_mask = m.a;

        float angle = loop_phase * TAU_F;
        float motion_scale = mix(1.0, 0.0, reduced_motion);

        // -----------------------------------------------------------------
        // Effect 4: Subtle asynchronous Mirror Self breathing inside glass
        // Strictly bounded (<= 1.05 px on 500x380), inspect-only, feathered to 0
        // well before frame, touching fingers, and glass crack plane.
        // -----------------------------------------------------------------
        float breath_wave = sin(angle) + 0.30 * sin(angle * 2.0 + 1.05);
        float sway_wave = cos(angle + 0.55) * 0.72 + 0.25 * sin(angle * 2.0 - 0.4);
        vec2 breath_px = vec2(sway_wave * 0.65, breath_wave * 0.95);
        vec2 uv_offset = (breath_px / vec2(500.0, 380.0)) * breath_mask * inspect_weight * effect_strength * motion_scale;
        vec2 sample_uv = clamp(UV - uv_offset, vec2(0.0), vec2(1.0));

        vec4 base_col = texture(TEXTURE, sample_uv);
        vec3 col = base_col.rgb;
        float lum = dot(col, vec3(0.2126, 0.7152, 0.0722));

        // Subtle dark-glass crimson presence behind the Mirror Self silhouette.
        float dark_glass = pow(clamp(1.0 - lum * 1.15, 0.0, 1.0), 1.8) * glass_mask;
        float aura_pulse = 0.5 + 0.5 * sin(angle - 0.85);
        vec3 wine_crimson = vec3(0.471, 0.137, 0.239); // #78233D
        col += wine_crimson * dark_glass * (0.045 + 0.075 * aura_pulse) * effect_strength * mix(0.65, 1.0, inspect_weight);

        // -----------------------------------------------------------------
        // Effect 1: Restrained Cool-Silver Mirror Sweep (peaks ~phase 0.28)
        // Slides diagonally across the mirror glass while face_protect keeps
        // the Mirror Self's face and expression crisp and unwashed.
        // -----------------------------------------------------------------
        float sweep_env = periodic_pulse(loop_phase, 0.28, 0.26);
        float sweep_t = clamp((loop_phase - 0.04) / 0.48, 0.0, 1.0);
        // Smooth sweep coordinate across mirror diagonal (0.48..1.38).
        float diag = UV.x * 0.78 + UV.y * 0.62;
        float band_pos = mix(0.42, 1.42, sweep_t);
        float primary_band = exp(-pow((diag - band_pos) / 0.068, 2.0));
        float caustic_band = exp(-pow((diag - (band_pos - 0.055)) / 0.018, 2.0)) * 0.55;
        vec3 cool_silver = vec3(0.804, 0.847, 0.886); // #CDD8E2
        float sweep_intensity = mix(0.17, 0.25, inspect_weight) * mix(1.0, 0.55, reduced_motion);
        // Protect already-bright ivory lace from clipping to pure white.
        float highlight_guard = 1.0 - smoothstep(0.72, 0.98, lum) * 0.55;
        col += cool_silver * (primary_band + caustic_band) * glass_mask * sweep_env * sweep_intensity * highlight_guard * effect_strength;

        // -----------------------------------------------------------------
        // Effect 2: Mirror Self Crimson Iris Glint (peaks ~phase 0.54)
        // Pixel-locked exclusively to the Mirror Self's crimson iris (mask.g).
        // -----------------------------------------------------------------
        float eye_pulse = periodic_pulse(loop_phase, 0.54, 0.22)
                        + 0.35 * periodic_pulse(loop_phase, 0.84, 0.14);
        vec3 pomegranate = vec3(0.812, 0.282, 0.416); // #CF486A
        vec2 iris_center = vec2(0.631, 0.366) + uv_offset;
        float iris_dist = length((UV - iris_center) * vec2(1.0, 380.0 / 500.0));
        float iris_spark = smoothstep(0.016, 0.002, iris_dist);
        float eye_strength = mix(0.32, 0.48, inspect_weight) * mix(1.0, 0.6, reduced_motion);
        col += (pomegranate * 0.82 + vec3(0.95, 0.86, 0.90) * iris_spark * 0.42) * iris_mask * eye_pulse * eye_strength * effect_strength;

        // -----------------------------------------------------------------
        // Effect 3: Existing Painted Glass Cracks Shimmer (peaks ~phase 0.72)
        // Glint travels cleanly along the real painted lower-right crack lines.
        // -----------------------------------------------------------------
        float crack_env = periodic_pulse(loop_phase, 0.72, 0.24)
                        + 0.28 * periodic_pulse(loop_phase, 0.34, 0.15);
        float crack_coord = clamp((UV.x - 0.54) * 1.45 + (0.94 - UV.y) * 0.85, 0.0, 1.0);
        float crack_wave_pos = fract(loop_phase * 1.0 + 0.32);
        float crack_glint = 0.45 + 0.55 * exp(-pow((crack_coord - crack_wave_pos) / 0.22, 2.0));
        vec3 crack_color = mix(cool_silver, pomegranate, 0.18);
        float crack_strength = mix(0.36, 0.55, inspect_weight) * mix(1.0, 0.6, reduced_motion);
        col += crack_color * crack_mask * crack_env * crack_glint * crack_strength * effect_strength;

        // -----------------------------------------------------------------
        // Subtle Floating Mirror Motes (strictly inside dark glass corners,
        // disabled in reduced motion, never crossing face).
        // -----------------------------------------------------------------
        if (motion_scale > 0.01) {
            float m1 = mirror_mote(UV, vec2(0.61, 0.12), vec2(0.02, -0.05), loop_phase, 0.10, 0.0075);
            float m2 = mirror_mote(UV, vec2(0.84, 0.24), vec2(-0.015, -0.045), loop_phase, 0.44, 0.0065);
            float m3 = mirror_mote(UV, vec2(0.66, 0.82), vec2(0.025, -0.055), loop_phase, 0.73, 0.0070);
            float m4 = mirror_mote(UV, vec2(0.79, 0.66), vec2(-0.01, -0.04), loop_phase, 0.28, 0.0060);
            float silver_motes = (m1 + m3) * glass_mask * (0.45 + 0.55 * dark_glass);
            float crimson_motes = (m2 + m4) * glass_mask * (0.45 + 0.55 * dark_glass);
            float mote_gain = mix(0.24, 0.42, inspect_weight) * effect_strength * motion_scale;
            col += (cool_silver * silver_motes + pomegranate * crimson_motes) * mote_gain;
        }

        COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), orig.a);
    }
}
