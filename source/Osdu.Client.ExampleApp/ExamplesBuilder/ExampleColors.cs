using System.Windows.Media;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Shared color constants for example UI elements.
/// </summary>
internal static class ExampleColors
{
    public static readonly Color SuccessColor = Color.FromRgb(73, 204, 144);
    public static readonly Color FailureColor = Color.FromRgb(249, 80, 80);

    public static SolidColorBrush SuccessBrush => new(SuccessColor);
    public static SolidColorBrush FailureBrush => new(FailureColor);

    public static SolidColorBrush StatusBrush(bool success) => success ? SuccessBrush : FailureBrush;
}
