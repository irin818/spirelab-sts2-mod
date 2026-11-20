shader_type canvas_item;

uniform vec4 trim : source_color = vec4(0.74, 0.72, 0.72, 1.0);

void fragment() {
    // Use the native frame's engraved relief and alpha with the Witch's ink-to-metal palette.
    float relief = dot(COLOR.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 ink = vec3(0.085, 0.066, 0.083);
    COLOR.rgb = mix(ink, trim.rgb, smoothstep(0.07, 0.93, relief));
}
