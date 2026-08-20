using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp.Controls;

public partial class RawViewControl : UserControl
{
    private static readonly JsonSerializerOptions s_pretty = new() { WriteIndented = true };

    public RawViewControl()
    {
        InitializeComponent();
        FontFamily = AppTheme.MonoFontFamily;
        FontSize = AppTheme.FontSizeSmall;
    }

    public void ApplyTheme(AppTheme theme)
    {
        FontFamily = AppTheme.MonoFontFamily;
        FontSize = AppTheme.FontSizeSmall;
        RawText.Background = theme.ResponseBgBrush;
        RawText.Foreground = theme.TextPrimaryBrush;
        RawText.BorderBrush = theme.BorderBrush;
    }

    public void SetData(IReadOnlyList<JsonElement> records, long totalCount)
    {
        var wrapper = new
        {
            totalCount,
            count = records.Count,
            results = records
        };
        RawText.Text = JsonSerializer.Serialize(wrapper, s_pretty);
    }

    public void Clear() => RawText.Text = string.Empty;
}