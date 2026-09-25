// A plain sprite lit like the palette-swapped Voidling body: accessories on that body, and the
// Garden's grass, flowers, mushrooms and placed trees. Its normal map comes from its CanvasTexture
// and the light is stepped the same way (see BalancedKeyLight).
shader_type canvas_item;

#include "res://Resources/Presentation/Lighting/BalancedKeyLight.gdshaderinc"

void fragment() {
	LIGHT_VERTEX = texel_light_vertex(LIGHT_VERTEX, UV, TEXTURE_PIXEL_SIZE);
}
