shader_type canvas_item;

// The mask atlas is aligned 1:1 with the source atlas.
// R = primary body, G = secondary/two-tone, B = authored accent.
uniform sampler2D fill_mask : filter_nearest, repeat_disable;
uniform sampler2D pattern_mask : filter_nearest, repeat_disable;
uniform bool pattern_enabled = false;
uniform vec4 primary_color : source_color = vec4(1.0);
uniform vec4 secondary_color : source_color = vec4(1.0);
uniform vec4 accent_color : source_color = vec4(1.0);
uniform float shiny_strength : hint_range(0.0, 1.0) = 0.0;
uniform float glow_strength : hint_range(0.0, 1.0) = 0.0;
uniform float glisten_strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    vec4 source = texture(TEXTURE, UV);
    vec4 mask = texture(fill_mask, UV);

    vec3 rgb = source.rgb;
    rgb = mix(rgb, primary_color.rgb, clamp(mask.r, 0.0, 1.0));
    rgb = mix(rgb, secondary_color.rgb, clamp(mask.g, 0.0, 1.0));
    rgb = mix(rgb, accent_color.rgb, clamp(mask.b, 0.0, 1.0));

    if (pattern_enabled) {
        float pattern = texture(pattern_mask, UV).r;
        rgb = mix(rgb, accent_color.rgb, clamp(pattern, 0.0, 1.0));
    }

    // Finish effects are deliberately presentation-only. TIME never enters Domain/Application state.
    if (shiny_strength > 0.0) {
        float sheen = 0.5 + 0.5 * sin((UV.x + UV.y) * 18.0 - TIME * 2.2);
        rgb += vec3(sheen * shiny_strength * 0.22);
    }

    if (glow_strength > 0.0) {
        float pulse = 0.75 + 0.25 * sin(TIME * 2.0);
        rgb += primary_color.rgb * glow_strength * pulse;
    }

    if (glisten_strength > 0.0) {
        float streak = sin((UV.x * 31.0 + UV.y * 47.0) - TIME * 4.0);
        float sparkle = smoothstep(0.94, 1.0, streak);
        rgb += vec3(sparkle * glisten_strength);
    }

    COLOR = vec4(clamp(rgb, vec3(0.0), vec3(1.0)), source.a);
}
