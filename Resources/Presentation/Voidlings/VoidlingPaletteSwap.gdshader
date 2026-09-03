shader_type canvas_item;

uniform int palette_size = 0;
uniform float match_tolerance = 0.0125;
uniform vec4 source_0 : source_color;
uniform vec4 source_1 : source_color;
uniform vec4 source_2 : source_color;
uniform vec4 source_3 : source_color;
uniform vec4 source_4 : source_color;
uniform vec4 source_5 : source_color;
uniform vec4 source_6 : source_color;
uniform vec4 source_7 : source_color;
uniform vec4 target_0 : source_color;
uniform vec4 target_1 : source_color;
uniform vec4 target_2 : source_color;
uniform vec4 target_3 : source_color;
uniform vec4 target_4 : source_color;
uniform vec4 target_5 : source_color;
uniform vec4 target_6 : source_color;
uniform vec4 target_7 : source_color;

bool matches(vec3 pixel, vec3 source) {
    return distance(pixel, source) <= match_tolerance;
}

void fragment() {
    vec4 pixel = texture(TEXTURE, UV);
    if (palette_size > 0 && matches(pixel.rgb, source_0.rgb)) pixel.rgb = target_0.rgb;
    else if (palette_size > 1 && matches(pixel.rgb, source_1.rgb)) pixel.rgb = target_1.rgb;
    else if (palette_size > 2 && matches(pixel.rgb, source_2.rgb)) pixel.rgb = target_2.rgb;
    else if (palette_size > 3 && matches(pixel.rgb, source_3.rgb)) pixel.rgb = target_3.rgb;
    else if (palette_size > 4 && matches(pixel.rgb, source_4.rgb)) pixel.rgb = target_4.rgb;
    else if (palette_size > 5 && matches(pixel.rgb, source_5.rgb)) pixel.rgb = target_5.rgb;
    else if (palette_size > 6 && matches(pixel.rgb, source_6.rgb)) pixel.rgb = target_6.rgb;
    else if (palette_size > 7 && matches(pixel.rgb, source_7.rgb)) pixel.rgb = target_7.rgb;
    COLOR = pixel * COLOR;
}
