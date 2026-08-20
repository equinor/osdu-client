using System.IO;
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

        // Stream to lines directly to avoid holding two copies of the full string
        using var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, wrapper, s_pretty);
        ms.Position = 0;

        var lines = new List<string>();
        using var reader = new StreamReader(ms);
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        RawText.ItemsSource = lines;
    }

    public void Clear() => RawText.ItemsSource = null;

    private void CopyValue_Click(object sender, RoutedEventArgs e)
    {
        if (RawText.SelectedItem is not string line)
            return;

        var trimmed = line.TrimEnd(',').Trim();

        // In pretty-printed JSON, key-value pairs use the pattern  "key": value
        // Look for '": ' to reliably split key from value without matching colons inside values.
        var separatorIndex = trimmed.IndexOf("\": ");
        if (separatorIndex >= 0)
        {
            trimmed = trimmed[(separatorIndex + 3)..].Trim();
        }

        // Strip surrounding quotes if present
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        Clipboard.SetText(trimmed);
    }

    private void CopyLine_Click(object sender, RoutedEventArgs e)
    {
        if (RawText.SelectedItem is string line)
        {
            Clipboard.SetText(line.Trim());
        }
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (RawText.ItemsSource is List<string> lines)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
    }
}