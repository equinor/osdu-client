using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Centralized theme definition supporting light and dark modes.
/// Colors are modeled after Visual Studio 2026's flat design language.
/// </summary>
public class AppTheme
{
    public static AppTheme Dark { get; } = new()
    {
        IsDark = true,
        // VS Dark: main background #1E1E1E, tool windows #252526, editor #1E1E1E
        Surface = Color.FromRgb(30, 30, 30),           // #1E1E1E - editor/main bg
        Sidebar = Color.FromRgb(37, 37, 38),           // #252526 - tool window bg
        Card = Color.FromRgb(37, 37, 38),              // #252526 - panel/card bg
        CardHover = Color.FromRgb(45, 45, 48),         // #2D2D30 - hover highlight
        Border = Color.FromRgb(63, 63, 70),            // #3F3F46 - borders
        Input = Color.FromRgb(51, 51, 55),             // #333337 - input bg
        InputField = Color.FromRgb(37, 37, 38),        // #252526 - text field bg
        ResponseBg = Color.FromRgb(30, 30, 30),        // #1E1E1E
        Tag = Color.FromRgb(45, 45, 48),               // #2D2D30 - tag/header bg
        TextPrimary = Color.FromRgb(220, 220, 220),    // #DCDCDC - primary text
        TextSecondary = Color.FromRgb(153, 153, 153),  // #999999 - secondary text
        TextMuted = Color.FromRgb(100, 100, 100),      // #646464 - disabled/muted
        Accent = Color.FromRgb(0, 122, 204),           // #007ACC - VS blue accent
        AccentHover = Color.FromRgb(28, 151, 234),     // #1C97EA - accent hover
        Required = Color.FromRgb(241, 135, 135),       // error red
        ShadowOpacity = 0.0,                           // flat, no shadows
        ExpanderArrow = Color.FromRgb(176, 176, 176),  // #B0B0B0
        TabActive = Color.FromRgb(0, 122, 204),        // #007ACC - active tab indicator
        TabInactive = Color.FromRgb(45, 45, 48),       // #2D2D30 - inactive tab bg
        TabInactiveText = Color.FromRgb(153, 153, 153),// #999999
        ToolbarBg = Color.FromRgb(45, 45, 48),         // #2D2D30 - toolbar bg
        StatusBarBg = Color.FromRgb(0, 122, 204),      // #007ACC - VS status bar (blue)
        StatusBarText = Color.FromRgb(255, 255, 255),  // white text on status bar
        SplitterBg = Color.FromRgb(63, 63, 70),        // #3F3F46
        ButtonBg = Color.FromRgb(51, 51, 55),          // #333337
        ButtonHover = Color.FromRgb(63, 63, 70),       // #3F3F46
        ButtonPressed = Color.FromRgb(0, 122, 204),    // #007ACC
        SelectionBg = Color.FromRgb(38, 79, 120),      // #264F78 - selected row
        SelectionText = Color.FromRgb(255, 255, 255),
    };

