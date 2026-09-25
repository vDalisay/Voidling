// Trees swaying in the wind.
//
// The canopy leans and the trunk stays planted: each vertex moves by how far up the sprite it is.
// The phase follows the tree's world position so neighbours do not sway in lockstep, and offsets
// snap to whole pixels to keep the art crisp. Wind comes from the Garden's weather.
shader_type canvas_item;

// The tree carries a generated normal map; the sun and nearby lights shade its canopy through this.
#include "res://Resources/Presentation/Lighting/BalancedKeyLight.gdshaderinc"

uniform float wind = 0.25;

void vertex() {
	float height = 1.0 - UV.y;
	float phase = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).x * 0.043;
	float speed = 1.0 + wind * 1.4;
	float gust = sin(TIME * speed + phase) + 0.35 * sin(TIME * speed * 2.7 + phase * 1.9);
	VERTEX.x += round(gust * (0.5 + wind * 2.6) * height * height);
}

void fragment() {
	LIGHT_VERTEX = texel_light_vertex(LIGHT_VERTEX, UV, TEXTURE_PIXEL_SIZE);
}
