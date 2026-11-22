shader_type canvas_item;
render_mode unshaded;

uniform sampler2D expression_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform vec4 expression_box = vec4(0.501196, 0.162593, 0.260167, 0.326249);
uniform float portrait_aspect = 1.7768332;
uniform vec3 acting = vec3(0.0);
uniform float smile = 0.0;
uniform float tilt = 0.0;
uniform float breath = 0.0;
uniform float lean = 0.0;
uniform float defocus = 0.0;
uniform float eyes = 0.0;
uniform float silver = 0.0;
uniform float erosion = 0.0;
varying vec4 tint;

void vertex() {
    tint = COLOR;
}

float fill_oval(vec2 uv, vec2 center, vec2 radius) {
    vec2 d = (uv - center) / radius;
    return 1.0 - smoothstep(0.30, 1.0, dot(d,d));
}

float grain(vec2 p) {
    vec2 cell = floor(p);
    vec2 u = fract(p);
    u = u * u * (3.0 - 2.0 * u);
    vec4 n = fract(sin(vec4(0.0, 41.53, 29.17, 70.70) + dot(cell, vec2(41.53, 29.17))) * 43758.5453);
    return mix(mix(n.x, n.y, u.x), mix(n.z, n.w, u.x), u.y);
}

void fragment() {
    vec2 uv = UV;
    vec2 pivot = vec2(0.636, 0.369);
    float head = 1.0 - smoothstep(0.448, 0.65, uv.y);
    float angle = tilt * head;
    vec2 delta = uv - pivot;
    delta.x *= portrait_aspect;
    vec2 turned = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle))) * delta;
    vec2 p = pivot + turned * vec2(1.0 / portrait_aspect, 1.0);
    p.y += (0.007 - acting.x * 0.009) * head;
    float shoulder = smoothstep(0.36, 0.55, uv.y) * (1.0 - smoothstep(0.59, 0.88, uv.y));
    p.y += shoulder * (0.005 * breath + 0.008 * lean);
    p.x -= (p.x - 0.5) * shoulder * lean * 0.042;

    vec2 eye_l = vec2(0.571, 0.328);
    vec2 eye_r = vec2(0.676, 0.260);
    vec2 mouth = vec2(0.656, 0.389);
    float face_l = fill_oval(p, eye_l, vec2(0.051, 0.041));
    float face_r = fill_oval(p, eye_r, vec2(0.057, 0.040));
    float eye_matte = max(face_l, face_r);
    float lip_matte = fill_oval(p, mouth, vec2(0.066, 0.075));
    vec2 base_uv = p;
    base_uv.x += (1.0 - acting.x) * 0.004 * eye_matte;
    base_uv.y += (0.003 - 0.006 * acting.x) * eye_matte;
    base_uv.y += (p.y - mix(eye_l.y, eye_r.y, step(0.623, p.x))) * (0.20 * acting.y * eye_matte);
    base_uv.y += lip_matte * smile * clamp((p.x - mouth.x) * 0.16 + 0.002, 0.0, 0.010);
    vec4 painted = textureLod(TEXTURE, clamp(base_uv, vec2(0.002), vec2(0.998)), defocus);

    float brow = fill_oval(p, eye_r - vec2(0.002, 0.033), vec2(0.044, 0.027));
    float performance = max(max(eye_matte * acting.y, brow * acting.z), lip_matte * smile);
    vec2 registration = mix(vec2(-0.002, -0.002), vec2(-0.003, -0.001), smoothstep(0.60, 0.65, p.x));
    registration = mix(registration, vec2(-0.007, -0.010), smoothstep(0.344, 0.387, p.y) * lip_matte);
    vec2 key_uv = clamp((p + registration - expression_box.xy) / expression_box.zw, vec2(0.002), vec2(0.998));
    vec4 keyframe = textureLod(expression_tex, key_uv, defocus);
    painted.rgb = mix(painted.rgb, keyframe.rgb, performance * keyframe.a * 0.96);

    vec2 iris_l = (p - eye_l - vec2(0.005 + .002 * acting.x, 0.001)) * vec2(92.0, 154.0);
    vec2 iris_r = (p - eye_r - vec2(0.001 + .002 * acting.x, 0.001)) * vec2(92.0, 154.0);
    float glow = exp(-dot(iris_l,iris_l)) + exp(-dot(iris_r,iris_r));
    painted.rgb += vec3(0.62, 0.048, 0.129) * eyes * glow;
    float veil = smoothstep(.805, 0.998, uv.y);
    float frameEdge = smoothstep(0.0, 0.049, uv.y) * (1.0 - smoothstep(0.985, 1.0, uv.x));
    painted.rgb = mix(painted.rgb, painted.rgb * vec3(0.65, 0.49, 0.80), veil * 0.53);
    painted.rgb += vec3(.16, .018, .054) * silver * painted.a;
    vec2 stroke_uv = vec2(p.x + p.y * 0.54, p.y - p.x * 0.31);
    float pigment = 2.0 * (0.58 * grain(stroke_uv * vec2(108.0, 47.0))
                       + 0.42 * grain(stroke_uv * vec2(62.0, 24.0))) - 1.0;
    float skin = fill_oval(p, vec2(0.635, 0.336), vec2(0.143, 0.215));
    float hold = clamp(0.20 + painted.a * 0.18 + skin * 0.53 + pigment * 0.11, 0.12, 0.96);
    float retained = smoothstep(-0.045, 0.040, hold - erosion);
    float rim = (1.0 - smoothstep(0.0, 0.085, abs(hold - erosion))) * step(0.02, erosion);
    painted.rgb = mix(painted.rgb, vec3(0.47, 0.16, 0.37), rim * 0.48);
    float afterimage = glow * eyes * 0.59 * smoothstep(0.61, 0.83, erosion)
                     * (1.0 - smoothstep(0.91, 1.0, erosion));
    COLOR = vec4(painted.rgb + vec3(afterimage, 0.0, afterimage * 0.28),
                 painted.a * (1.0 - veil) * frameEdge * max(retained, afterimage)) * tint;
}
