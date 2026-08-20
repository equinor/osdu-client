using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Helpers;

namespace Osdu.Client.ExampleApp.Controls;

public partial class TabularViewControl : UserControl
{
    private AppTheme _theme = AppTheme.Light;
    private readonly Stack<(string Label, IReadOnlyList<JsonElement> Data)> _navStack = new();
    private IReadOnlyList<JsonElement> _currentData = [];

    public TabularViewControl()
    {
        InitializeComponent();
    }

    public void ApplyTheme(AppTheme theme)
    {
        _theme = theme;
        Background = theme.SurfaceBrush;
        DataGrid.Background = theme.SurfaceBrush;
        DataGrid.Foreground = theme.TextPrimaryBrush;
        DataGrid.RowBackground = theme.CardBrush;
        DataGrid.AlternatingRowBackground = new SolidColorBrush(theme.Surface);
        DataGrid.HorizontalGridLinesBrush = theme.BorderBrush;
        BreadcrumbPanel.Background = theme.SurfaceBrush;
    }

    public void SetData(IReadOnlyList<JsonElement> records, string label = "Root")
    {
        _navStack.Clear();
        ShowData(records, label);
    }

    /// <summary>
    /// Scrolls to the specified row index and highlights (selects) it.
    /// </summary>
    public void ScrollToRowAndHighlight(int rowIndex)
    {
        if (DataGrid.Items.Count == 0 || rowIndex < 0 || rowIndex >= DataGrid.Items.Count)
            return;

        DataGrid.SelectedIndex = rowIndex;
        DataGrid.ScrollIntoView(DataGrid.Items[rowIndex]);

        // Focus the row for visual highlight
        DataGrid.UpdateLayout();
        var row = DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
        row?.Focus();
    }

    public void Clear()
    {
        DataGrid.Columns.Clear();
        DataGrid.ItemsSource = null;
        _navStack.Clear();
        BreadcrumbPanel.Children.Clear();
    }

    private void ShowData(IReadOnlyList<JsonElement> records, string label)
    {
        _currentData = records;
        DataGrid.Columns.Clear();
        DataGrid.ItemsSource = null;

        if (records.Count == 0) return;

        // If all elements are simple values (e.g. string array), show single-column
        if (records.All(r => r.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)))
        {
            ShowSimpleList(records, label);
            return;
        }

        var columns = JsonHelper.ExtractColumns(records);
        var rows = new ObservableCollection<RowData>();

        int rowIndex = 1;
        foreach (var record in records)
        {
            var flat = JsonHelper.FlattenRecord(record);
            var row = new RowData();
            row["#"] = new CellValue(rowIndex.ToString(), default);
            foreach (var col in columns)
            {
                row[col] = flat.TryGetValue(col, out var cell) ? cell : null;
            }
            rows.Add(row);
            rowIndex++;
        }

