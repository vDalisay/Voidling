// Low-sun light shafts at the golden hours.
//
// Soft diagonal bands of warm light added over the Garden, anchored in world space so they stay put
// while the camera moves, stepped and dithered so they read as pixel-art light rather than a blur.
shader_type canvas_item;
render_mode blend_add, unshaded;

uniform vec4 light_color : source_color = vec4(1.0, 0.78, 0.46, 1.0);
uniform float strength = 0.0;

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec2 p = floor(world);
	float along = p.x * 0.72 + p.y * 0.69;
	float bands = sin(along * 0.018 + TIME * 0.05) * 0.6 + sin(along * 0.043 - TIME * 0.08) * 0.4;
	float shaft = smoothstep(0.25, 0.9, bands);
	float level = floor(shaft * 3.0 + fract((p.x + p.y * 2.0) * 0.25) * 0.99) / 3.0;
	COLOR = vec4(light_color.rgb * level * strength, 1.0);
}
