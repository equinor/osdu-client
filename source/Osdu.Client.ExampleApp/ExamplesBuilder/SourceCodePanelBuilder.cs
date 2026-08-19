using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

internal sealed record SourceCodePanelResult(DockPanel Header, Border Content);

internal sealed class SourceCodePanelBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");
    private const double DefaultFontSize = 12;
    private const double MinFontSize = 8;
    private const double MaxFontSize = 24;
    private const double FontSizeStep = 2;

    public SourceCodePanelResult Build(IExample example)
    {
        var sourceTextBox = new TextBox
        {
            Text = example.SourceCode,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            Foreground = theme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            FontFamily = MonoFont,
            FontSize = DefaultFontSize,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var heading = new DockPanel
        {
            Margin = new Thickness(0, 18, 0, 10),
            Visibility = Visibility.Collapsed
        };

        // Right-aligned actions panel
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actionsPanel, Dock.Right);

        // Font size label
        var fontSizeLabel = new TextBlock
        {
            Text = $"{DefaultFontSize:0}px",
            Foreground = theme.TextMutedBrush,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };

        var decreaseButton = CreateHeaderButton("A−");
        var increaseButton = CreateHeaderButton("A+");

        decreaseButton.Click += (_, _) =>
        {
            if (sourceTextBox.FontSize - FontSizeStep >= MinFontSize)
            {
                sourceTextBox.FontSize -= FontSizeStep;
                fontSizeLabel.Text = $"{sourceTextBox.FontSize:0}px";
            }
        };

        increaseButton.Click += (_, _) =>
        {
            if (sourceTextBox.FontSize + FontSizeStep <= MaxFontSize)
            {
                sourceTextBox.FontSize += FontSizeStep;
                fontSizeLabel.Text = $"{sourceTextBox.FontSize:0}px";
            }
        };

        var resetFontButton = CreateHeaderButton("↺");
        resetFontButton.ToolTip = "Reset font size";
        resetFontButton.Click += (_, _) =>
        {
            sourceTextBox.FontSize = DefaultFontSize;
            fontSizeLabel.Text = $"{DefaultFontSize:0}px";
        };

        actionsPanel.Children.Add(decreaseButton);
        actionsPanel.Children.Add(fontSizeLabel);
        actionsPanel.Children.Add(increaseButton);
        actionsPanel.Children.Add(resetFontButton);

        // Fullscreen button
        var fullScreenButton = CreateHeaderButton("⛶");
        fullScreenButton.ToolTip = "View in fullscreen";
        fullScreenButton.Margin = new Thickness(8, 0, 0, 0);
        fullScreenButton.Click += (_, _) => OpenFullScreenWindow(example, sourceTextBox.FontSize);
        actionsPanel.Children.Add(fullScreenButton);

        var copyButton = CreateHeaderButton("📋 Copy");
        copyButton.Margin = new Thickness(8, 0, 0, 0);
        copyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(sourceTextBox.Text))
                Clipboard.SetText(sourceTextBox.Text);
        };
        actionsPanel.Children.Add(copyButton);

        heading.Children.Add(actionsPanel);

        var leftPanel = new StackPanel { Orientation = Orientation.Horizontal };
        leftPanel.Children.Add(new TextBlock
        {
            Text = "💻 Source Code",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var showAllCheckBox = new CheckBox
        {
            Content = "Show All",
            Foreground = theme.TextSecondaryBrush,
            FontSize = 12,
            Margin = new Thickness(12, 0, 0, 0),
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        showAllCheckBox.Checked += (_, _) => sourceTextBox.Text = example.FullSourceCode;
        showAllCheckBox.Unchecked += (_, _) => sourceTextBox.Text = example.SourceCode;
        leftPanel.Children.Add(showAllCheckBox);

        heading.Children.Add(leftPanel);

        var panel = new Border
        {
            Background = theme.InputBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 14),
            Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect
            {
                BlurRadius = 4, ShadowDepth = 1,
                Opacity = theme.ShadowOpacity * 0.5, Color = Colors.Black, Direction = 270
            },
            Child = sourceTextBox
        };

        return new SourceCodePanelResult(heading, panel);
    }

    private void OpenFullScreenWindow(IExample example, double initialFontSize)
    {
        var fsTextBox = new TextBox
        {
            Text = example.SourceCode,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            Foreground = theme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            FontFamily = MonoFont,
            FontSize = initialFontSize,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Toolbar
        var toolbar = new DockPanel
        {
            Background = new SolidColorBrush(theme.Card),
            Margin = new Thickness(0),
            LastChildFill = false
        };

        var leftPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16, 8, 0, 8)
        };
        DockPanel.SetDock(leftPanel, Dock.Left);

        leftPanel.Children.Add(new TextBlock
        {
            Text = "💻 Source Code",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var fsShowAllCheckBox = new CheckBox
        {
            Content = "Show Full Code",
            Foreground = theme.TextSecondaryBrush,
            FontSize = 12,
            Margin = new Thickness(16, 0, 0, 0),
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        fsShowAllCheckBox.Checked += (_, _) => fsTextBox.Text = example.FullSourceCode;
        fsShowAllCheckBox.Unchecked += (_, _) => fsTextBox.Text = example.SourceCode;
        leftPanel.Children.Add(fsShowAllCheckBox);

        toolbar.Children.Add(leftPanel);

        var toolbarActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(toolbarActions, Dock.Right);

        var fsFontSizeLabel = new TextBlock
        {
            Text = $"{initialFontSize:0}px",
            Foreground = theme.TextMutedBrush,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };

        var fsDecreaseButton = CreateHeaderButton("A−");
        var fsIncreaseButton = CreateHeaderButton("A+");

        fsDecreaseButton.Click += (_, _) =>
        {
            if (fsTextBox.FontSize - FontSizeStep >= MinFontSize)
            {
                fsTextBox.FontSize -= FontSizeStep;
                fsFontSizeLabel.Text = $"{fsTextBox.FontSize:0}px";
            }
        };

        fsIncreaseButton.Click += (_, _) =>
        {
            if (fsTextBox.FontSize + FontSizeStep <= MaxFontSize)
            {
                fsTextBox.FontSize += FontSizeStep;
                fsFontSizeLabel.Text = $"{fsTextBox.FontSize:0}px";
            }
        };

        var fsResetButton = CreateHeaderButton("↺");
        fsResetButton.ToolTip = "Reset font size";
        fsResetButton.Click += (_, _) =>
        {
            fsTextBox.FontSize = DefaultFontSize;
            fsFontSizeLabel.Text = $"{DefaultFontSize:0}px";
        };

        var fsCopyButton = CreateHeaderButton("📋 Copy");
        fsCopyButton.Margin = new Thickness(8, 0, 0, 0);
        fsCopyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(fsTextBox.Text))
                Clipboard.SetText(fsTextBox.Text);
        };

        var fsCloseButton = CreateHeaderButton("✕ Exit");
        fsCloseButton.Margin = new Thickness(8, 0, 0, 0);
        fsCloseButton.ToolTip = "Exit fullscreen (Esc)";

        toolbarActions.Children.Add(fsDecreaseButton);
        toolbarActions.Children.Add(fsFontSizeLabel);
        toolbarActions.Children.Add(fsIncreaseButton);
        toolbarActions.Children.Add(fsResetButton);
        toolbarActions.Children.Add(fsCopyButton);
        toolbarActions.Children.Add(fsCloseButton);
        toolbar.Children.Add(toolbarActions);

        var rootPanel = new DockPanel { Background = theme.SurfaceBrush };
        DockPanel.SetDock(toolbar, Dock.Top);
        rootPanel.Children.Add(toolbar);
        rootPanel.Children.Add(new Border
        {
            Background = theme.InputBrush,
            Padding = new Thickness(18, 14, 18, 14),
            Child = fsTextBox
        });

        var fullScreenWindow = new Window
        {
            Title = "Source Code",
            WindowStyle = WindowStyle.None,
            WindowState = WindowState.Maximized,
            Content = rootPanel,
            Owner = Application.Current.MainWindow
        };

        fsCloseButton.Click += (_, _) => fullScreenWindow.Close();
        fullScreenWindow.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                fullScreenWindow.Close();
        };

        fullScreenWindow.ShowDialog();
    }

    private Button CreateHeaderButton(string text) => new()
    {
        Content = text,
        Background = Brushes.Transparent,
        Foreground = theme.TextSecondaryBrush,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(6, 4, 6, 4),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand,
        VerticalAlignment = VerticalAlignment.Center
    };
}