        // Add row number column
        DataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "#",
            Binding = new Binding("[#]") { Mode = BindingMode.OneWay },
            MaxWidth = 60,
            IsReadOnly = true
        });

        foreach (var col in columns)
        {
            var column = CreateColumn(col);
            DataGrid.Columns.Add(column);
        }

        DataGrid.ItemsSource = rows;
        DataGrid.MouseDoubleClick -= DataGrid_MouseDoubleClick;
        DataGrid.MouseDoubleClick += DataGrid_MouseDoubleClick;

        UpdateBreadcrumbs(label);
    }

    private void ShowSimpleList(IReadOnlyList<JsonElement> records, string label)
    {
        var rows = new ObservableCollection<RowData>();
        int rowIndex = 1;
        foreach (var record in records)
        {
            var row = new RowData();
            row["#"] = new CellValue(rowIndex.ToString(), default);
            row["Value"] = new CellValue(record.ToString(), record);
            rows.Add(row);
            rowIndex++;
        }

        DataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "#",
            Binding = new Binding("[#]") { Mode = BindingMode.OneWay },
            MaxWidth = 60,
            IsReadOnly = true
        });
        DataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding("[Value]") { Mode = BindingMode.OneWay },
            MaxWidth = 800
        });
        DataGrid.ItemsSource = rows;
        UpdateBreadcrumbs(label);
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataGrid.CurrentCell.Column is not DataGridTextColumn col) return;
        if (DataGrid.CurrentItem is not RowData row) return;

        var key = (string)col.Header;
        if (!row.TryGetValue(key, out var val) || val is not CellValue cell || !cell.IsExpandable)
            return;

        // Push current state
        _navStack.Push((BreadcrumbPanel.Tag as string ?? "Root", _currentData));

        // Drill into the expandable value
        var childRecords = new List<JsonElement>();
        if (cell.Raw.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in cell.Raw.EnumerateArray())
                childRecords.Add(item);
        }
        else if (cell.Raw.ValueKind == JsonValueKind.Object)
        {
            childRecords.Add(cell.Raw);
        }

        ShowData(childRecords, key);
    }

    private void UpdateBreadcrumbs(string currentLabel)
    {
        BreadcrumbPanel.Children.Clear();
        BreadcrumbPanel.Tag = currentLabel;

        var trail = _navStack.Reverse().ToList();
        foreach (var (label, data) in trail)
        {
            var captured = data;
            var capturedLabel = label;
            var link = new TextBlock
            {
                Text = label,
                Foreground = _theme.AccentBrush,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 13
            };
            link.MouseLeftButtonUp += (_, _) =>
            {
                // Pop back to this level
                while (_navStack.Count > 0 && _navStack.Peek().Label != capturedLabel)
                    _navStack.Pop();
                if (_navStack.Count > 0) _navStack.Pop();
                ShowData(captured, capturedLabel);
            };
            BreadcrumbPanel.Children.Add(link);
            BreadcrumbPanel.Children.Add(new TextBlock
            {
                Text = " > ",
                Foreground = _theme.TextMutedBrush,
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 13
            });
        }

        BreadcrumbPanel.Children.Add(new TextBlock
        {
            Text = currentLabel,
            Foreground = _theme.TextPrimaryBrush,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        });
    }

    private DataGridColumn CreateColumn(string col)
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new Binding($"[{col}]") { Mode = BindingMode.OneWay });
        factory.SetValue(TextBlock.CursorProperty, Cursors.Arrow);
        var template = new DataTemplate { VisualTree = factory };

        // Create a second factory for expandable cells styled as hyperlinks
        var linkFactory = new FrameworkElementFactory(typeof(TextBlock));
        linkFactory.SetBinding(TextBlock.TextProperty, new Binding($"[{col}]") { Mode = BindingMode.OneWay });
        linkFactory.SetValue(TextBlock.ForegroundProperty, _theme.AccentBrush);
        linkFactory.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
        linkFactory.SetValue(TextBlock.CursorProperty, Cursors.Hand);
        linkFactory.AddHandler(TextBlock.MouseLeftButtonUpEvent, new MouseButtonEventHandler((sender, e) =>
        {
            if (DataGrid.CurrentItem is not RowData row) return;
            if (!row.TryGetValue(col, out var val) || val is not CellValue cell || !cell.IsExpandable) return;

            _navStack.Push((BreadcrumbPanel.Tag as string ?? "Root", _currentData));
            var childRecords = new List<JsonElement>();
            if (cell.Raw.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cell.Raw.EnumerateArray())
                    childRecords.Add(item);
            }
            else if (cell.Raw.ValueKind == JsonValueKind.Object)
            {
                childRecords.Add(cell.Raw);
            }
            ShowData(childRecords, col);
        }));
        var linkTemplate = new DataTemplate { VisualTree = linkFactory };

        var templateColumn = new DataGridTemplateColumn
        {
            Header = col,
            MaxWidth = 400,
            CellTemplateSelector = new ExpandableCellTemplateSelector(col, template, linkTemplate)
        };

        return templateColumn;
    }
}

/// <summary>
/// Row wrapper with a string-keyed indexer that returns display text for WPF binding.
/// Stores CellValue objects internally, returns Display string via indexer for DataGrid columns.
/// </summary>
public class RowData : Dictionary<string, CellValue?>
{
    /// <summary>
    /// Indexer override that returns the Display string for binding, while preserving
    /// the underlying CellValue for drill-down on double-click.
    /// </summary>
    public new object? this[string key]
    {
        get => base.TryGetValue(key, out var cell) ? cell?.Display : null;
        set => base[key] = value as CellValue;
    }

    /// <summary>
    /// Gets the underlying CellValue for a column (used for drill-down).
    /// </summary>
    public CellValue? GetCellValue(string key) =>
        base.TryGetValue(key, out var cell) ? cell : null;

    public bool TryGetValue(string key, out object? value)
    {
        if (base.TryGetValue(key, out var cell))
        {
            value = cell;
            return true;
        }
        value = null;
        return false;
    }
}

public class ExpandableCellTemplateSelector : DataTemplateSelector
{
    private readonly string _column;
    private readonly DataTemplate _normalTemplate;
    private readonly DataTemplate _linkTemplate;

    public ExpandableCellTemplateSelector(string column, DataTemplate normalTemplate, DataTemplate linkTemplate)
    {
        _column = column;
        _normalTemplate = normalTemplate;
        _linkTemplate = linkTemplate;
    }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is RowData row &&
            row.TryGetValue(_column, out var val) &&
            val is CellValue cell &&
            cell.IsExpandable)
        {
            return _linkTemplate;
        }
        return _normalTemplate;
    }
}