using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

internal sealed class RunButtonBuilder(AppTheme theme)
{
    public Button Build()
    {
        var accentColor = theme.Accent;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(accentColor));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(22, 10, 22, 10));
        borderFactory.SetValue(Border.EffectProperty, new DropShadowEffect
        {
            BlurRadius = 8, ShadowDepth = 2, Opacity = 0.25,
            Color = accentColor, Direction = 270
        });
        borderFactory.Name = "ButtonBorder";

        var content = new FrameworkElementFactory(typeof(StackPanel));
        content.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetValue(TextBlock.TextProperty, "▶");
        icon.SetValue(TextBlock.FontSizeProperty, 14.0);
        icon.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        icon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 0));
        icon.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.AppendChild(icon);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, "Run Example");
        text.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.FontSizeProperty, 13.5);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.AppendChild(text);

        borderFactory.AppendChild(content);
        template.VisualTree = borderFactory;

        var hoverColor = Color.FromArgb(255,
            (byte)Math.Min(accentColor.R + 20, 255),
            (byte)Math.Min(accentColor.G + 20, 255),
            (byte)Math.Min(accentColor.B + 20, 255));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "ButtonBorder"));
        template.Triggers.Add(hover);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Border.BackgroundProperty, theme.TagBrush, "ButtonBorder"));
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.6));
        template.Triggers.Add(disabled);

        button.Template = template;
        return button;
    }
}
