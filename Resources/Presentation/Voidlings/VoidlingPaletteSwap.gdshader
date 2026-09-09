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
    // COLOR already carries the sampled texel multiplied by this item's modulate, so the swap is
    // applied as a per-pixel ratio. Writing `texel * COLOR` instead would multiply the sheet into
    // itself, which is what made every Voidling render darker and more saturated than the artwork.
    vec3 texel = texture(TEXTURE, UV).rgb;
    vec3 swapped = texel;
    if (palette_size > 0 && matches(texel, source_0.rgb)) swapped = target_0.rgb;
    else if (palette_size > 1 && matches(texel, source_1.rgb)) swapped = target_1.rgb;
    else if (palette_size > 2 && matches(texel, source_2.rgb)) swapped = target_2.rgb;
    else if (palette_size > 3 && matches(texel, source_3.rgb)) swapped = target_3.rgb;
    else if (palette_size > 4 && matches(texel, source_4.rgb)) swapped = target_4.rgb;
    else if (palette_size > 5 && matches(texel, source_5.rgb)) swapped = target_5.rgb;
    else if (palette_size > 6 && matches(texel, source_6.rgb)) swapped = target_6.rgb;
    else if (palette_size > 7 && matches(texel, source_7.rgb)) swapped = target_7.rgb;
    COLOR.rgb *= swapped / max(texel, vec3(1.0 / 255.0));
}
