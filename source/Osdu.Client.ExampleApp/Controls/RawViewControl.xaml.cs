using System.Text.Json;
using System.Windows.Controls;

namespace Osdu.Client.ExampleApp.Controls;

public partial class RawViewControl : UserControl
{
    private static readonly JsonSerializerOptions s_pretty = new() { WriteIndented = true };

    public RawViewControl()
    {
        InitializeComponent();
    }

    public void ApplyTheme(AppTheme theme)
    {
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