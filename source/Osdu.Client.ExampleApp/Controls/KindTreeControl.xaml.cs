using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Services;

namespace Osdu.Client.ExampleApp.Controls;

public partial class KindTreeControl : UserControl
{
    private List<KindGroup> _allGroups = [];
    private AppTheme _theme = AppTheme.Light;

    public event Action<string>? KindSelected;

    public KindTreeControl()
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
        Background = theme.SidebarBrush;
        FilterBox.Background = theme.InputFieldBrush;
        FilterBox.Foreground = theme.TextPrimaryBrush;
        FilterBox.BorderBrush = theme.BorderBrush;
        FilterBox.CaretBrush = theme.TextPrimaryBrush;
        FilterBox.FontFamily = AppTheme.FontFamily;
        FilterBox.FontSize = AppTheme.FontSize;

        // Update dynamic resources so XAML styles pick up the theme
        Resources["ExpanderArrowBrush"] = new SolidColorBrush(theme.ExpanderArrow);
        Resources["AccentBrush"] = theme.AccentBrush;
        Resources["FolderFillBrush"] = new SolidColorBrush(
            Color.FromArgb(40, theme.Accent.R, theme.Accent.G, theme.Accent.B));
        Resources["TextSecondaryBrush"] = theme.TextSecondaryBrush;
        Resources["TextMutedBrush"] = theme.TextMutedBrush;
        Resources["TextPrimaryBrush"] = theme.TextPrimaryBrush;
        Resources["CardHoverBrush"] = new SolidColorBrush(theme.CardHover);
        Resources["SelectionBgBrush"] = theme.SelectionBgBrush;
        Resources["SelectionTextBrush"] = theme.SelectionTextBrush;

        RebuildTree();
    }

    public void LoadKinds(List<KindGroup> groups)
    {
        _allGroups = groups;
        RebuildTree();
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => RebuildTree();

    private void ExpandAllButton_Click(object sender, RoutedEventArgs e) =>
        SetAllExpanders(true);

    private void CollapseAllButton_Click(object sender, RoutedEventArgs e) =>
        SetAllExpanders(false);

    private void SetAllExpanders(bool expanded)
    {
        foreach (var child in KindPanel.Children)
        {
            if (child is Expander expander)
                expander.IsExpanded = expanded;
        }
    }

    private void RebuildTree()
    {
        KindPanel.Children.Clear();
        string filter = FilterBox.Text.Trim();

        foreach (var group in _allGroups)
        {
            var filtered = string.IsNullOrEmpty(filter)
                ? group.Kinds
                : group.Kinds.Where(k =>
                    k.KindId.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtered.Count == 0) continue;

            var expander = new Expander
            {
                Header = new TextBlock
                {
                    Text = $"{ToPascalCase(group.Category)} ({filtered.Count})",
                    FontFamily = AppTheme.FontFamily,
                    FontSize = AppTheme.FontSizeLarge,
                    Foreground = _theme.TextPrimaryBrush,
                    FontWeight = FontWeights.SemiBold
                },
                IsExpanded = !string.IsNullOrEmpty(filter),
                Style = (Style)Resources["KindExpanderStyle"]
            };

            var listBox = new ListBox
            {
                Style = (Style)Resources["KindListBoxStyle"],
                FontFamily = AppTheme.FontFamily,
                FontSize = AppTheme.FontSizeLarge
            };

            foreach (var kind in filtered)
            {
                var item = new ListBoxItem
                {
                    Content =
                        $"{(kind.EntityType.Contains("--") ? kind.EntityType[(kind.EntityType.LastIndexOf("--") + 2)..] : kind.EntityType)}{kind.KindId[(kind.KindId.LastIndexOf(':') + 1)..]}",
                    Tag = kind.KindId,
                    ToolTip = kind.KindId,
                    Style = (Style)Resources["KindItemStyle"]
                };
                item.Selected += (_, _) => KindSelected?.Invoke(kind.KindId);
                listBox.Items.Add(item);
            }

            expander.Content = listBox;
            KindPanel.Children.Add(expander);
        }
    }

    private static string ToPascalCase(string value) =>
        string.Concat(value.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => $"{char.ToUpperInvariant(word[0])}{word[1..]}"));
}