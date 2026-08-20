using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Osdu.Client.ExampleApp.Helpers;

namespace Osdu.Client.ExampleApp.Controls;

public partial class TreeViewTab : UserControl
{
    private IReadOnlyList<JsonElement> _records = [];
    private AppTheme _theme = AppTheme.Light;

    public TreeViewTab()
    {
        InitializeComponent();
        FontFamily = AppTheme.FontFamily;
        FontSize = AppTheme.FontSize;
    }

    public void ApplyTheme(AppTheme theme)
    {
        _theme = theme;
        FontFamily = AppTheme.FontFamily;
        FontSize = AppTheme.FontSize;
        Background = theme.SurfaceBrush;
        JsonTree.Background = theme.SurfaceBrush;
        JsonTree.Foreground = theme.TextPrimaryBrush;
        RecordSelector.Background = theme.InputFieldBrush;
        RecordSelector.Foreground = theme.TextPrimaryBrush;
    }

    public void SetData(IReadOnlyList<JsonElement> records)
    {
        _records = records;
        RecordSelector.Items.Clear();
        for (int i = 0; i < records.Count; i++)
        {
            string label = TryGetId(records[i]) ?? $"Record {i}";
            RecordSelector.Items.Add(new ComboBoxItem { Content = label, Tag = i });
        }

        if (RecordSelector.Items.Count > 0)
            RecordSelector.SelectedIndex = 0;
    }

    public void Clear()
    {
        RecordSelector.Items.Clear();
        JsonTree.Items.Clear();
    }

    private void RecordSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordSelector.SelectedItem is ComboBoxItem { Tag: int idx } && idx < _records.Count)
            BuildTree(_records[idx]);
    }

    private void BuildTree(JsonElement element)
    {
        JsonTree.Items.Clear();
        var nodes = JsonHelper.BuildTree(element);
        foreach (var node in nodes)
            JsonTree.Items.Add(CreateTreeItem(node));
    }

    private TreeViewItem CreateTreeItem(JsonTreeNode node)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = node.Key + ": ",
            FontWeight = FontWeights.SemiBold,
            Foreground = _theme.AccentBrush
        });

        panel.Children.Add(new TextBlock
        {
            Text = node.DisplayValue,
            Foreground = node.IsLeaf ? _theme.TextPrimaryBrush : _theme.TextSecondaryBrush
        });

        var item = new TreeViewItem { Header = panel };
        item.MouseRightButtonDown += (_, _) =>
        {
            item.ContextMenu = new ContextMenu();
            var copyItem = new MenuItem { Header = "Copy Value" };
            copyItem.Click += (_, _) => Clipboard.SetText(node.CopyText);
            item.ContextMenu.Items.Add(copyItem);
        };

        if (node.Children is not null)
        {
            foreach (var child in node.Children)
                item.Items.Add(CreateTreeItem(child));
        }

        return item;
    }

    private static string? TryGetId(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("id", out var id))
            return id.GetString();
        return null;
    }
}