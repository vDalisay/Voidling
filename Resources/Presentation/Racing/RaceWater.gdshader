// Race water: a river crossing the course, or the lake under the glide.
//
// Built on the premium Sprout Lands water tile so the colour and texture match the Garden, then
// animated here because the pack only ships four static frames. Everything is snapped to whole
// world pixels so the motion stays crisp at the race's pixel-art scale.
shader_type canvas_item;

uniform sampler2D water_tile : repeat_enable, filter_nearest;
uniform vec4 shallow_tint : source_color = vec4(0.62, 0.86, 0.80, 1.0);
uniform vec4 deep_tint : source_color = vec4(0.34, 0.62, 0.66, 1.0);
uniform vec4 foam_color : source_color = vec4(0.95, 0.99, 1.0, 1.0);
uniform vec4 glint_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
/// World pixels per second the surface drifts. A river runs down the screen; the lake barely moves.
uniform vec2 flow = vec2(0.0, 16.0);
/// World X of the two banks, for depth shading and shoreline foam.
uniform float bank_left = 0.0;
uniform float bank_right = 256.0;
/// 0 for a lake whose far side is out of frame, 1 for a river with foam on both shores.
uniform float two_banks = 1.0;

varying vec2 world;

void vertex() {
	world = VERTEX;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec2 p = floor(world);
	float t = TIME;

	// Two layers of the premium tile sliding at different speeds give the surface depth.
	vec3 base = texture(water_tile, (p + flow * t) / 16.0).rgb;
	vec3 slow = texture(water_tile, (p.yx * vec2(1.0, -1.0) + flow.yx * t * 0.45) / 32.0).rgb;
	vec3 color = mix(base, slow, 0.35) * shallow_tint.rgb / vec3(0.62, 0.86, 0.80);

	// Deeper in the middle of the channel.
	float width = max(1.0, bank_right - bank_left);
	float from_left = p.x - bank_left;
	float from_right = mix(1e5, bank_right - p.x, two_banks);
	float edge = min(from_left, from_right);
	float depth = clamp(edge / (min(width, 240.0) * 0.5), 0.0, 1.0);
	color = mix(color, deep_tint.rgb, smoothstep(0.08, 1.0, depth) * 0.55);

	// Caustic net: two interfering sine fields, thresholded into hard pixel-art highlights.
	float c1 = sin(p.x * 0.19 + t * 1.2 + sin(p.y * 0.11 + t * 0.7) * 2.0);
	float c2 = sin(p.y * 0.23 - t * 1.4 + sin(p.x * 0.07 - t * 0.5) * 2.0);
	float caustic = c1 * c2;
	color += step(0.78, caustic) * 0.09 * (0.4 + depth);
	color -= step(caustic, -0.86) * 0.04;

	// Current streaks drifting with the flow.
	vec2 streak_cell = floor((p - flow * t * 1.6) / vec2(22.0, 9.0));
	float streak = hash(streak_cell);
	vec2 in_cell = fract((p - flow * t * 1.6) / vec2(22.0, 9.0));
	if (streak > 0.86 && in_cell.y < 0.12 && in_cell.x < 0.45) {
		color = mix(color, foam_color.rgb, 0.45);
	}

	// Sparse single-pixel glints that blink in and out, each with a faint cross.
	vec2 glint_cell = floor(p / 7.0);
	vec2 glint_at = mod(p, 7.0);
	float cell = hash(glint_cell + floor(flow * t / 7.0));
	float lit = step(0.97, cell) * step(0.55, sin(t * 3.0 + cell * 60.0));
	float center = step(distance(glint_at, vec2(3.0)), 0.1);
	float arms = step(abs(glint_at.x - 3.0) + abs(glint_at.y - 3.0), 1.1) - center;
	color = mix(color, glint_color.rgb, lit * (center * 0.95 + arms * 0.45));

	// Shoreline foam that laps back and forth.
	float lap = 2.0 + 1.6 * sin(p.y * 0.21 + t * 2.6) + 0.8 * sin(p.y * 0.07 - t * 1.3);
	float foam = step(edge, lap);
	float spray = step(edge, lap + 3.0) * step(0.55, hash(floor(p / 2.0) + floor(t * 4.0)));
	color = mix(color, foam_color.rgb, max(foam * 0.92, spray * 0.55));
	// Wet shade just inside the foam line.
	color *= 1.0 - step(edge, lap + 5.0) * (1.0 - step(edge, lap + 3.0)) * 0.10;

	COLOR = vec4(color, 1.0);
}
