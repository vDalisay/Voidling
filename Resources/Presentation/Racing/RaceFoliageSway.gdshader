// Roadside foliage swaying in the breeze.
//
// The whole foliage layer is one canvas item, so each sprite's own UV decides how far its vertices
// move: tops sway, bases stay planted. The phase follows world X so neighbouring trees do not move
// in lockstep, and the offset is snapped to whole pixels to keep the art crisp.
shader_type canvas_item;

uniform float strength = 1.6;
uniform float speed = 1.4;

void vertex() {
	float top = 1.0 - UV.y;
	float gust = sin(TIME * speed + VERTEX.x * 0.045) + 0.4 * sin(TIME * speed * 2.3 + VERTEX.x * 0.11);
	VERTEX.x += round(gust * strength * top);
}
