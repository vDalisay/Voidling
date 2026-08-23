using Godot;

namespace VoidlingGame;

public partial class PerspectiveHaloWorld : Node2D
{
    public bool Compact { get; set; }

    public override void _Draw()
    {
        var pixels = Compact
            ? new[] { " ### ", "#   #", " ### " }
            : new[] { "  #####  ", "##     ##", "#       #", "##     ##", "  #####  " };
        var origin = new Vector2(-(pixels[0].Length - 1) * 0.5f, -(pixels.Length - 1) * 0.5f);
        var back = Color.FromHtml("#C99B37");
        var gold = Color.FromHtml("#F1CE55");
        var shine = Color.FromHtml("#FFF2A8");

        // One world pixel per cell keeps the halo crisp with the project's nearest filtering.
        for (var y = 0; y < pixels.Length; y++)
        {
            for (var x = 0; x < pixels[y].Length; x++)
            {
                if (pixels[y][x] != '#')
                    continue;

                var color = y < pixels.Length / 2 ? back : y == pixels.Length / 2 ? gold : shine;
                DrawRect(new Rect2(origin + new Vector2(x, y), Vector2.One), color);
            }
        }
    }
}
