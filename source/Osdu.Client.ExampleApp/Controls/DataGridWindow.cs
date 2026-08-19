using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp.Controls;

/// <summary>
/// A themed popup window that displays data in a styled DataGrid.
/// </summary>
public class DataGridWindow : Window
{
    public DataGridWindow(string title, IEnumerable itemsSource, AppTheme theme)
    {
        Title = title;
        Width = 1000;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = theme.SurfaceBrush;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = theme.TextPrimaryBrush,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // DataGrid
        var dataGrid = new DataGrid
        {
            ItemsSource = itemsSource,
            AutoGenerateColumns = true,
            IsReadOnly = true,
            CanUserSortColumns = true,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            BorderThickness = new Thickness(1),
            BorderBrush = theme.BorderBrush,
            Background = theme.CardBrush,
            Foreground = theme.TextPrimaryBrush,
            RowBackground = theme.CardBrush,
            AlternatingRowBackground = new SolidColorBrush(theme.CardHover),
            HorizontalGridLinesBrush = theme.BorderBrush,
            VerticalGridLinesBrush = theme.BorderBrush,
            FontSize = 13,
            RowHeight = 32,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
        };

        // Column header style
        var columnHeaderStyle = new Style(typeof(DataGridColumnHeader));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, theme.TagBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, theme.TextPrimaryBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 6, 8, 6)));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, theme.BorderBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        dataGrid.ColumnHeaderStyle = columnHeaderStyle;

        // Cell style
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(8, 4, 8, 4)));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));

        // Selected row highlight
        var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, theme.AccentBrush));
        selectedTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.White));
        cellStyle.Triggers.Add(selectedTrigger);

        dataGrid.CellStyle = cellStyle;

        // Row style
        var rowStyle = new Style(typeof(DataGridRow));
        var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(theme.CardHover)));
        rowStyle.Triggers.Add(hoverTrigger);
        dataGrid.RowStyle = rowStyle;

        Grid.SetRow(dataGrid, 1);
        grid.Children.Add(dataGrid);

        Content = grid;
    }
}
