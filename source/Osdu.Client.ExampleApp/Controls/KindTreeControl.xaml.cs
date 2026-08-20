using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Services;

namespace Osdu.Client.ExampleApp.Controls;

public partial class KindTreeControl : UserControl
{
    private List<KindGroup> _allGroups = [];
    private AppTheme _theme = AppTheme.Light;
    private ListBoxItem? _selectedItem;

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
        RebuildTree();
    }

    public void LoadKinds(List<KindGroup> groups)
    {
        _allGroups = groups;
        RebuildTree();
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => RebuildTree();

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
                    Text = $"{group.Category} ({filtered.Count})",
                    FontFamily = AppTheme.FontFamily,
                    FontSize = AppTheme.FontSize,
                    Foreground = _theme.TextPrimaryBrush,
                    FontWeight = FontWeights.SemiBold
                },
                IsExpanded = !string.IsNullOrEmpty(filter),
                FontFamily = AppTheme.FontFamily,
                FontSize = AppTheme.FontSize,
                Foreground = _theme.TextPrimaryBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var listBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(12, 0, 0, 0),
                FontFamily = AppTheme.FontFamily,
                FontSize = AppTheme.FontSizeSmall
            };

            foreach (var kind in filtered)
            {
                var item = new ListBoxItem
                {
                    Content = kind.EntityType,
                    Tag = kind.KindId,
                    ToolTip = kind.KindId,
                    Foreground = _theme.TextSecondaryBrush,
                    Padding = new Thickness(6, 3, 6, 3),
                    Cursor = Cursors.Hand
                };
                item.MouseLeftButtonUp += (_, _) =>
                {
                    if (_selectedItem is not null)
                        _selectedItem.Background = Brushes.Transparent;

                    item.Background = new SolidColorBrush(_theme.CardHover);
                    _selectedItem = item;
                    KindSelected?.Invoke(kind.KindId);
                };
                listBox.Items.Add(item);
            }

            expander.Content = listBox;
            KindPanel.Children.Add(expander);
        }
    }
}