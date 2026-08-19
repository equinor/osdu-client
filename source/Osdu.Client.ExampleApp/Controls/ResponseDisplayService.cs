using System.Collections;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp.Controls;

/// <summary>
/// Provides the ability for examples to display data in the response area's DataGrid
/// instead of the default TextBox.
/// </summary>
public static class ResponseDisplayService
{
    private static DataGrid? _responseDataGrid;
    private static Grid? _responseDataGridPanel;
    private static TextBlock? _itemCountText;
    private static Border? _statusBar;
    private static TextBox? _responseTextBox;
    private static AppTheme? _currentTheme;
    private static int _totalItemCount;

    /// <summary>
    /// Initializes the service with the response area controls.
    /// Must be called once during window setup.
    /// </summary>
    public static void Initialize(TextBox responseTextBox, DataGrid responseDataGrid, Grid dataGridPanel, TextBlock itemCountText, Border statusBar, AppTheme theme)
    {
        _responseTextBox = responseTextBox;
        _responseDataGrid = responseDataGrid;
        _responseDataGridPanel = dataGridPanel;
        _itemCountText = itemCountText;
        _statusBar = statusBar;
        _currentTheme = theme;
        ApplyDataGridTheme();

        _responseDataGrid.SelectionChanged += OnSelectionChanged;
        _responseDataGrid.AutoGeneratingColumn += OnAutoGeneratingColumn;
    }

    /// <summary>
    /// Returns true if the DataGrid is currently visible.
    /// </summary>
    public static bool IsDataGridVisible => _responseDataGridPanel?.Visibility == Visibility.Visible;

    /// <summary>
    /// Updates the theme for the DataGrid.
    /// </summary>
    public static void UpdateTheme(AppTheme theme)
    {
        _currentTheme = theme;
        ApplyDataGridTheme();
        ApplyStatusBarTheme();
    }

    /// <summary>
    /// Switches the response area to display a DataGrid with the provided items.
    /// </summary>
    public static void ShowDataGrid(IEnumerable itemsSource)
    {
        if (_responseDataGrid is null || _responseTextBox is null || _responseDataGridPanel is null) return;

        _responseDataGrid.ItemsSource = itemsSource;
        _responseTextBox.Visibility = Visibility.Collapsed;
        _responseDataGridPanel.Visibility = Visibility.Visible;

        // Count items
        _totalItemCount = 0;
        foreach (var _ in itemsSource)
            _totalItemCount++;

        UpdateItemCountText(0);
    }

    /// <summary>
    /// Switches the response area back to the TextBox display.
    /// </summary>
    public static void ShowTextBox()
    {
        if (_responseDataGrid is null || _responseTextBox is null || _responseDataGridPanel is null) return;

        _responseDataGridPanel.Visibility = Visibility.Collapsed;
        _responseTextBox.Visibility = Visibility.Visible;
        _responseDataGrid.ItemsSource = null;
    }

    /// <summary>
    /// Copies the selected DataGrid cells to clipboard as tab-separated text with headers.
    /// </summary>
    public static void CopySelectedToClipboard()
    {
        if (_responseDataGrid is null || _responseDataGrid.SelectedCells.Count == 0) return;

        // Group selected cells by row item, preserving column order
        var columns = _responseDataGrid.Columns.OrderBy(c => c.DisplayIndex).ToList();
        var selectedColumns = columns.Where(c => _responseDataGrid.SelectedCells.Any(cell => cell.Column == c)).ToList();

        var rowItems = _responseDataGrid.SelectedCells
            .Select(c => c.Item)
            .Distinct()
            .ToList();

        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join('\t', selectedColumns.Select(c => c.Header?.ToString() ?? "")));

        // Data rows
        foreach (var item in rowItems)
        {
            var values = new List<string>();
            foreach (var col in selectedColumns)
            {
                var cellContent = col.OnCopyingCellClipboardContent(item);
                values.Add(cellContent?.ToString() ?? "");
            }
            sb.AppendLine(string.Join('\t', values));
        }

