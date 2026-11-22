shader_type canvas_item;
render_mode unshaded;

// Pixel-locked Live2D-style rig weight mask (391x438, fix_alpha_border=false)
// R = head + wide-brim hat rigid pivot weight
// G = flowing back hair, bent hat tip + ruby pendant, lower ribbons/crosses
// B = layered gothic skirt & ivory lace hem (strictly zero on legs & boots)
// A = crimson eyes & ruby gems glow mask
uniform sampler2D rig_mask_tex : filter_linear, repeat_disable;

uniform float time_sec = 0.0;
uniform float breath = 0.0;         // [-1, 1] organic chest breath
uniform float body_lean = 0.0;      // [-1, 1] spring-driven forward/back posture lean
uniform float head_tilt = 0.0;      // radians (small, e.g. [-0.045, 0.045])
uniform float head_nod = 0.0;       // UV-normalized vertical nod (e.g. [-0.004, 0.004])
uniform float hair_lag = 0.0;       // spring inertia lag for back hair & hat tip
uniform float skirt_lag = 0.0;      // spring inertia lag for gothic skirt hem
uniform float blink = 0.0;          // [0, 1] upper eyelid closure
uniform float eye_glow = 0.15;      // [0, 1.5] crimson iris & ruby luminance
uniform float cast_surge = 0.0;     // [0, 1] spell-cast arcane resonance
uniform float motion_scale = 1.0;   // 1.0 = Full, 0.4 = Reduced, 0.0 = Static
uniform vec3 rim_color = vec3(0.812, 0.282, 0.416); // #CF486A default crimson
uniform float rim_strength = 0.0;   // [0, 1] temporary cast/block/counterpoint rim light

varying vec4 v_tint;

void vertex() {
    v_tint = COLOR;
}

float eye_aperture(vec2 uv, vec2 center, vec2 radii, float angle, out float local_y) {
    vec2 d = uv - center;
    float ca = cos(angle);
    float sa = sin(angle);
    vec2 rot = vec2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
    local_y = rot.y / radii.y; // -1 at top lash, +1 at bottom lid
    vec2 n = rot / radii;
    return 1.0 - smoothstep(0.65, 1.05, dot(n, n));
}

