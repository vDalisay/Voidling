// Soft cloud shadows drifting across the course.
//
// Drawn over the whole view from the camera, but sampled in world space so the shadows stay on the
// ground while the camera moves. Quantised to two steps with an ordered dither so they sit in the
// pixel-art look instead of reading as a smooth blur.
shader_type canvas_item;

uniform vec4 shadow_color : source_color = vec4(0.12, 0.18, 0.22, 1.0);
uniform float strength = 0.11;
uniform vec2 drift = vec2(9.0, 2.5);
uniform float scale = 150.0;

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(41.3, 289.1))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 p = floor(world);
	vec2 q = (p - drift * TIME) / scale;
	float n = noise(q) * 0.62 + noise(q * 2.1 + 7.3) * 0.28 + noise(q * 4.3 + 1.7) * 0.10;
	float cloud = smoothstep(0.54, 0.70, n);

	// 2x2 ordered dither on the soft edge.
	float bayer = mod(p.x + p.y * 2.0, 4.0) / 4.0 + 0.125;
	float shade = cloud > bayer * 0.9 + 0.05 ? 1.0 : 0.0;
	shade = max(shade * 0.6, step(0.85, cloud));

	COLOR = vec4(shadow_color.rgb, shade * strength);
}
