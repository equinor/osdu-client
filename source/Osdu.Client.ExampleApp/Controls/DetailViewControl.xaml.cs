using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Helpers;

namespace Osdu.Client.ExampleApp.Controls;

public partial class DetailViewControl : UserControl
{
    private AppTheme _theme = AppTheme.Light;

    public DetailViewControl()
    {
        InitializeComponent();
        MasterGrid.LoadingRow += MasterGrid_LoadingRow;
        MasterGrid.UnloadingRow += MasterGrid_UnloadingRow;
    }

    public void ApplyTheme(AppTheme theme)
    {
        _theme = theme;
        Background = theme.SurfaceBrush;
        theme.ApplyToDataGrid(MasterGrid);

        // Apply scrollbar style at the control level so it persists
        theme.ApplyScrollBarStyle(this);

        // Re-render if data is present so detail sections pick up new theme
        if (MasterGrid.ItemsSource is ObservableCollection<MasterDetailRow> rows && rows.Count > 0)
        {
            MasterGrid.RowDetailsTemplate = BuildRowDetailsTemplate();
        }
    }

    public void SetData(IReadOnlyList<JsonElement> records)
    {
        MasterGrid.Columns.Clear();
        MasterGrid.RowDetailsTemplate = null;
        MasterGrid.ItemsSource = null;

        if (records.Count == 0) return;

        // Separate scalar columns from complex (detail) columns
        var allColumns = JsonHelper.ExtractColumns(records);
        var scalarColumns = new List<string>();
        var detailColumns = new List<string>();

        foreach (var col in allColumns)
        {
            bool isComplex = records.Any(r =>
            {
                if (r.ValueKind != JsonValueKind.Object) return false;
                if (!r.TryGetProperty(col, out var val)) return false;
                return val.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
            });

            if (isComplex)
                detailColumns.Add(col);
            else
                scalarColumns.Add(col);
        }

        // Build rows
        var rows = new ObservableCollection<MasterDetailRow>();
        foreach (var record in records)
        {
            var flat = JsonHelper.FlattenRecord(record);
            var row = new MasterDetailRow();

            foreach (var col in scalarColumns)
                row.ScalarValues[col] = flat.TryGetValue(col, out var cell) ? cell.Display : null;

            foreach (var col in detailColumns)
            {
                if (flat.TryGetValue(col, out var cell))
                    row.DetailSections.Add(new DetailSection(col, cell.Raw, _theme));
            }

            rows.Add(row);
        }

        // Add scalar columns to the grid
        foreach (var col in scalarColumns)
        {
            MasterGrid.Columns.Add(new DataGridTextColumn
            {
                Header = col,
                Binding = new Binding($"ScalarValues[{col}]") { Mode = BindingMode.OneWay },
                MaxWidth = 400
            });
        }

        // If there are detail columns, add a summary column and row details template
        if (detailColumns.Count > 0)
        {
            MasterGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Details",
                Binding = new Binding("DetailSummary") { Mode = BindingMode.OneWay },
                MaxWidth = 300,
                FontStyle = FontStyles.Italic
            });

