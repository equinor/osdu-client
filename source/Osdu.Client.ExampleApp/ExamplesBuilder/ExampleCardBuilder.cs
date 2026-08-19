using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Builds the main example run card with parameters, source code, and run button.
/// </summary>
internal sealed class ExampleCardBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    public event Func<IExample, System.Threading.Tasks.Task>? RunRequested;
    public event Action<string>? ValidationFailed;

    public Border Build(IExample example)
    {
        var card = new Border
        {
            Background = theme.CardBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 3,
                Opacity = theme.ShadowOpacity,
                Color = Colors.Black,
                Direction = 270
            }
        };

        var stack = new StackPanel();

        // Parameters
        var paramBuilder = new ParameterPanelBuilder(theme);
        var paramResult = paramBuilder.Build(example);

        // Source code
        var sourceBuilder = new SourceCodePanelBuilder(theme);
        var sourceResult = sourceBuilder.Build(example);

        // Action row (Run button only)
        var actionRow = BuildActionRow(example, paramResult);
        stack.Children.Add(actionRow);

        // TabControl for Parameters and Source Code
        var tabControl = BuildTabControl(example, paramResult, sourceResult);
        stack.Children.Add(tabControl);

        card.Child = stack;
        return card;
    }

    private StackPanel BuildActionRow(IExample example, ParameterPanelResult? paramResult)
    {
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal };

        var runButton = new RunButtonBuilder(theme).Build();

        // Wire run button
        runButton.Click += async (_, _) =>
        {
            if (paramResult != null)
            {
                var error = paramResult.ApplyValues(example);
                if (error != null)
                {
                    ValidationFailed?.Invoke(error);
                    return;
                }
            }
            if (RunRequested != null)
                await RunRequested(example);
        };

        actionRow.Children.Add(runButton);
        return actionRow;
    }

    private TabControl BuildTabControl(IExample example, ParameterPanelResult? paramResult, SourceCodePanelResult sourceResult)
    {
        var tabControl = new TabControl
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(0)
        };

        tabControl.Style = CreateTabControlStyle();

        // Parameters tab
        if (paramResult != null)
        {
            var paramTab = new TabItem
            {
                Header = $"⚙️ Parameters ({example.Parameters.Count})",
                Style = CreateTabItemStyle()
            };

            var paramContent = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            paramResult.Header.Visibility = Visibility.Visible;
            paramResult.Content.Visibility = Visibility.Visible;
            paramContent.Children.Add(paramResult.Header);
            paramContent.Children.Add(paramResult.Content);
            paramTab.Content = paramContent;

            tabControl.Items.Add(paramTab);
        }

        // Source Code tab
        var sourceTab = new TabItem
        {
            Header = "💻 View Source Code",
            Style = CreateTabItemStyle()
        };

        var sourceContent = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        sourceResult.Header.Visibility = Visibility.Visible;
        sourceResult.Content.Visibility = Visibility.Visible;
        sourceContent.Children.Add(sourceResult.Header);
        sourceContent.Children.Add(sourceResult.Content);
        sourceTab.Content = sourceContent;

        tabControl.Items.Add(sourceTab);

        // Select first tab
        tabControl.SelectedIndex = 0;

        return tabControl;
    }

    private SolidColorBrush TabStripBackground => theme.IsDark
        ? new SolidColorBrush(Color.FromRgb(30, 30, 36))
        : new SolidColorBrush(Color.FromRgb(228, 230, 238));

    private SolidColorBrush TabItemSelectedBackground => theme.IsDark
        ? new SolidColorBrush(Color.FromRgb(50, 50, 60))
        : new SolidColorBrush(Color.FromRgb(255, 255, 255));

    private Style CreateTabControlStyle()
    {
        var style = new Style(typeof(TabControl));
        style.Setters.Add(new Setter(TabControl.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(TabControl.BorderThicknessProperty, new Thickness(0)));

        var template = new ControlTemplate(typeof(TabControl));

        // Use DockPanel approach for simpler template
        var dock = new FrameworkElementFactory(typeof(DockPanel));

        var tabPanelBorder = new FrameworkElementFactory(typeof(Border));
        tabPanelBorder.SetValue(DockPanel.DockProperty, Dock.Top);
        tabPanelBorder.SetValue(Border.BackgroundProperty, TabStripBackground);
        tabPanelBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        tabPanelBorder.SetValue(Border.PaddingProperty, new Thickness(4));
        tabPanelBorder.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 0));

        var tabPanel = new FrameworkElementFactory(typeof(TabPanel));
        tabPanel.SetValue(TabPanel.IsItemsHostProperty, true);
        tabPanelBorder.AppendChild(tabPanel);
        dock.AppendChild(tabPanelBorder);

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
        contentPresenter.SetValue(ContentPresenter.MarginProperty, new Thickness(0, 8, 0, 0));
        dock.AppendChild(contentPresenter);

        template.VisualTree = dock;
        style.Setters.Add(new Setter(TabControl.TemplateProperty, template));

        return style;
    }

    private Style CreateTabItemStyle()
    {
        var style = new Style(typeof(TabItem));

        style.Setters.Add(new Setter(TabItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(TabItem.ForegroundProperty, theme.TextMutedBrush));
        style.Setters.Add(new Setter(TabItem.FontSizeProperty, 12.0));
        style.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(TabItem.CursorProperty, System.Windows.Input.Cursors.Hand));
        style.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(14, 8, 14, 8)));
        style.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(2, 0, 2, 0)));

        var template = new ControlTemplate(typeof(TabItem));
        var border = new FrameworkElementFactory(typeof(Border), "TabBorder");
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new Thickness(14, 8, 14, 8));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        template.VisualTree = border;

        // Selected trigger — subtle elevated background, accent text (not filled accent like Run button)
        var selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, theme.TextPrimaryBrush));
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, TabItemSelectedBackground) { TargetName = "TabBorder" });

        // MouseOver trigger (when not selected)
        var hoverTrigger = new MultiTrigger();
        hoverTrigger.Conditions.Add(new Condition(TabItem.IsMouseOverProperty, true));
        hoverTrigger.Conditions.Add(new Condition(TabItem.IsSelectedProperty, false));
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, theme.Accent.R, theme.Accent.G, theme.Accent.B))) { TargetName = "TabBorder" });
        hoverTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, theme.AccentBrush));

        template.Triggers.Add(selectedTrigger);
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(TabItem.TemplateProperty, template));

        return style;
    }
}
