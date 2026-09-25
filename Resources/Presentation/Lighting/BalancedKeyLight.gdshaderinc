// Pixel-art lighting for sprites that carry a normal map generated from their silhouette
// (Voidlings, trees, grass tufts, flowers and mushrooms).
//
// A surface facing the viewer is lit exactly like the flat ground around it, so a sprite keeps the
// colours its artist drew. Only its turned edges answer to where the light comes from: the side
// facing the light brightens and the far side falls into shade. The Garden takes the sun's share
// back out of its ambient light, which is what keeps "facing the viewer" equal to "lit like the
// ground" for the sun as well as for fireflies and halos.
//
// The answer is stepped into two flat levels each way, so light lands as crisp bands along the
// pixels, the way a pixel artist shades, rather than a smooth gradient that reads as 3D. Light is
// also measured once per texel (see texel_light_vertex), so a nearby light's falloff steps with the
// art's pixels instead of cutting through them.

// How strongly an edge's lean turns into light or shade before it is stepped.
const float LIGHT_SIDE_GAIN = 1.3;
// Levels each way: 2 gives a half step and a full step of light, and of shade.
const float LIGHT_SIDE_STEPS = 2.0;
// A full step, as a share of the light falling on the ground next to the sprite.
const float LIGHT_HIGHLIGHT = 0.4;
const float LIGHT_SHADE = 0.45;

// The light position of the centre of the texel this fragment falls in. fragment() assigns it to
// LIGHT_VERTEX so every screen pixel of one art pixel is lit the same.
vec3 texel_light_vertex(vec3 light_vertex, vec2 uv, vec2 texel_size) {
	vec2 to_centre = (floor(uv / texel_size) + 0.5) * texel_size - uv;
	mat2 uv_per_pixel = mat2(dFdx(uv), dFdy(uv));
	if (abs(determinant(uv_per_pixel)) < 1e-12) {
		return light_vertex;
	}
	mat2 vertex_per_pixel = mat2(dFdx(light_vertex.xy), dFdy(light_vertex.xy));
	return vec3(light_vertex.xy + vertex_per_pixel * (inverse(uv_per_pixel) * to_centre), light_vertex.z);
}

void light() {
	vec3 toward = normalize(LIGHT_DIRECTION);
	float side = clamp(dot(NORMAL.xy, toward.xy) * LIGHT_SIDE_GAIN, -1.0, 1.0);
	float level = round(side * LIGHT_SIDE_STEPS) / LIGHT_SIDE_STEPS;
	float response = 1.0 + level * (level > 0.0 ? LIGHT_HIGHLIGHT : LIGHT_SHADE);
	// LIGHT_COLOR already carries the light's energy (and a point light's texture); multiplying by
	// LIGHT_ENERGY again would square it and leave lit sprites darker than the ground.
	LIGHT = vec4(LIGHT_COLOR.rgb * COLOR.rgb * response, LIGHT_COLOR.a);
}