void fragment() {
    vec2 uv = UV;
    // Aspect ratio of witch_sprite.png = 391 / 438 = 0.892694
    const float ASPECT = 0.892694;

    // Sample rig weights at base UV for smooth, tear-free inverse lookup
    vec4 w = texture(rig_mask_tex, uv);
    float head_w = w.r;
    float hair_w = w.g;
    float skirt_w = w.b;

    // 1. Floor-anchored spine height factor: 0.0 at boots (y >= 0.855), 1.0 at upper torso/head
    float spine_h = smoothstep(0.855, 0.26, uv.y);

    // 2. Inverse kinematic offset (subtract offset to move visual content forward)
    vec2 p = uv;

    // Subtle ankle-pivoted posture lean & attack/hit inertia (boots stay glued!)
    float lean_rad = body_lean * 0.042 * motion_scale * spine_h;
    vec2 ankle_pivot = vec2(0.515, 0.895);
    vec2 da = p - ankle_pivot;
    da.x *= ASPECT;
    float cl = cos(-lean_rad);
    float sl = sin(-lean_rad);
    vec2 turned_body = vec2(da.x * cl - da.y * sl, da.x * sl + da.y * cl);
    p = ankle_pivot + vec2(turned_body.x / ASPECT, turned_body.y);

    // Organic ribcage breathing lift + gentle chest expansion around corset/grimoire (y ~ 0.34..0.52)
    float torso_lift = smoothstep(0.76, 0.28, uv.y);
    p.y += breath * 0.0052 * motion_scale * torso_lift;
    float chest_band = smoothstep(0.54, 0.40, uv.y) * smoothstep(0.24, 0.35, uv.y);
    p.x -= (p.x - 0.54) * breath * 0.011 * motion_scale * chest_band;

    // 3. Neck-pivoted rigid head & wide-brim hat tilt + nod (zero facial distortion!)
    vec2 neck_pivot = vec2(0.542, 0.279);
    float h_angle = -head_tilt * motion_scale * head_w;
    vec2 dh = p - neck_pivot;
    dh.x *= ASPECT;
    float ch = cos(h_angle);
    float sh = sin(h_angle);
    vec2 turned_head = vec2(dh.x * ch - dh.y * sh, dh.x * sh + dh.y * ch);
    p = neck_pivot + vec2(turned_head.x / ASPECT, turned_head.y);
    p.y += head_nod * motion_scale * head_w;

    // 4. Secondary Live2D physics: flowing back hair curls + bent hat tip & ruby charm + dangling crosses
    float wave_hair = sin(time_sec * 1.95 - uv.y * 10.5 + uv.x * 4.0) * 0.65
                    + sin(time_sec * 3.10 - uv.y * 16.0) * 0.35;
    float hair_dx = (wave_hair * 0.0068 + hair_lag * 0.0145) * motion_scale * hair_w;
    float hair_dy = (cos(time_sec * 1.95 - uv.y * 9.0) * 0.0022 + abs(hair_lag) * 0.0032) * motion_scale * hair_w;
    p.x -= hair_dx;
    p.y -= hair_dy;

    // 5. Layered gothic skirt & ivory lace hem cloth ripple (strictly zero on legs & boots)
    float wave_skirt = sin(time_sec * 1.65 - uv.y * 12.0 + uv.x * 7.5) * 0.68
                     + sin(time_sec * 2.75 + uv.x * 13.0) * 0.32;
    float skirt_dx = (wave_skirt * 0.0052 + skirt_lag * 0.0125) * motion_scale * skirt_w;
    float skirt_dy = (sin(time_sec * 1.65 + uv.x * 6.0) * 0.0016) * motion_scale * skirt_w;
    p.x -= skirt_dx;
    p.y -= skirt_dy;

    // Sample original untouched witch_sprite.png + re-sample glow mask at articulated coords
    vec4 tex = texture(TEXTURE, clamp(p, vec2(0.002), vec2(0.998)));
    vec4 w_art = texture(rig_mask_tex, clamp(p, vec2(0.002), vec2(0.998)));

    // 6. Natural Live2D upper-eyelid blink directly on exact articulated eye coordinates
    float blink_cover = 0.0;
    if (blink > 0.001 && tex.a > 0.05) {
        float ly1 = 0.0;
        float ly2 = 0.0;
        // Exact pixel centers from witch_sprite.png: left eye (211, 100.5), right eye (226.5, 95)
        float ap1 = eye_aperture(p, vec2(0.5396, 0.2295), vec2(0.0175, 0.0092), -0.17, ly1);
        float ap2 = eye_aperture(p, vec2(0.5793, 0.2169), vec2(0.0145, 0.0085), -0.17, ly2);
        float ap = max(ap1, ap2);
        float ly = ap1 >= ap2 ? ly1 : ly2;

        // Upper lid edge descends from ly = -1.15 (open) to ly = +1.05 (closed)
        float lid_edge = mix(-1.15, 1.05, clamp(blink, 0.0, 1.0));
        float upper_lid = (1.0 - smoothstep(lid_edge - 0.22, lid_edge + 0.14, ly)) * ap;
        float lash_line = exp(-pow((ly - lid_edge) / 0.20, 2.0)) * ap * smoothstep(0.04, 0.22, blink);

        // Soft rose-ivory eyelid matching her exact cheek tone + charcoal-plum gothic lash line
        vec3 skin_lid = vec3(0.905, 0.798, 0.822) * (0.93 + 0.07 * smoothstep(-1.0, 0.8, ly));
        vec3 lash_col = vec3(0.145, 0.102, 0.138);

        tex.rgb = mix(tex.rgb, skin_lid, upper_lid * 0.94);
        tex.rgb = mix(tex.rgb, lash_col, lash_line * 0.92);
        blink_cover = upper_lid;
    }

    // 7. Crimson iris & ruby gems luminance (hat ruby charm, hat brooch, eyes, grimoire gem)
    float is_eye_band = smoothstep(0.195, 0.205, p.y) * (1.0 - smoothstep(0.245, 0.258, p.y));
    float gem_mask = w_art.a * (1.0 - blink_cover * is_eye_band);
    float idle_gem_pulse = 0.5 + 0.5 * sin(time_sec * 2.1 + p.y * 5.0);
    float gem_power = (eye_glow * (0.32 + 0.22 * idle_gem_pulse) + cast_surge * 0.65) * gem_mask;
    vec3 ruby_glow = vec3(0.812, 0.282, 0.416); // #CF486A
    tex.rgb += ruby_glow * gem_power * 0.55 * tex.a;

    // 8. Subtle arcane rim sheen during spell cast / block / counterpoint / desire
    if (rim_strength > 0.002 && tex.a > 0.05) {
        // Estimate silhouette edge cheaply via 4-tap alpha gradient
        vec2 px = TEXTURE_PIXEL_SIZE * 2.4;
        float a_min = min(
            min(texture(TEXTURE, clamp(p + vec2(px.x, 0.0), vec2(0.002), vec2(0.998))).a,
                texture(TEXTURE, clamp(p - vec2(px.x, 0.0), vec2(0.002), vec2(0.998))).a),
            min(texture(TEXTURE, clamp(p + vec2(0.0, px.y), vec2(0.002), vec2(0.998))).a,
                texture(TEXTURE, clamp(p - vec2(0.0, px.y), vec2(0.002), vec2(0.998))).a)
        );
        float edge = clamp((tex.a - a_min) * 1.45, 0.0, 1.0);
        tex.rgb += rim_color * edge * rim_strength * 0.62;
    }

    COLOR = tex * v_tint;
}
