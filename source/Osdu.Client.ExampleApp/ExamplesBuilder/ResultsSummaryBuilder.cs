using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Builds the results summary cards shown after running multiple examples.
/// </summary>
internal sealed class ResultsSummaryBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    public delegate void ExampleSelected(IExample example);
    public event ExampleSelected? OnExampleClicked;

    public void Build(StackPanel container, IReadOnlyList<IExample> examples, IReadOnlyDictionary<IExample, (bool Success, string Output, long ElapsedMs)> results)
    {
        container.Children.Clear();

        foreach (var example in examples)
        {
            if (!results.TryGetValue(example, out var result)) continue;

            var statusColor = result.Success ? ExampleColors.SuccessColor : ExampleColors.FailureColor;

            var card = new Border
            {
                Background = theme.CardBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, statusColor.R, statusColor.G, statusColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 6, ShadowDepth = 2,
                    Opacity = theme.ShadowOpacity, Color = Colors.Black, Direction = 270
                }
            };

            var row = new DockPanel();

            // Accent bar
            var bar = new Border
            {
                Width = 4, Background = new SolidColorBrush(statusColor),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            DockPanel.SetDock(bar, Dock.Left);
            row.Children.Add(bar);

            row.Children.Add(new TextBlock
            {
                Text = result.Success ? "✅" : "❌", FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
            });

            var time = new Border
            {
                Background = theme.TagBrush, CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center
            };
            time.Child = new TextBlock
            {
                Text = $"{result.ElapsedMs}ms", FontSize = 10,
                FontFamily = MonoFont, Foreground = theme.TextMutedBrush, FontWeight = FontWeights.Medium
            };
            DockPanel.SetDock(time, Dock.Right);
            row.Children.Add(time);

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = example.Text, FontWeight = FontWeights.SemiBold,
                FontSize = 13, Foreground = theme.TextPrimaryBrush
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = example.Category, FontSize = 10.5,
                Foreground = theme.TextMutedBrush, Margin = new Thickness(0, 2, 0, 0)
            });
            row.Children.Add(nameStack);

            card.Child = row;

            var captured = example;
            card.MouseLeftButtonUp += (_, _) => OnExampleClicked?.Invoke(captured);

            container.Children.Add(card);
        }
    }
}
