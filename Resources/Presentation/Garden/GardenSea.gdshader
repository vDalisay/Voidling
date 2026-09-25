// The sea around the Garden island.
//
// Built on the premium Sprout Lands water tile, then given what the pack's four still frames cannot
// have: depth from the island's own distance field (bright shallows over a sandy bottom falling away
// to deep teal), shore waves that roll in and break on the cliff foot, caustics, glints that turn
// into star twinkles at night, dawn mist, rain rings, and the Voidlings' reflections, read from a
// separate buffer and broken up by the ripples. Everything snaps to whole world pixels so it keeps
// the pixel-art look at any zoom.
shader_type canvas_item;

uniform sampler2D water_tile : repeat_enable, filter_nearest;
uniform sampler2D island_field : filter_linear, repeat_disable;
/// World rectangle the field covers: x, y, width, height.
uniform vec4 field_rect = vec4(0.0, 0.0, 1.0, 1.0);
/// World pixels the field's full range stands for either side of the coast.
uniform float field_range = 128.0;

uniform sampler2D reflections : filter_nearest, repeat_disable;
uniform float reflection_strength = 0.62;

uniform vec4 shallow_color : source_color = vec4(0.58, 0.87, 0.80, 1.0);
uniform vec4 mid_color : source_color = vec4(0.36, 0.70, 0.73, 1.0);
uniform vec4 deep_color : source_color = vec4(0.22, 0.49, 0.60, 1.0);
uniform vec4 sand_color : source_color = vec4(0.84, 0.80, 0.58, 1.0);
uniform vec4 foam_color : source_color = vec4(0.96, 0.99, 1.0, 1.0);
uniform vec4 glint_color : source_color = vec4(1.0, 1.0, 0.94, 1.0);
uniform vec4 star_color : source_color = vec4(0.86, 0.92, 1.0, 1.0);

/// 0 by day, 1 at night: glints become star twinkles and the deep water darkens.
uniform float night = 0.0;
uniform float rain = 0.0;
uniform float mist = 0.0;
/// 0 calm to 1 storm: faster swell, more foam.
uniform float wind = 0.25;

varying vec2 world;

void vertex() {
	world = VERTEX;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float coast_distance(vec2 at) {
	vec2 uv = (at - field_rect.xy) / field_rect.zw;
	if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) {
		return field_range;
	}
	return (texture(island_field, uv).r - 0.5) * 2.0 * field_range;
}

