using Godot;

namespace Gecko.UI;

/// <summary>
/// Autoload. Minimal gameplay HUD: an interaction prompt at the bottom and
/// a build-mode panel at the top. Placeholder styling.
/// </summary>
public partial class GameHud : CanvasLayer
{
    public static GameHud Instance { get; private set; } = null!;

    public const int MenuRows = 4;

    private Label _prompt = null!;
    private Label _build = null!;
    private PanelContainer _menu = null!;
    private Label _menuTitle = null!;
    private Label _menuStatus = null!;
    private readonly Label[] _menuRows = new Label[MenuRows];
    private Label _menuFooter = null!;

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

        BuildMenu();
    }

    /// <summary>One row of the targeting menu.</summary>
    public readonly record struct MenuRow(string Button, string Label, string Detail, bool Enabled, float HoldProgress);

    /// <summary>Shows the targeting menu (bottom right). Pass null to hide it.</summary>
    public void SetMenu(string? title, string status = "", MenuRow[]? rows = null, string footer = "")
    {
        _menu.Visible = title != null;
        if (title == null)
            return;
        _menuTitle.Text = title;
        _menuStatus.Text = status;
        for (int i = 0; i < MenuRows; i++)
        {
            var label = _menuRows[i];
            if (rows == null || i >= rows.Length)
            {
                label.Visible = false;
                continue;
            }
            var row = rows[i];
            string hold = row.HoldProgress > 0f ? "  " + Bar(row.HoldProgress) : "";
            label.Text = $"[{row.Button}]  {row.Label}   ({row.Detail}){hold}";
            label.Modulate = row.Enabled ? Colors.White : new Color(1f, 1f, 1f, 0.4f);
            label.Visible = true;
        }
        _menuFooter.Text = footer;
    }

    private void BuildMenu()
    {
        _menu = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetLeft = -40f,
            OffsetTop = -40f,
            OffsetRight = -40f,
            OffsetBottom = -40f,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.6f),
            ContentMarginLeft = 16f,
            ContentMarginRight = 16f,
            ContentMarginTop = 12f,
            ContentMarginBottom = 12f,
        };
        _menu.AddThemeStyleboxOverride("panel", style);
        AddChild(_menu);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _menu.AddChild(column);
        _menuTitle = MakeRowLabel(column, 24);
        _menuStatus = MakeRowLabel(column, 16);
        _menuStatus.Modulate = new Color(1f, 1f, 1f, 0.75f);
        for (int i = 0; i < MenuRows; i++)
            _menuRows[i] = MakeRowLabel(column, 20);
        _menuFooter = MakeRowLabel(column, 14);
        _menuFooter.Modulate = new Color(1f, 1f, 1f, 0.6f);
    }

    private static Label MakeRowLabel(Container parent, int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        parent.AddChild(label);
        return label;
    }

    private static string Bar(float progress)
    {
        const int width = 8;
        int filled = Mathf.Clamp(Mathf.RoundToInt(progress * width), 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
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
