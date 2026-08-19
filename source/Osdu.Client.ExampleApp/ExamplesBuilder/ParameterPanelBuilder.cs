using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

internal sealed record ParameterPanelResult(
    StackPanel Header,
    StackPanel Content,
    List<(ExampleParameterInfo Info, FrameworkElement Control)> Controls,
    Dictionary<ExampleParameterInfo, object?> OriginalValues)
{
    /// <summary>
    /// Applies UI values to the example properties. Returns error message or null on success.
    /// </summary>
    public string? ApplyValues(IExample example)
    {
        foreach (var (info, control) in Controls)
        {
            try
            {
                var rawValue = ParameterConvert.GetValue(control);
                var converted = ParameterConvert.Convert(rawValue, info.PropertyType);
                info.SetValue(example, converted);
            }
            catch (Exception ex)
            {
                return $"Parameter '{info.DisplayName}' value could not converted. Error: {ex.Message}";
            }
        }

        foreach (var (info, _) in Controls)
        {
            if (info.Required)
            {
                var value = info.GetValue(example);
                if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
                    return $"Parameter '{info.DisplayName}' is required but has no value.";
            }
        }

        return null;
    }
}

internal sealed class ParameterPanelBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    public ParameterPanelResult? Build(IExample example)
    {
        var parameters = example.Parameters;
        if (parameters.Count == 0) return null;

        var controls = new List<(ExampleParameterInfo Info, FrameworkElement Control)>();
        var originalValues = new Dictionary<ExampleParameterInfo, object?>();

        // Header row
        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 18, 0, 10),
            Visibility = Visibility.Visible
        };

        headerRow.Children.Add(new TextBlock
        {
            Text = "⚙️ Parameters",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var resetBorder = new Border
        {
            Background = theme.TagBrush,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var resetButton = new Button
        {
            Content = "⟲ Reset",
            Background = Brushes.Transparent,
            Foreground = theme.TextMutedBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 3, 8, 3),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Reset parameters to default values"
        };
        resetBorder.Child = resetButton;
        headerRow.Children.Add(resetBorder);

        // Content
        var content = new StackPanel { Visibility = Visibility.Visible };
        Grid.SetIsSharedSizeScope(content, true);

        foreach (var param in parameters)
        {
            var currentValue = param.GetValue(example);
            originalValues[param] = currentValue;

            var paramCard = BuildParameterCard(param, currentValue);
            content.Children.Add(paramCard.Card);
            controls.Add((param, paramCard.Input));
        }

        // Reset handler
        var capturedControls = controls;
        var capturedDefaults = originalValues;
        var capturedExample = example;
        resetButton.Click += (_, _) =>
        {
            foreach (var (info, control) in capturedControls)
            {
                if (!capturedDefaults.TryGetValue(info, out var defaultValue)) continue;
                info.SetValue(capturedExample, defaultValue);

                if (control is TextBox tb)
                    tb.Text = defaultValue switch
                    {
                        string[] arr => string.Join(", ", arr),
                        _ => defaultValue?.ToString() ?? ""
                    };
                else if (control is CheckBox cb)
                    cb.IsChecked = defaultValue is true;
            }
        };

        return new ParameterPanelResult(headerRow, content, controls, originalValues);
    }

    private (Border Card, FrameworkElement Input) BuildParameterCard(ExampleParameterInfo param, object? currentValue)
    {
        var border = new Border
        {
            Background = theme.InputBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
            Effect = new DropShadowEffect
            {
                BlurRadius = 4, ShadowDepth = 1,
                Opacity = theme.ShadowOpacity * 0.5, Color = Colors.Black, Direction = 270
            }
        };

        var stack = new StackPanel();

        // Main row: label + type + input on same line using Grid for alignment
        var mainRow = new Grid();
        mainRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ParamLabel" });
        mainRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ParamType" });
        mainRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Label side
        var labelRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        labelRow.Children.Add(new TextBlock
        {
            Text = param.DisplayName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.5,
            FontFamily = MonoFont,
            Foreground = theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        if (param.Required)
        {
            labelRow.Children.Add(new TextBlock
            {
                Text = " *", Foreground = theme.RequiredBrush,
                FontWeight = FontWeights.Bold, FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        Grid.SetColumn(labelRow, 0);
        mainRow.Children.Add(labelRow);

        // Type badge
        var typeBadge = new Border
        {
            Background = theme.TagBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = ParameterConvert.FriendlyTypeName(param.PropertyType),
                Foreground = theme.TextMutedBrush,
                FontSize = 10, FontFamily = MonoFont, FontWeight = FontWeights.Medium
            }
        };
        Grid.SetColumn(typeBadge, 1);
        mainRow.Children.Add(typeBadge);

        // Input side (fills remaining space, left-aligned across all cards)
        FrameworkElement input;
        if (param.PropertyType == typeof(bool))
        {
            input = new CheckBox
            {
                IsChecked = currentValue is true,
                Foreground = theme.TextPrimaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
        }
        else
        {
            var displayValue = currentValue switch
            {
                string[] arr => string.Join(", ", arr),
                _ => currentValue?.ToString() ?? ""
            };
            input = new TextBox
            {
                Text = displayValue,
                Padding = new Thickness(10, 6, 10, 6),
                Background = theme.InputFieldBrush,
                Foreground = theme.TextPrimaryBrush,
                CaretBrush = theme.TextPrimaryBrush,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                FontFamily = MonoFont, FontSize = 12,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        Grid.SetColumn(input, 2);
        mainRow.Children.Add(input);

        stack.Children.Add(mainRow);

        if (!string.IsNullOrEmpty(param.Description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = param.Description,
                Foreground = theme.TextMutedBrush,
                FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0), LineHeight = 18
            });
        }

        border.Child = stack;
        return (border, input);
    }
}
