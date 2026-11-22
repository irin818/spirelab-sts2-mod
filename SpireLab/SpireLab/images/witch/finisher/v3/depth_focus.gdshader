shader_type canvas_item;
render_mode unshaded;

uniform float defocus : hint_range(0.0, 3.0) = 0.0;
uniform float erosion = 0.0;
varying vec4 ink;

void vertex() {
    ink = COLOR;
}

void fragment() {
    vec4 paint = textureLod(TEXTURE, UV, defocus);
    float stroke = 0.24 * sin(UV.x * 65.0 + sin(UV.y * 51.0) * 4.0) * sin(UV.y * 82.0);
    float held = smoothstep(-0.07, 0.05, 0.25 + 0.20 * paint.a + stroke - erosion);
    COLOR = vec4(paint.rgb, paint.a * held) * ink;
}
