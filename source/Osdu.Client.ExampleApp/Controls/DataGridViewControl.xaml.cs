using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Helpers;

namespace Osdu.Client.ExampleApp.Controls;

public partial class DataGridViewControl : UserControl
{
    private AppTheme _theme = AppTheme.Light;

    public DataGridViewControl()
    {
        InitializeComponent();
        MasterGrid.LoadingRow += MasterGrid_LoadingRow;
        MasterGrid.UnloadingRow += MasterGrid_UnloadingRow;
    }

    public void ApplyTheme(AppTheme theme)
    {
        _theme = theme;
        Background = theme.SurfaceBrush;
        MasterGrid.Background = theme.SurfaceBrush;
        MasterGrid.Foreground = theme.TextPrimaryBrush;
        MasterGrid.RowBackground = theme.CardBrush;
        MasterGrid.AlternatingRowBackground = new SolidColorBrush(theme.Surface);
        MasterGrid.HorizontalGridLinesBrush = theme.BorderBrush;
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
                    row.DetailSections.Add(new DetailSection(col, cell.Raw));
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
    }

    private void MasterGrid_UnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.MouseLeftButtonUp -= Row_MouseLeftButtonUp;
    }

    private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        // Toggle row details visibility
        row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private DataTemplate BuildRowDetailsTemplate()
    {
        // Use a FrameworkElementFactory to build the row details dynamically
        var template = new DataTemplate();

        var factory = new FrameworkElementFactory(typeof(ItemsControl));
        factory.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("DetailSections"));
        factory.SetValue(MarginProperty, new Thickness(20, 8, 8, 8));

        // Each detail section is rendered via its own DataTemplate
        var itemTemplate = new DataTemplate();
        var sectionFactory = new FrameworkElementFactory(typeof(StackPanel));
        sectionFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 8));

        // Section header
        var headerFactory = new FrameworkElementFactory(typeof(TextBlock));
        headerFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        headerFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        headerFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
        headerFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 4));
        sectionFactory.AppendChild(headerFactory);

        // Section content (ContentPresenter bound to RenderedContent)
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetBinding(ContentPresenter.ContentProperty, new Binding("RenderedContent"));
        sectionFactory.AppendChild(contentFactory);

        itemTemplate.VisualTree = sectionFactory;
        factory.SetValue(ItemsControl.ItemTemplateProperty, itemTemplate);

        template.VisualTree = factory;
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

    public DetailSection(string name, JsonElement raw)
    {
        Name = name;
        Raw = raw;
    }

    /// <summary>
    /// Lazily renders the JSON value as a WPF element for display in row details.
    /// </summary>
    public object RenderedContent => BuildContent(Raw);

    private static object BuildContent(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => BuildObjectGrid(element),
            JsonValueKind.Array => BuildArrayContent(element),
            _ => new TextBlock
            {
                Text = element.ToString(),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 2, 4, 2)
            }
        };
    }

    private static UIElement BuildObjectGrid(JsonElement obj)
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
                Margin = new Thickness(4, 2, 12, 2),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(keyBlock, row);
            Grid.SetColumn(keyBlock, 0);
            grid.Children.Add(keyBlock);

            UIElement valueElement;
            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                // Nest recursively inside an expander
                var expander = new Expander
                {
                    Header = prop.Value.ValueKind == JsonValueKind.Array
                        ? $"[{prop.Value.GetArrayLength()} items]"
                        : "{...}",
                    IsExpanded = false,
                    Margin = new Thickness(0, 2, 4, 2),
                    Content = BuildContent(prop.Value)
                };
                valueElement = expander;
            }
            else
            {
                valueElement = new TextBlock
                {
                    Text = prop.Value.ToString(),
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
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Margin = new Thickness(0, 2, 0, 2),
            Child = grid
        };

        return border;
    }

    private static UIElement BuildArrayContent(JsonElement array)
    {
        var panel = new StackPanel();
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
                    Margin = new Thickness(8, 1, 4, 1),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            return panel;
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
                    Margin = new Thickness(0, 2, 0, 2),
                    Content = BuildContent(item)
                };
                panel.Children.Add(expander);
            }
            else
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"[{index}] {item}",
                    Margin = new Thickness(8, 1, 4, 1)
                });
            }
            index++;
        }

        return panel;
    }
}