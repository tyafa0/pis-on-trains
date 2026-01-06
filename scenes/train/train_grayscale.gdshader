shader_type canvas_item;

// 外部（C#）から切り替え可能なスイッチ
uniform bool is_grayscale = false;

void fragment() {
    // もとの画像の色を取得
    vec4 input_color = texture(TEXTURE, UV);

    // グレースケール値を計算（一般的な輝度計算式）
    float gray = dot(input_color.rgb, vec3(0.299, 0.587, 0.114));

    // スイッチがONなら灰色、OFFなら元の色
    if (is_grayscale) {
        COLOR = vec4(gray, gray, gray, input_color.a);
    } else {
        COLOR = input_color;
    }
}