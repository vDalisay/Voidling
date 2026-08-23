using Godot;

namespace VoidlingGame;

public partial class EyeIcon : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(16, 12);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var color = Color.FromHtml("#4F5948");
        var top = new Vector2(8, 2);
        var right = new Vector2(14, 6);
        var bottom = new Vector2(8, 10);
        var left = new Vector2(2, 6);

        DrawLine(left, top, color, 1.5f);
        DrawLine(top, right, color, 1.5f);
        DrawLine(right, bottom, color, 1.5f);
        DrawLine(bottom, left, color, 1.5f);
        DrawCircle(new Vector2(8, 6), 2.2f, color);
        DrawCircle(new Vector2(8, 6), 0.8f, Color.FromHtml("#E8EAD8"));
    }
}
