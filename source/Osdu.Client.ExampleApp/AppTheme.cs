using System.Windows.Media;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Centralized theme definition supporting light and dark modes.
/// </summary>
public class AppTheme
{
    public static AppTheme Dark { get; } = new()
    {
        IsDark = true,
        Surface = Color.FromRgb(27, 27, 31),
        Sidebar = Color.FromRgb(20, 20, 24),
        Card = Color.FromRgb(35, 35, 41),
        CardHover = Color.FromRgb(42, 42, 50),
        Border = Color.FromRgb(58, 58, 68),
        Input = Color.FromRgb(27, 27, 31),
        InputField = Color.FromRgb(22, 22, 26),
        ResponseBg = Color.FromRgb(24, 24, 28),
        Tag = Color.FromRgb(45, 45, 55),
        TextPrimary = Color.FromRgb(232, 232, 237),
        TextSecondary = Color.FromRgb(144, 144, 160),
        TextMuted = Color.FromRgb(100, 100, 120),
        Accent = Color.FromRgb(108, 142, 239),
        Required = Color.FromRgb(255, 100, 100),
        ShadowOpacity = 0.15,
        ExpanderArrow = Color.FromRgb(180, 180, 200)
    };

    public static AppTheme Light { get; } = new()
    {
        IsDark = false,
        Surface = Color.FromRgb(248, 249, 251),
        Sidebar = Color.FromRgb(255, 255, 255),
        Card = Color.FromRgb(255, 255, 255),
        CardHover = Color.FromRgb(245, 246, 250),
        Border = Color.FromRgb(218, 220, 230),
        Input = Color.FromRgb(245, 246, 250),
        InputField = Color.FromRgb(255, 255, 255),
        ResponseBg = Color.FromRgb(250, 251, 253),
        Tag = Color.FromRgb(233, 235, 242),
        TextPrimary = Color.FromRgb(32, 33, 40),
        TextSecondary = Color.FromRgb(90, 95, 115),
        TextMuted = Color.FromRgb(130, 135, 150),
        Accent = Color.FromRgb(75, 110, 220),
        Required = Color.FromRgb(220, 50, 50),
        ShadowOpacity = 0.08,
        ExpanderArrow = Color.FromRgb(90, 95, 115)
    };

    public bool IsDark { get; init; }
    public Color Surface { get; init; }
    public Color Sidebar { get; init; }
    public Color Card { get; init; }
    public Color CardHover { get; init; }
    public Color Border { get; init; }
    public Color Input { get; init; }
    public Color InputField { get; init; }
    public Color ResponseBg { get; init; }
    public Color Tag { get; init; }
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextMuted { get; init; }
    public Color Accent { get; init; }
    public Color Required { get; init; }
    public double ShadowOpacity { get; init; }
    public Color ExpanderArrow { get; init; }

    // Convenience brush accessors
    public SolidColorBrush SurfaceBrush => new(Surface);
    public SolidColorBrush SidebarBrush => new(Sidebar);
    public SolidColorBrush CardBrush => new(Card);
    public SolidColorBrush BorderBrush => new(Border);
    public SolidColorBrush InputBrush => new(Input);
    public SolidColorBrush InputFieldBrush => new(InputField);
    public SolidColorBrush ResponseBgBrush => new(ResponseBg);
    public SolidColorBrush TagBrush => new(Tag);
    public SolidColorBrush TextPrimaryBrush => new(TextPrimary);
    public SolidColorBrush TextSecondaryBrush => new(TextSecondary);
    public SolidColorBrush TextMutedBrush => new(TextMuted);
    public SolidColorBrush AccentBrush => new(Accent);
    public SolidColorBrush RequiredBrush => new(Required);
}
