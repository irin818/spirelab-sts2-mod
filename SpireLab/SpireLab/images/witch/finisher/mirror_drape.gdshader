shader_type canvas_item;
render_mode unshaded;

uniform float aperture = 0.0;
uniform float curtain = 0.0;
uniform float fade = 1.0;
uniform float elapsed = 0.0;
uniform float impact = 0.0;
uniform float contraction = 0.0;
uniform vec2 parallax = vec2(0.0);
uniform float depth_zoom = 1.0;
uniform float defocus = 0.0;
uniform float smile_wave = 0.0;

void fragment() {
    vec2 uv = UV;
    float fold = sin(uv.y * 22.0 + elapsed * 4.0) * 0.007
               + sin(uv.y * 8.0 - elapsed * 2.0) * 0.012;
    float edge = abs(uv.x - 0.5 + fold * curtain) - aperture;
    float revealed = (1.0 - smoothstep(-0.026, 0.006, edge)) * smoothstep(0.0, 0.026, aperture);
    float rim = exp(-abs(edge) * 215.0);
    float sheen = exp(-abs(edge) * 48.0) * (0.55 + 0.45 * sin(uv.y * 47.0 + elapsed * 10.0));
    float aperture_glow = (0.67 * rim + 0.25 * sheen) * curtain;
    float safe_band = smoothstep(0.034, 0.155, uv.y) * (1.0 - smoothstep(0.745, 0.955, uv.y));
    vec2 backdrop = (uv - 0.5) / depth_zoom + 0.5 + parallax;
    backdrop.x += fold * curtain * exp(-abs(edge) * 17.0) * 0.76;
    vec2 axis = backdrop - vec2(0.5, 0.56);
    float inhale = sin(elapsed * 3.0 - length(axis) * 9.0) * (0.007 + smile_wave * 0.018);
    backdrop += axis * (inhale + contraction * 0.29 - impact * 0.048);
    backdrop.x += axis.y * (abs(axis.x) * 0.15 * contraction + 0.009 * sin(elapsed * 1.8));
    // Fold the painted edges back into the gallery during the strongest pull.
    // Clamping reuses a single edge pixel and leaves horizontal streaks.
    vec2 folded = 1.0 - abs(mod(backdrop, 2.0) - 1.0);
    vec3 stage = textureLod(TEXTURE, clamp(folded, vec2(0.002), vec2(0.998)), defocus).rgb;
    vec3 silver = vec3(0.79, 0.72, 0.93);
    vec3 lit = mix(stage, silver, aperture_glow);
    float wash = exp(-length((uv - vec2(0.5, 0.46)) * vec2(2.0, 1.25)) * 3.6) * impact;
    float centre = exp(-length((uv - vec2(0.5, 0.5)) * vec2(2.0, 0.86)) * 7.5);
    vec3 coloured = mix(lit, vec3(0.86, 0.14, 0.29), wash * 0.53);
    float pull = exp(-abs(uv.y - 0.50 - (uv.x - 0.60) * 0.10) * 14.0);
    float breath = exp(-pow((length(axis * vec2(1.22, 0.95)) - (0.48 - contraction * 0.30)) * 9.0, 2.0));
    coloured *= 1.0 - smile_wave * 0.14;
    coloured += vec3(0.21, 0.023, 0.086) * breath * (0.19 * smile_wave + contraction * 0.67);
    coloured += vec3(0.26, 0.031, 0.085) * (centre + pull * 0.25) * contraction;
    float haze = 0.5 + 0.5 * sin(9.5 * uv.y + cos(uv.x * 8.3) - elapsed * 1.5);
    float veil = exp(-pow(abs(uv.y - 0.54) * 4.0, 2.0));
    coloured += (0.16 + smile_wave * 0.56) * haze * veil * vec3(0.13, 0.032, 0.11);
    COLOR = vec4(coloured,
                 max(revealed * 0.99, aperture_glow) * safe_band * fade);
}