            MasterGrid.RowDetailsTemplate = BuildRowDetailsTemplate();
        }

        MasterGrid.ItemsSource = rows;
    }

    public void Clear()
    {
        MasterGrid.Columns.Clear();
        MasterGrid.RowDetailsTemplate = null;
        MasterGrid.ItemsSource = null;
    }

    private void MasterGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.MouseLeftButtonUp += Row_MouseLeftButtonUp;
        // Style the row details background
        e.Row.DetailsVisibility = Visibility.Collapsed;
    }

    private void MasterGrid_UnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.MouseLeftButtonUp -= Row_MouseLeftButtonUp;
    }

    private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        // Only toggle details on double-click or if user clicks the "Details" column area
        // Use single click to just select; use the expander icon in details column to expand
        row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        e.Handled = true; // Prevent selection change confusion
    }

    private DataTemplate BuildRowDetailsTemplate()
    {
        // Use a FrameworkElementFactory to build the row details dynamically
        var template = new DataTemplate();

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, _theme.SurfaceBrush);
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));
        borderFactory.SetValue(Border.BorderBrushProperty, _theme.BorderBrush);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0, 1, 0, 0));

        var factory = new FrameworkElementFactory(typeof(ItemsControl));
        factory.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("DetailSections"));
        factory.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0));

        // Each detail section is rendered via its own DataTemplate
        var itemTemplate = new DataTemplate();
        var sectionFactory = new FrameworkElementFactory(typeof(StackPanel));
        sectionFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 8));

        // Section header
        var headerFactory = new FrameworkElementFactory(typeof(TextBlock));
        headerFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        headerFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        headerFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
        headerFactory.SetValue(TextBlock.ForegroundProperty, _theme.TextPrimaryBrush);
        headerFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 4));
        sectionFactory.AppendChild(headerFactory);

        // Section content (ContentPresenter bound to RenderedContent)
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetBinding(ContentPresenter.ContentProperty, new Binding("RenderedContent"));
        sectionFactory.AppendChild(contentFactory);

        itemTemplate.VisualTree = sectionFactory;
        factory.SetValue(ItemsControl.ItemTemplateProperty, itemTemplate);

        borderFactory.AppendChild(factory);
        template.VisualTree = borderFactory;
        return template;
    }
}

/// <summary>
/// Represents a row with scalar values displayed in the master grid
/// and complex detail sections shown in row details.
/// </summary>
public class MasterDetailRow
{
    public Dictionary<string, string?> ScalarValues { get; } = new();
    public List<DetailSection> DetailSections { get; } = [];

    public string DetailSummary => DetailSections.Count > 0
        ? $"▸ {DetailSections.Count} section(s): {string.Join(", ", DetailSections.Select(d => d.Name))}"
        : "";
}

/// <summary>
/// A named detail section containing a complex JSON value rendered as a UI element.
/// </summary>
public class DetailSection
{
    public string Name { get; }
    public JsonElement Raw { get; }
    private readonly AppTheme _theme;

    public DetailSection(string name, JsonElement raw, AppTheme theme)
    {
        Name = name;
        Raw = raw;
        _theme = theme;
    }

    /// <summary>
    /// Lazily renders the JSON value as a WPF element for display in row details.
    /// </summary>
    public object RenderedContent => BuildContent(Raw, _theme);

