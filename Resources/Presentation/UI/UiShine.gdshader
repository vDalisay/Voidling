// A gleam that sweeps across a call-to-action button now and then. The band is measured in whole
// UI pixels (floor of the local position), so it moves in crisp one-pixel steps instead of
// smearing, and it only brightens what is already drawn (alpha-weighted).
shader_type canvas_item;

uniform float period = 4.2;
uniform float phase = 0.0;
uniform float width_px = 120.0;
uniform float band_px = 4.0;
uniform float strength = 0.38;

varying vec2 local_px;

void vertex() {
	local_px = VERTEX;
}

void fragment() {
	vec4 color = texture(TEXTURE, UV) * COLOR;
	float cycle = fract((TIME + phase) / period);
	// The sweep crosses in the first quarter of the cycle and rests for the rest of it.
	float head = mix(-24.0, width_px + 24.0, clamp(cycle * 4.0, 0.0, 1.0));
	vec2 pixel = floor(local_px);
	float d = pixel.x + pixel.y * 0.5 - head;
	float core = step(0.0, d) * step(d, band_px);
	float trail = step(band_px + 2.0, d) * step(d, band_px + 3.0) * 0.5;
	color.rgb = mix(color.rgb, vec3(1.0, 0.99, 0.9), (core + trail) * strength * step(0.5, color.a));
	COLOR = color;
}
