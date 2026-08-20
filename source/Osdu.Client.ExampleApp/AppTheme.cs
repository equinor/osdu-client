using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Centralized theme definition supporting light and dark modes.
/// </summary>
public class AppTheme
{
    public static AppTheme Dark { get; } = new()
    {
        IsDark = true,
        Surface = Color.FromRgb(27, 27, 31),
        Sidebar = Color.FromRgb(20, 20, 24),
        Card = Color.FromRgb(35, 35, 41),
        CardHover = Color.FromRgb(42, 42, 50),
        Border = Color.FromRgb(58, 58, 68),
        Input = Color.FromRgb(27, 27, 31),
        InputField = Color.FromRgb(22, 22, 26),
        ResponseBg = Color.FromRgb(24, 24, 28),
        Tag = Color.FromRgb(45, 45, 55),
        TextPrimary = Color.FromRgb(232, 232, 237),
        TextSecondary = Color.FromRgb(144, 144, 160),
        TextMuted = Color.FromRgb(100, 100, 120),
        Accent = Color.FromRgb(108, 142, 239),
        Required = Color.FromRgb(255, 100, 100),
        ShadowOpacity = 0.15,
        ExpanderArrow = Color.FromRgb(180, 180, 200)
    };

    public static AppTheme Light { get; } = new()
    {
        IsDark = false,
        Surface = Color.FromRgb(248, 249, 251),
        Sidebar = Color.FromRgb(255, 255, 255),
        Card = Color.FromRgb(255, 255, 255),
        CardHover = Color.FromRgb(245, 246, 250),
        Border = Color.FromRgb(218, 220, 230),
        Input = Color.FromRgb(245, 246, 250),
        InputField = Color.FromRgb(255, 255, 255),
        ResponseBg = Color.FromRgb(250, 251, 253),
        Tag = Color.FromRgb(233, 235, 242),
        TextPrimary = Color.FromRgb(32, 33, 40),
        TextSecondary = Color.FromRgb(90, 95, 115),
        TextMuted = Color.FromRgb(130, 135, 150),
        Accent = Color.FromRgb(75, 110, 220),
        Required = Color.FromRgb(220, 50, 50),
        ShadowOpacity = 0.08,
        ExpanderArrow = Color.FromRgb(90, 95, 115)
    };

    public bool IsDark { get; init; }
    public Color Surface { get; init; }
    public Color Sidebar { get; init; }
    public Color Card { get; init; }
    public Color CardHover { get; init; }
    public Color Border { get; init; }
    public Color Input { get; init; }
    public Color InputField { get; init; }
    public Color ResponseBg { get; init; }
    public Color Tag { get; init; }
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextMuted { get; init; }
    public Color Accent { get; init; }
    public Color Required { get; init; }
    public double ShadowOpacity { get; init; }
    public Color ExpanderArrow { get; init; }

    // Convenience brush accessors
    public SolidColorBrush SurfaceBrush => new(Surface);
    public SolidColorBrush SidebarBrush => new(Sidebar);
    public SolidColorBrush CardBrush => new(Card);
    public SolidColorBrush BorderBrush => new(Border);
    public SolidColorBrush InputBrush => new(Input);
    public SolidColorBrush InputFieldBrush => new(InputField);
    public SolidColorBrush ResponseBgBrush => new(ResponseBg);
    public SolidColorBrush TagBrush => new(Tag);
    public SolidColorBrush TextPrimaryBrush => new(TextPrimary);
    public SolidColorBrush TextSecondaryBrush => new(TextSecondary);
    public SolidColorBrush TextMutedBrush => new(TextMuted);
    public SolidColorBrush AccentBrush => new(Accent);
    public SolidColorBrush RequiredBrush => new(Required);

