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
        FontFamily = AppTheme.FontFamily;
        FontSize = AppTheme.FontSize;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = new TextBlock
        {
            Text = title,
            FontSize = AppTheme.FontSizeLarge + 4,
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
            RowHeight = 32,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
        };

        theme.ApplyToDataGrid(dataGrid);

        Grid.SetRow(dataGrid, 1);
        grid.Children.Add(dataGrid);

        Content = grid;
    }
}
