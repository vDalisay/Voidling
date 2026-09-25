// A rain puddle.
//
// The premium water-object sheet ships its puddles as flat blue shapes; this uses a shape only as a
// mask and fills it with water that reflects the sky, a darker wet rim, and rings where drops land,
// fading in and out with how wet the Garden is.
shader_type canvas_item;

uniform float wetness = 0.0;
uniform float rain = 0.0;
uniform vec4 sky_near : source_color = vec4(0.52, 0.64, 0.74, 1.0);
uniform vec4 sky_far : source_color = vec4(0.80, 0.88, 0.93, 1.0);
uniform vec4 rim_color : source_color = vec4(0.30, 0.36, 0.30, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec2 px = TEXTURE_PIXEL_SIZE;
	float inside = step(0.5, texture(TEXTURE, UV).a);
	float edge = inside * (1.0 - step(0.5, texture(TEXTURE, UV + vec2(px.x, 0.0)).a)
		* step(0.5, texture(TEXTURE, UV - vec2(px.x, 0.0)).a)
		* step(0.5, texture(TEXTURE, UV + vec2(0.0, px.y)).a)
		* step(0.5, texture(TEXTURE, UV - vec2(0.0, px.y)).a));

	vec2 local = floor(UV / px);
	vec3 color = mix(sky_near.rgb, sky_far.rgb, fract(UV.y / px.y / 16.0));
	color = mix(color, sky_far.rgb, step(0.9, hash(local + floor(TIME * 0.5))) * 0.3);
	color = mix(color, rim_color.rgb, edge * 0.55);

	// Drops landing.
	float seed = hash(floor(local / 6.0));
	float life = fract(TIME * 1.3 + seed * 9.0);
	vec2 drop = floor(local / 6.0) * 6.0 + 3.0;
	float ring = abs(length((local - drop) * vec2(1.0, 1.6)) - life * 3.5);
	color = mix(color, vec3(0.94, 0.97, 1.0), (1.0 - step(0.6, ring)) * (1.0 - life) * step(seed, rain) * 0.7);

	COLOR = vec4(color, inside * wetness * 0.9);
}