    /// <summary>
    /// Applies full theming to a DataGrid including headers, cells, selection, and hover styles.
    /// </summary>
    public void ApplyToDataGrid(DataGrid dataGrid)
    {
        dataGrid.Background = SurfaceBrush;
        dataGrid.Foreground = TextPrimaryBrush;
        dataGrid.RowBackground = CardBrush;
        dataGrid.AlternatingRowBackground = new SolidColorBrush(Surface);
        dataGrid.HorizontalGridLinesBrush = BorderBrush;
        dataGrid.VerticalGridLinesBrush = BorderBrush;
        dataGrid.BorderBrush = BorderBrush;

        // Column header style
        var columnHeaderStyle = new Style(typeof(DataGridColumnHeader));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, TagBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, TextPrimaryBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 6, 8, 6)));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, BorderBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        dataGrid.ColumnHeaderStyle = columnHeaderStyle;

        // Cell style with selection colors
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(8, 4, 8, 4)));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, TextPrimaryBrush));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));

        var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, AccentBrush));
        selectedTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(Colors.White)));
        selectedTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, AccentBrush));
        cellStyle.Triggers.Add(selectedTrigger);

        dataGrid.CellStyle = cellStyle;

        // Row style with hover
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, CardBrush));
        rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, TextPrimaryBrush));

        var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(CardHover)));
        rowStyle.Triggers.Add(hoverTrigger);

        var selectedRowTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selectedRowTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, AccentBrush));
        selectedRowTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new SolidColorBrush(Colors.White)));
        rowStyle.Triggers.Add(selectedRowTrigger);

        dataGrid.RowStyle = rowStyle;

        // Row header style (hides the row header gripper area)
        var rowHeaderStyle = new Style(typeof(DataGridRowHeader));
        rowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.BackgroundProperty, CardBrush));
        rowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.BorderBrushProperty, BorderBrush));
        dataGrid.RowHeaderStyle = rowHeaderStyle;

        // ScrollBar theming
        ApplyScrollBarStyle(dataGrid);
    }

    /// <summary>
    /// Applies themed ScrollBar styles to a control's local resources so that
    /// scrollbars within it pick up the current theme colors.
    /// </summary>
    public void ApplyScrollBarStyle(FrameworkElement target)
    {
        var thumbBrush = new SolidColorBrush(IsDark
            ? Color.FromRgb(80, 80, 95)
            : Color.FromRgb(180, 182, 195));
        var thumbHoverBrush = new SolidColorBrush(IsDark
            ? Color.FromRgb(110, 110, 130)
            : Color.FromRgb(150, 152, 165));
        var trackBrush = new SolidColorBrush(IsDark
            ? Color.FromRgb(30, 30, 35)
            : Color.FromRgb(240, 241, 245));

        // Thumb style with hover trigger
        var thumbStyle = new Style(typeof(System.Windows.Controls.Primitives.Thumb));
        var thumbTemplate = CreateScrollBarThumbTemplate(thumbBrush, thumbHoverBrush);
        thumbStyle.Setters.Add(new Setter(Control.TemplateProperty, thumbTemplate));

        // ScrollBar style — color only, no custom template to avoid IAddChild issues
        var scrollBarStyle = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
        scrollBarStyle.Setters.Add(new Setter(Control.BackgroundProperty, trackBrush));
        scrollBarStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        scrollBarStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
        scrollBarStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 10.0));

        // For horizontal scrollbar
        var horizontalTrigger = new Trigger
        {
            Property = System.Windows.Controls.Primitives.ScrollBar.OrientationProperty,
            Value = System.Windows.Controls.Orientation.Horizontal
        };
        horizontalTrigger.Setters.Add(new Setter(FrameworkElement.WidthProperty, double.NaN));
        horizontalTrigger.Setters.Add(new Setter(FrameworkElement.HeightProperty, 10.0));
        horizontalTrigger.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 10.0));
        scrollBarStyle.Triggers.Add(horizontalTrigger);

        // ScrollViewer style to remove extra chrome
        var scrollViewerStyle = new Style(typeof(ScrollViewer));
        scrollViewerStyle.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceBrush));

        target.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollBarStyle;
        target.Resources[typeof(System.Windows.Controls.Primitives.Thumb)] = thumbStyle;
        target.Resources[typeof(ScrollViewer)] = scrollViewerStyle;
    }

    private static ControlTemplate CreateScrollBarTemplate(SolidColorBrush trackBrush, bool horizontal = false)
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ScrollBar));

        var borderFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, trackBrush);
        borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(4));

        var trackFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Track));
        trackFactory.Name = "PART_Track";
        trackFactory.SetValue(System.Windows.Controls.Primitives.Track.IsDirectionReversedProperty, !horizontal);

        var thumbFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Thumb));
        thumbFactory.SetValue(FrameworkElement.MinHeightProperty, horizontal ? 0.0 : 20.0);
        thumbFactory.SetValue(FrameworkElement.MinWidthProperty, horizontal ? 20.0 : 0.0);

        // Assign the Thumb to the Track via the factory tree
        trackFactory.AppendChild(thumbFactory);
        borderFactory.AppendChild(trackFactory);

        template.VisualTree = borderFactory;
        return template;
    }

    private static ControlTemplate CreateScrollBarThumbTemplate(
        SolidColorBrush normalBrush, SolidColorBrush hoverBrush)
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb));

        var borderFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, normalBrush);
        borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
        borderFactory.Name = "ThumbBorder";

        template.VisualTree = borderFactory;

        // Hover trigger
        var hoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, hoverBrush)
        {
            TargetName = "ThumbBorder"
        });
        template.Triggers.Add(hoverTrigger);

        return template;
    }
}