    private static object BuildContent(JsonElement element, AppTheme theme)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => BuildObjectGrid(element, theme),
            JsonValueKind.Array => BuildArrayContent(element, theme).Content,
            _ => new TextBlock
            {
                Text = element.ToString(),
                Foreground = theme.TextPrimaryBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 2, 4, 2)
            }
        };
    }

    private static UIElement BuildObjectGrid(JsonElement obj, AppTheme theme)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        foreach (var prop in obj.EnumerateObject())
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var keyBlock = new TextBlock
            {
                Text = prop.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextSecondaryBrush,
                Margin = new Thickness(4, 2, 12, 2),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(keyBlock, row);
            Grid.SetColumn(keyBlock, 0);
            grid.Children.Add(keyBlock);

            UIElement valueElement;
            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var (content, childExpanders) = BuildArrayContent(prop.Value, theme);
                    var expander = new Expander
                    {
                        Header = BuildArrayExpanderHeader(
                            $"[{prop.Value.GetArrayLength()} items]", childExpanders, theme),
                        IsExpanded = false,
                        Foreground = theme.TextPrimaryBrush,
                        Margin = new Thickness(0, 2, 4, 2),
                        Content = content
                    };
                    valueElement = expander;
                }
                else
                {
                    var expander = new Expander
                    {
                        Header = "{...}",
                        IsExpanded = false,
                        Foreground = theme.TextPrimaryBrush,
                        Margin = new Thickness(0, 2, 4, 2),
                        Content = BuildContent(prop.Value, theme)
                    };
                    valueElement = expander;
                }
            }
            else
            {
                valueElement = new TextBlock
                {
                    Text = prop.Value.ToString(),
                    Foreground = theme.TextPrimaryBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 2, 4, 2),
                    VerticalAlignment = VerticalAlignment.Top
                };
            }

            Grid.SetRow(valueElement, row);
            Grid.SetColumn(valueElement, 1);
            grid.Children.Add(valueElement);

            row++;
        }

        var border = new Border
        {
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = theme.CardBrush,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 2, 0, 2),
            Child = grid
        };

        return border;
    }

    /// <summary>
    /// Builds a header panel with the label text and Expand All / Collapse All buttons.
    /// </summary>
    private static UIElement BuildArrayExpanderHeader(string label, List<Expander> expanders, AppTheme theme)
    {
        if (expanders.Count == 0)
            return new TextBlock { Text = label, Foreground = theme.TextPrimaryBrush };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var expandAllBtn = new Button
        {
            Content = "▼ Expand All",
            Background = theme.CardBrush,
            Foreground = theme.TextPrimaryBrush,
            BorderBrush = theme.BorderBrush,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 11,
            Cursor = Cursors.Hand
        };
        expandAllBtn.Click += (s, e) =>
        {
            foreach (var exp in expanders)
                SetExpandedRecursive(exp, true);
            var parentExpander = FindAncestor<Expander>((DependencyObject)s!);
            parentExpander?.Dispatcher.BeginInvoke(() => parentExpander.IsExpanded = true);
            e.Handled = true;
        };

        var collapseAllBtn = new Button
        {
            Content = "▲ Collapse All",
            Background = theme.CardBrush,
            Foreground = theme.TextPrimaryBrush,
            BorderBrush = theme.BorderBrush,
            Padding = new Thickness(4, 1, 4, 1),
            FontSize = 11,
            Cursor = Cursors.Hand
        };
        collapseAllBtn.Click += (s, e) =>
        {
            foreach (var exp in expanders)
                SetExpandedRecursive(exp, false);
            var parentExpander = FindAncestor<Expander>((DependencyObject)s!);
            parentExpander?.Dispatcher.BeginInvoke(() => parentExpander.IsExpanded = true);
            e.Handled = true;
        };

        panel.Children.Add(expandAllBtn);
        panel.Children.Add(collapseAllBtn);
        return panel;
    }

    /// <summary>
    /// Walks up the visual tree to find the nearest ancestor of type T.
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Recursively sets IsExpanded on an expander and all nested expanders within it.
    /// </summary>
    private static void SetExpandedRecursive(Expander expander, bool isExpanded)
    {
        expander.IsExpanded = isExpanded;

        if (expander.Content is DependencyObject content)
            SetExpandedRecursiveVisual(content, isExpanded);
    }

    private static void SetExpandedRecursiveVisual(DependencyObject parent, bool isExpanded)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);

        // If not yet in the visual tree, walk the logical tree instead
        if (childCount == 0 && parent is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is Expander childExpander)
                    SetExpandedRecursive(childExpander, isExpanded);
                else if (child is DependencyObject dep)
                    SetExpandedRecursiveVisual(dep, isExpanded);
            }
            return;
        }

        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Expander childExp)
                SetExpandedRecursive(childExp, isExpanded);
            else
                SetExpandedRecursiveVisual(child, isExpanded);
        }
    }

    private static (UIElement Content, List<Expander> Expanders) BuildArrayContent(JsonElement array, AppTheme theme)
    {
        var panel = new StackPanel();
        var expanders = new List<Expander>();
        int index = 0;

        // Check if it's a simple array (all primitives/strings)
        bool allSimple = true;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                allSimple = false;
                break;
            }
        }

        if (allSimple)
        {
            // Render as a simple list
            foreach (var item in array.EnumerateArray())
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"• {item}",
                    Foreground = theme.TextPrimaryBrush,
                    Margin = new Thickness(8, 1, 4, 1),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            return (panel, expanders);
        }

        // Complex array — render each item in an expander
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                var expander = new Expander
                {
                    Header = $"[{index}]",
                    IsExpanded = false,
                    Foreground = theme.TextPrimaryBrush,
                    Margin = new Thickness(0, 2, 0, 2),
                    Content = BuildContent(item, theme)
                };
                expanders.Add(expander);
                panel.Children.Add(expander);
            }
            else
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"[{index}] {item}",
                    Foreground = theme.TextPrimaryBrush,
                    Margin = new Thickness(8, 1, 4, 1)
                });
            }
            index++;
        }

        return (panel, expanders);
    }
}