    public static AppTheme Light { get; } = new()
    {
        IsDark = false,
        // VS Light: main background #F5F5F5, editor #FFFFFF, tool windows #F3F3F3
        Surface = Color.FromRgb(245, 245, 245),        // #F5F5F5 - main bg
        Sidebar = Color.FromRgb(243, 243, 243),        // #F3F3F3 - tool window bg
        Card = Color.FromRgb(255, 255, 255),           // #FFFFFF - panel/card bg
        CardHover = Color.FromRgb(232, 232, 236),      // #E8E8EC - hover
        Border = Color.FromRgb(204, 206, 219),         // #CCCEDB - borders
        Input = Color.FromRgb(245, 245, 245),          // #F5F5F5 - input bg
        InputField = Color.FromRgb(255, 255, 255),     // #FFFFFF - text field bg
        ResponseBg = Color.FromRgb(252, 252, 252),     // #FCFCFC
        Tag = Color.FromRgb(232, 232, 236),            // #E8E8EC - tag/header bg
        TextPrimary = Color.FromRgb(30, 30, 30),       // #1E1E1E - primary text
        TextSecondary = Color.FromRgb(104, 104, 104),  // #686868 - secondary text
        TextMuted = Color.FromRgb(160, 160, 160),      // #A0A0A0 - disabled/muted
        Accent = Color.FromRgb(0, 122, 204),           // #007ACC - VS blue accent
        AccentHover = Color.FromRgb(28, 151, 234),     // #1C97EA
        Required = Color.FromRgb(220, 50, 50),         // error red
        ShadowOpacity = 0.0,                           // flat, no shadows
        ExpanderArrow = Color.FromRgb(104, 104, 104),  // #686868
        TabActive = Color.FromRgb(0, 122, 204),        // #007ACC - active tab indicator
        TabInactive = Color.FromRgb(243, 243, 243),    // #F3F3F3
        TabInactiveText = Color.FromRgb(104, 104, 104),// #686868
        ToolbarBg = Color.FromRgb(238, 238, 242),      // #EEEEF2 - toolbar bg
        StatusBarBg = Color.FromRgb(104, 33, 122),     // #68217A - VS purple status bar
        StatusBarText = Color.FromRgb(255, 255, 255),  // white text on status bar
        SplitterBg = Color.FromRgb(204, 206, 219),     // #CCCEDB
        ButtonBg = Color.FromRgb(221, 221, 221),       // #DDDDDD
        ButtonHover = Color.FromRgb(201, 222, 245),    // #C9DEF5
        ButtonPressed = Color.FromRgb(0, 122, 204),    // #007ACC
        SelectionBg = Color.FromRgb(51, 153, 255),     // #3399FF
        SelectionText = Color.FromRgb(255, 255, 255),
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
    public Color AccentHover { get; init; }
    public Color Required { get; init; }
    public double ShadowOpacity { get; init; }
    public Color ExpanderArrow { get; init; }
    public Color TabActive { get; init; }
    public Color TabInactive { get; init; }
    public Color TabInactiveText { get; init; }
    public Color ToolbarBg { get; init; }
    public Color StatusBarBg { get; init; }
    public Color StatusBarText { get; init; }
    public Color SplitterBg { get; init; }
    public Color ButtonBg { get; init; }
    public Color ButtonHover { get; init; }
    public Color ButtonPressed { get; init; }
    public Color SelectionBg { get; init; }
    public Color SelectionText { get; init; }

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
    public SolidColorBrush AccentHoverBrush => new(AccentHover);
    public SolidColorBrush RequiredBrush => new(Required);
    public SolidColorBrush TabActiveBrush => new(TabActive);
    public SolidColorBrush TabInactiveBrush => new(TabInactive);
    public SolidColorBrush TabInactiveTextBrush => new(TabInactiveText);
    public SolidColorBrush ToolbarBgBrush => new(ToolbarBg);
    public SolidColorBrush StatusBarBgBrush => new(StatusBarBg);
    public SolidColorBrush StatusBarTextBrush => new(StatusBarText);
    public SolidColorBrush SplitterBgBrush => new(SplitterBg);
    public SolidColorBrush ButtonBgBrush => new(ButtonBg);
    public SolidColorBrush ButtonHoverBrush => new(ButtonHover);
    public SolidColorBrush ButtonPressedBrush => new(ButtonPressed);
    public SolidColorBrush SelectionBgBrush => new(SelectionBg);
    public SolidColorBrush SelectionTextBrush => new(SelectionText);

    // Font constants
    public static FontFamily FontFamily { get; } = new("Segoe UI");
    public static FontFamily MonoFontFamily { get; } = new("Cascadia Code,Consolas,Courier New");
    public static double FontSize => 13;
    public static double FontSizeSmall => 12;
    public static double FontSizeXSmall => 11;
    public static double FontSizeLarge => 14;

    /// <summary>
    /// Applies full theming to a DataGrid including headers, cells, selection, and hover styles.
    /// </summary>
    public void ApplyToDataGrid(DataGrid dataGrid)
    {
        dataGrid.Background = SurfaceBrush;
        dataGrid.Foreground = TextPrimaryBrush;
        dataGrid.RowBackground = CardBrush;
        dataGrid.AlternatingRowBackground = new SolidColorBrush(IsDark
            ? Color.FromRgb(34, 34, 34)
            : Color.FromRgb(248, 248, 248));
        dataGrid.HorizontalGridLinesBrush = BorderBrush;
        dataGrid.VerticalGridLinesBrush = BorderBrush;
        dataGrid.BorderBrush = BorderBrush;
        dataGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        dataGrid.BorderThickness = new Thickness(1);

        // Column header style — flat, no gradient
        var columnHeaderStyle = new Style(typeof(DataGridColumnHeader));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Tag)));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, TextPrimaryBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, FontSizeSmall));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontFamilyProperty, FontFamily));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 5, 8, 5)));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, BorderBrush));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        dataGrid.ColumnHeaderStyle = columnHeaderStyle;

        // Cell style — flat selection
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(8, 4, 8, 4)));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, TextPrimaryBrush));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));

        var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, SelectionBgBrush));
        selectedTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, SelectionTextBrush));
        selectedTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellStyle.Triggers.Add(selectedTrigger);

        dataGrid.CellStyle = cellStyle;

        // Row style — flat hover
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Brushes.Transparent));
        rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, TextPrimaryBrush));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));

        var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(CardHover)));
        rowStyle.Triggers.Add(hoverTrigger);

        var selectedRowTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selectedRowTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, SelectionBgBrush));
        selectedRowTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, SelectionTextBrush));
        rowStyle.Triggers.Add(selectedRowTrigger);

        dataGrid.RowStyle = rowStyle;

        // Row header style
        var rowHeaderStyle = new Style(typeof(DataGridRowHeader));
        rowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.BackgroundProperty, Brushes.Transparent));
        rowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.BorderBrushProperty, Brushes.Transparent));
        rowHeaderStyle.Setters.Add(new Setter(DataGridRowHeader.WidthProperty, 0.0));
        dataGrid.RowHeaderStyle = rowHeaderStyle;

        // ScrollBar theming
        ApplyScrollBarStyle(dataGrid);
    }

    /// <summary>
    /// Applies VS-style flat theming to a TabControl.
    /// </summary>
    public void ApplyToTabControl(TabControl tabControl)
    {
        tabControl.Background = SurfaceBrush;
        tabControl.BorderBrush = BorderBrush;
        tabControl.BorderThickness = new Thickness(0, 1, 0, 0);
        tabControl.Padding = new Thickness(0);

        var tabItemStyle = new Style(typeof(TabItem));
        tabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, TabInactiveBrush));
        tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, TabInactiveTextBrush));
        tabItemStyle.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0)));
        tabItemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(12, 6, 12, 6)));
        tabItemStyle.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(0)));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontSizeProperty, FontSizeSmall));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontFamilyProperty, FontFamily));

        var selectedTabTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selectedTabTrigger.Setters.Add(new Setter(TabItem.BackgroundProperty, SurfaceBrush));
        selectedTabTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, TextPrimaryBrush));
        selectedTabTrigger.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.SemiBold));
        tabItemStyle.Triggers.Add(selectedTabTrigger);

        var hoverTabTrigger = new MultiTrigger();
        hoverTabTrigger.Conditions.Add(new Condition(TabItem.IsMouseOverProperty, true));
        hoverTabTrigger.Conditions.Add(new Condition(TabItem.IsSelectedProperty, false));
        hoverTabTrigger.Setters.Add(new Setter(TabItem.BackgroundProperty, new SolidColorBrush(CardHover)));
        hoverTabTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, TextPrimaryBrush));
        tabItemStyle.Triggers.Add(hoverTabTrigger);

        tabControl.Resources[typeof(TabItem)] = tabItemStyle;
    }

    /// <summary>
    /// Applies VS-style flat theming to a ToolBar.
    /// </summary>
    public void ApplyToToolBar(ToolBar toolbar)
    {
        toolbar.Background = ToolbarBgBrush;
        toolbar.Foreground = TextPrimaryBrush;
        toolbar.BorderBrush = BorderBrush;
        toolbar.BorderThickness = new Thickness(0, 0, 0, 1);

        // Remove the toolbar overflow/grip chrome
        toolbar.Loaded += (s, _) =>
        {
            if (toolbar.Template.FindName("OverflowGrid", toolbar) is FrameworkElement overflow)
                overflow.Visibility = Visibility.Collapsed;
            if (toolbar.Template.FindName("MainPanelBorder", toolbar) is FrameworkElement border)
            {
                if (border is System.Windows.Controls.Border b)
                {
                    b.Background = ToolbarBgBrush;
                    b.BorderBrush = Brushes.Transparent;
                }
            }
        };
    }

    /// <summary>
    /// Applies VS-style theming to a StatusBar (blue in dark, purple in light — matching VS).
    /// </summary>
    public void ApplyToStatusBar(StatusBar statusBar)
    {
        statusBar.Background = StatusBarBgBrush;
        statusBar.Foreground = StatusBarTextBrush;
        statusBar.BorderBrush = Brushes.Transparent;
        statusBar.BorderThickness = new Thickness(0);
    }

    /// <summary>
    /// Applies VS-style flat theming to a GridSplitter.
    /// </summary>
    public void ApplyToSplitter(GridSplitter splitter)
    {
        splitter.Background = SplitterBgBrush;
        splitter.Width = 4;
        splitter.Margin = new Thickness(0);
    }

    /// <summary>
    /// Applies VS-style flat button styling.
    /// </summary>
    public void ApplyToButton(ButtonBase button)
    {
        button.Background = ButtonBgBrush;
        button.Foreground = TextPrimaryBrush;
        button.BorderBrush = BorderBrush;
        button.BorderThickness = new Thickness(1);
        button.Padding = new Thickness(8, 3, 8, 3);
    }

    /// <summary>
    /// Applies VS-style flat theming to a TextBox.
    /// </summary>
    public void ApplyToTextBox(TextBox textBox)
    {
        textBox.Background = InputFieldBrush;
        textBox.Foreground = TextPrimaryBrush;
        textBox.BorderBrush = BorderBrush;
        textBox.BorderThickness = new Thickness(1);
        textBox.CaretBrush = TextPrimaryBrush;
        textBox.SelectionBrush = AccentBrush;
    }

    /// <summary>
    /// Applies VS-style flat theming to a ComboBox.
    /// </summary>
    public void ApplyToComboBox(ComboBox comboBox)
    {
        comboBox.Background = InputFieldBrush;
        comboBox.Foreground = TextPrimaryBrush;
        comboBox.BorderBrush = BorderBrush;
        comboBox.BorderThickness = new Thickness(1);
    }

    /// <summary>
    /// Applies VS-style theming to a ProgressBar.
    /// </summary>
    public void ApplyToProgressBar(ProgressBar progressBar)
    {
        progressBar.Foreground = AccentBrush;
        progressBar.Background = new SolidColorBrush(IsDark
            ? Color.FromRgb(45, 45, 48)
            : Color.FromRgb(230, 230, 230));
        progressBar.BorderBrush = Brushes.Transparent;
        progressBar.BorderThickness = new Thickness(0);
    }

    /// <summary>
    /// Applies themed ScrollBar styles to a control's local resources so that
    /// scrollbars within it pick up the current theme colors.
    /// </summary>
    public void ApplyScrollBarStyle(FrameworkElement target)
    {
        var thumbBrush = new SolidColorBrush(IsDark
            ? Color.FromRgb(104, 104, 104)   // #686868
            : Color.FromRgb(193, 193, 193));  // #C1C1C1
        var thumbHoverBrush = new SolidColorBrush(IsDark
            ? Color.FromRgb(158, 158, 158)   // #9E9E9E
            : Color.FromRgb(145, 145, 145)); // #919191
        var trackBrush = new SolidColorBrush(IsDark
            ? Color.FromRgb(37, 37, 38)      // #252526
            : Color.FromRgb(243, 243, 243)); // #F3F3F3

        // Thumb style with hover trigger
        var thumbStyle = new Style(typeof(Thumb));
        var thumbTemplate = CreateScrollBarThumbTemplate(thumbBrush, thumbHoverBrush);
        thumbStyle.Setters.Add(new Setter(Control.TemplateProperty, thumbTemplate));

        // ScrollBar style — flat, no buttons, VS-like thin bar
        var scrollBarStyle = new Style(typeof(ScrollBar));
        scrollBarStyle.Setters.Add(new Setter(Control.BackgroundProperty, trackBrush));
        scrollBarStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        scrollBarStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 12.0));
        scrollBarStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 12.0));

        // For horizontal scrollbar
        var horizontalTrigger = new Trigger
        {
            Property = ScrollBar.OrientationProperty,
            Value = Orientation.Horizontal
        };
        horizontalTrigger.Setters.Add(new Setter(FrameworkElement.WidthProperty, double.NaN));
        horizontalTrigger.Setters.Add(new Setter(FrameworkElement.HeightProperty, 12.0));
        horizontalTrigger.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 12.0));
        scrollBarStyle.Triggers.Add(horizontalTrigger);

        // ScrollViewer style
        var scrollViewerStyle = new Style(typeof(ScrollViewer));
        scrollViewerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));

        target.Resources[typeof(ScrollBar)] = scrollBarStyle;
        target.Resources[typeof(Thumb)] = thumbStyle;
        target.Resources[typeof(ScrollViewer)] = scrollViewerStyle;
    }

    private static ControlTemplate CreateScrollBarThumbTemplate(
        SolidColorBrush normalBrush, SolidColorBrush hoverBrush)
    {
        var template = new ControlTemplate(typeof(Thumb));

        var borderFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, normalBrush);
        borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(3));
        borderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
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