        Clipboard.SetText(sb.ToString());
    }

    private static void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var propertyType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;

        // For numeric types, replace the default text column with one that uses a custom sort comparer
        if (IsNumericType(propertyType))
        {
            var textColumn = (DataGridTextColumn)e.Column;
            textColumn.SortMemberPath = e.PropertyName;

            // Apply a custom sort description via the Sorting event isn't possible here,
            // so we replace with a column that binds to the actual typed property
            var newColumn = new DataGridTextColumn
            {
                Header = e.Column.Header,
                Binding = new Binding(e.PropertyName),
                SortMemberPath = e.PropertyName,
                CanUserSort = true
            };

            e.Column = newColumn;
        }
    }

    /// <summary>
    /// Determines if a type is numeric for sorting purposes.
    /// </summary>
    private static bool IsNumericType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(double) ||
        type == typeof(float) || type == typeof(decimal) || type == typeof(short) ||
        type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
        type == typeof(ushort);

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_responseDataGrid is null) return;

        var selectedRowCount = _responseDataGrid.SelectedCells
            .Select(c => c.Item)
            .Distinct()
            .Count();
        UpdateItemCountText(selectedRowCount);
    }

    private static void UpdateItemCountText(int selectedCount)
    {
        if (_itemCountText is null) return;

        _itemCountText.Text = selectedCount > 0
            ? $"{_totalItemCount} items  •  {selectedCount} selected"
            : $"{_totalItemCount} items";
    }

    private static void ApplyStatusBarTheme()
    {
        if (_statusBar is null || _itemCountText is null || _currentTheme is null) return;

        _statusBar.BorderBrush = _currentTheme.BorderBrush;
        _statusBar.Background = _currentTheme.TagBrush;
        _itemCountText.Foreground = _currentTheme.TextSecondaryBrush;
    }

    private static void ApplyDataGridTheme()
    {
        if (_responseDataGrid is null || _currentTheme is null) return;

        var theme = _currentTheme;

        _responseDataGrid.AutoGenerateColumns = true;
        _responseDataGrid.IsReadOnly = true;
        _responseDataGrid.CanUserSortColumns = true;
        _responseDataGrid.CanUserReorderColumns = true;
        _responseDataGrid.CanUserResizeColumns = true;
        _responseDataGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _responseDataGrid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        _responseDataGrid.BorderThickness = new Thickness(0);
        _responseDataGrid.Background = Brushes.Transparent;
        _responseDataGrid.Foreground = theme.TextPrimaryBrush;
        _responseDataGrid.RowBackground = theme.CardBrush;
        _responseDataGrid.AlternatingRowBackground = new SolidColorBrush(theme.CardHover);
        _responseDataGrid.HorizontalGridLinesBrush = theme.BorderBrush;
        _responseDataGrid.VerticalGridLinesBrush = theme.BorderBrush;
        _responseDataGrid.FontFamily = new FontFamily("Cascadia Code, Consolas, monospace");
        _responseDataGrid.FontSize = 12;
        _responseDataGrid.RowHeight = 30;
        _responseDataGrid.SelectionMode = DataGridSelectionMode.Extended;
        _responseDataGrid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;

        // Column header style
        var columnHeaderStyle = new Style(typeof(DataGridColumnHeader));
        columnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, theme.TagBrush));
        columnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, theme.TextPrimaryBrush));
        columnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        columnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        columnHeaderStyle.Setters.Add(new Setter(Control.BorderBrushProperty, theme.BorderBrush));
        columnHeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        _responseDataGrid.ColumnHeaderStyle = columnHeaderStyle;

        // Cell style
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

        var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, theme.AccentBrush));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        cellStyle.Triggers.Add(selectedTrigger);
        _responseDataGrid.CellStyle = cellStyle;

        // Row style with hover
        var rowStyle = new Style(typeof(DataGridRow));
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(theme.CardHover)));
        rowStyle.Triggers.Add(hoverTrigger);
        _responseDataGrid.RowStyle = rowStyle;

        ApplyStatusBarTheme();
    }
}
