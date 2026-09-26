shader_type canvas_item;

#include "res://Resources/Presentation/Lighting/BalancedKeyLight.gdshaderinc"

vec3 rainbow_color(float hue) {
    vec3 rgb = clamp(abs(mod(hue * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return mix(vec3(1.0), rgb, 0.72);
}

void fragment() {
    vec4 texel = texture(TEXTURE, UV);
    float hue = fract(TIME / 18.0 + UV.y * 0.22 + UV.x * 0.10);
    float brightness = max(max(texel.r, texel.g), texel.b);
    float color_amount = smoothstep(0.16, 0.55, brightness) * 0.78;
    vec3 rainbow = rainbow_color(hue);
    COLOR.rgb = mix(texel.rgb, rainbow * brightness * 1.14, color_amount);

    // A restrained one-pixel colored halo keeps the silhouette visible against dark garden tiles.
    float nearby = max(max(texture(TEXTURE, UV + vec2(TEXTURE_PIXEL_SIZE.x, 0.0)).a,
                           texture(TEXTURE, UV - vec2(TEXTURE_PIXEL_SIZE.x, 0.0)).a),
                       max(texture(TEXTURE, UV + vec2(0.0, TEXTURE_PIXEL_SIZE.y)).a,
                           texture(TEXTURE, UV - vec2(0.0, TEXTURE_PIXEL_SIZE.y)).a));
    float halo = (1.0 - texel.a) * nearby * 0.18;
    COLOR.rgb = mix(COLOR.rgb, rainbow, halo);
    COLOR.a = max(texel.a, halo);
    LIGHT_VERTEX = texel_light_vertex(LIGHT_VERTEX, UV, TEXTURE_PIXEL_SIZE);
}
