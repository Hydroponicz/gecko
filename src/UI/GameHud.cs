using Godot;

namespace Gecko.UI;

/// <summary>
/// Autoload. Minimal gameplay HUD: an interaction prompt at the bottom and
/// a build-mode panel at the top. Placeholder styling.
/// </summary>
public partial class GameHud : CanvasLayer
{
    public static GameHud Instance { get; private set; } = null!;

    private Label _prompt = null!;
    private Label _build = null!;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Layer = 10;
        _prompt = MakeLabel(fontSize: 26);
        _prompt.AnchorLeft = 0f;
        _prompt.AnchorRight = 1f;
        _prompt.AnchorTop = 1f;
        _prompt.AnchorBottom = 1f;
        _prompt.OffsetTop = -140f;
        _prompt.OffsetBottom = -60f;
        _prompt.VerticalAlignment = VerticalAlignment.Bottom;

        _build = MakeLabel(fontSize: 22);
        _build.AnchorLeft = 0f;
        _build.AnchorRight = 1f;
        _build.AnchorTop = 0f;
        _build.AnchorBottom = 0f;
        _build.OffsetTop = 20f;
        _build.OffsetBottom = 120f;
    }

    /// <summary>Bottom-center text, e.g. "[E] Load Paper". Empty hides it.</summary>
    public void SetPrompt(string text) => _prompt.Text = text;

    /// <summary>Top-center build-mode info. Empty hides it.</summary>
    public void SetBuildInfo(string text) => _build.Text = text;

    private Label MakeLabel(int fontSize)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("outline_size", 8);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(label);
        return label;
    }
}