void fragment() {
	vec2 p = floor(world);
	float t = TIME;
	float d = max(coast_distance(p + 0.5), 0.0);

	// Depth: sandy shallows, turquoise, then deep water further out.
	vec3 color = mix(shallow_color.rgb, mid_color.rgb, smoothstep(6.0, 42.0, d));
	color = mix(color, deep_color.rgb, smoothstep(42.0, 128.0, d));
	color = mix(color, sand_color.rgb, (1.0 - smoothstep(1.0, 7.0, d)) * 0.35);
	color = mix(color, deep_color.rgb * vec3(0.72, 0.80, 0.95), night * 0.35 * smoothstep(20.0, 110.0, d));

	// The premium tile's own ripple pattern, in two layers drifting against each other.
	float swell = 1.0 + wind * 1.6;
	vec3 tile_a = texture(water_tile, (p + vec2(t * 3.0, t * 5.0) * swell) / 16.0).rgb;
	vec3 tile_b = texture(water_tile, (p.yx * vec2(1.0, -1.0) + vec2(-t * 2.0, t * 1.5) * swell) / 32.0).rgb;
	float detail = (dot(tile_a, vec3(0.333)) + dot(tile_b, vec3(0.333))) * 0.5 - 0.74;
	color += detail * (0.55 + 0.25 * smoothstep(20.0, 90.0, d));

	// Caustics playing over the sandy shallows.
	float c1 = sin(p.x * 0.21 + t * 1.3 + sin(p.y * 0.13 + t * 0.8) * 2.0);
	float c2 = sin(p.y * 0.19 - t * 1.1 + sin(p.x * 0.09 - t * 0.6) * 2.0);
	float shallows = 1.0 - smoothstep(8.0, 46.0, d);
	color += step(0.74, c1 * c2) * 0.10 * shallows * (1.0 - night * 0.7);

	// Reflections of whatever stands on the shore, read back through the ripples. Rows break up
	// where the swell crests, the way a reflection does on moving water.
	float world_per_screen = max(abs(dFdx(world.x)), 1e-4);
	float wobble = round(sin(world.y * 0.85 + t * 2.4) * (1.0 + wind) + sin(world.y * 0.31 - t * 1.2) * 0.6);
	vec2 reflect_uv = SCREEN_UV + vec2(wobble / world_per_screen * SCREEN_PIXEL_SIZE.x, 0.0);
	vec4 mirrored = textureLod(reflections, reflect_uv, 0.0);
	float crest = step(0.82, sin(p.y * 1.6 + t * 2.6 + sin(p.x * 0.06 + t * 0.4) * 2.0));
	float reach = 1.0 - smoothstep(4.0, 72.0, d);
	float reflect_amount = mirrored.a * reflection_strength * reach * (1.0 - crest * 0.65) * (1.0 - rain * 0.45);
	color = mix(color, mirrored.rgb * mix(vec3(0.80, 0.92, 0.95), vec3(0.62, 0.72, 0.92), night), reflect_amount);

	// Shore waves: foam rings rolling in towards the coast, broken along their length.
	float roll = d / 13.0 + t * (0.30 + wind * 0.25);
	float ring = fract(roll);
	float broken = step(0.52 - wind * 0.22, noise(p * 0.06 + vec2(floor(roll) * 3.1, t * 0.05)));
	float wave = (1.0 - step(0.075, ring)) * broken * (1.0 - smoothstep(10.0, 46.0, d)) * step(3.0, d);
	color = mix(color, foam_color.rgb, wave * 0.75);

	// Lapping foam hugging the coast, with spray just beyond it.
	float lap = 0.8 + 0.9 * sin(t * 1.7 + p.x * 0.06 + p.y * 0.05) + 0.6 * sin(t * 0.9 - p.x * 0.03) + wind * 1.2;
	float foam = 1.0 - step(lap, d);
	float spray = (1.0 - step(lap + 2.5, d)) * step(0.70, hash(floor(p / 2.0) + floor(t * 5.0)));
	color = mix(color, foam_color.rgb, max(foam * 0.95, spray * 0.6));
	// Wet shadow just outside the foam.
	color *= 1.0 - (1.0 - step(lap + 5.0, d)) * step(lap + 2.0, d) * 0.08;

	// Glints by day: short flashes of sun along the swell. Star twinkles by night: little crosses.
	vec2 glint_at = mod(p, 11.0);
	float cell = hash(floor(p / 11.0) + floor(vec2(t * 0.35, t * 0.2)));
	float lit = step(0.984 - night * 0.014, cell) * step(0.5, sin(t * (2.5 + night * 1.5) + cell * 71.0));
	float flash = step(abs(glint_at.y - 5.0), 0.1) * step(abs(glint_at.x - 5.0), 1.1);
	float center = step(distance(glint_at, vec2(5.0)), 0.1);
	float arms = step(abs(glint_at.x - 5.0) + abs(glint_at.y - 5.0), 1.1) - center;
	float shape = mix(flash * 0.85, center * 0.95 + arms * 0.40, night);
	float glint = lit * shape * smoothstep(10.0, 40.0, d) * (1.0 - rain * 0.8);
	color = mix(color, mix(glint_color.rgb, star_color.rgb, night), glint);

	// Rain: rings spreading from where drops land.
	if (rain > 0.001) {
		vec2 cell_size = vec2(19.0, 13.0);
		vec2 id = floor(p / cell_size);
		float seed = hash(id);
		float period = 0.9 + seed * 0.8;
		float life = fract(t / period + seed * 13.0);
		vec2 drop = id * cell_size + vec2(2.0 + seed * 8.0, 2.0 + fract(seed * 7.3) * 5.0);
		vec2 offset = (p - drop) * vec2(1.0, 1.8);
		float radius = life * 4.0;
		float edge = abs(length(offset) - radius);
		float shows = step(seed, rain * 0.55);
		float ripple = (1.0 - step(0.7, edge)) * (1.0 - life * life) * shows * step(0.5, d);
		color = mix(color, foam_color.rgb, ripple * 0.45);
	}

	// Dawn mist lying on the water: a fine haze in three dithered steps, thicker away from the island.
	if (mist > 0.001) {
		vec2 drift = vec2(t * 2.0, t * 0.6);
		float fog = noise((p + drift) * 0.011) * 0.55 + noise((p - drift) * 0.034) * 0.30 + noise(p * 0.09 + drift * 0.05) * 0.15;
		float amount = smoothstep(0.30, 0.80, fog) * mist * (0.20 + 0.25 * smoothstep(0.0, 90.0, d));
		float bayer = mod(p.x, 2.0) * 0.5 + mod(p.y, 2.0) * 0.25 + 0.125;
		float stepped = floor(amount * 3.0 / 0.45 + bayer) / (3.0 / 0.45);
		color = mix(color, vec3(0.92, 0.94, 0.95), clamp(stepped, 0.0, 0.45));
	}

	COLOR = vec4(color, 1.0);
}
