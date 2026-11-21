shader_type canvas_item;
render_mode unshaded;

uniform float drift = 0.0;
uniform float pressure = 0.0;
uniform float eyes = 0.0;
varying vec4 tint;

void vertex() {
    tint = COLOR;
}

void fragment() {
    vec2 p = UV;
    float head = 1.0 - smoothstep(0.42, 0.61, p.y);
    float cloth = smoothstep(0.48, 0.70, p.y) * smoothstep(0.12, 0.42, abs(p.x - 0.5));
    p.x += drift * (0.006 * head - 0.008 * cloth) + pressure * 0.005 * head;
    p.y -= drift * (p.x - 0.54) * 0.017 * head + cloth * 0.004 * pressure;
    vec4 painted = texture(TEXTURE, clamp(p, vec2(0.001), vec2(0.999)));
    vec2 left = (p - vec2(0.473, 0.316)) * vec2(57.0, 158.0);
    vec2 right = (p - vec2(0.591, 0.293)) * vec2(57.0, 158.0);
    float irises = exp(-dot(left,left)) + exp(-dot(right,right));
    float bloom = exp(-dot(left,left) * 0.26) + exp(-dot(right,right) * 0.26);
    vec3 light = vec3(0.98, 0.064, 0.205) * eyes * (0.49 * irises + 0.13 * bloom);
    painted.a *= 1.0 - smoothstep(0.77, 0.994, UV.y);
    COLOR = vec4(painted.rgb + light, painted.a) * tint;
}
