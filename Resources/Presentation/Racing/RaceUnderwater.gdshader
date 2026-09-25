// The submerged half of a swimming Voidling.
//
// Drawn over the body below the waterline and reading back what is already on screen, so it works
// for every body, palette and accessory layer without knowing about any of them: the body below the
// surface is refracted, pulled towards the water colour and lit by moving caustics, and fades out
// softly at the sides so the patch never reads as a box laid on the river.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform vec4 water_tint : source_color = vec4(0.36, 0.66, 0.68, 1.0);
uniform vec4 caustic_color : source_color = vec4(0.86, 1.0, 0.97, 1.0);
/// Polygon half-width and depth in its own local units, for the soft mask.
uniform float half_width = 13.0;
uniform float depth = 30.0;
uniform float phase = 0.0;

varying vec2 local;

void vertex() {
	local = VERTEX;
}

void fragment() {
	float t = TIME + phase;
	float below = clamp(local.y / depth, 0.0, 1.0);

	// Refraction: rows slide sideways by a pixel or two, more the deeper they are.
	float wobble = sin(local.y * 0.85 + t * 5.5) * (0.6 + below * 1.4);
	vec2 offset = vec2(wobble * SCREEN_PIXEL_SIZE.x, 0.0);
	vec3 under = textureLod(screen_tex, SCREEN_UV + offset, 0.0).rgb;

	// Murkier with depth: the body is still readable just under the surface and all but gone
	// by the time it reaches the feet.
	vec3 color = mix(under, water_tint.rgb, 0.52 + below * 0.44);
	// A darker band a few pixels down, the shadow the surface throws.
	color *= 1.0 - step(3.0, local.y) * (1.0 - step(7.0, local.y)) * 0.08;

	// Caustic ripples playing over the submerged body.
	float c = sin(local.x * 0.9 + t * 2.3 + sin(local.y * 0.7 - t * 1.7) * 1.6);
	color = mix(color, caustic_color.rgb, step(0.84, c) * (1.0 - below) * 0.35);

	// Soft sides and a soft bottom so the edge dissolves into the river.
	float side = 1.0 - smoothstep(half_width * 0.55, half_width, abs(local.x));
	float bottom = 1.0 - smoothstep(depth * 0.6, depth, local.y);
	float alpha = side * bottom;

	// A lighter band right under the surface: light scattering at the waterline.
	color = mix(color, caustic_color.rgb, (1.0 - step(2.0, local.y)) * 0.25);

	COLOR = vec4(color, alpha * 0.92);
}
