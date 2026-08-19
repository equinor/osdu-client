using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

internal sealed class ToggleButtonBuilder(AppTheme theme)
{
    public Button Build(string text)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 12, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, theme.TagBrush);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(16, 9, 16, 9));
        borderFactory.SetValue(Border.BorderBrushProperty, theme.BorderBrush);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.Name = "ButtonBorder";

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, text);
        textFactory.SetValue(TextBlock.ForegroundProperty, theme.TextPrimaryBrush);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        borderFactory.AppendChild(textFactory);
        template.VisualTree = borderFactory;

        var hoverBg = theme.IsDark
            ? new SolidColorBrush(Color.FromRgb(55, 55, 68))
            : new SolidColorBrush(Color.FromRgb(225, 227, 235));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "ButtonBorder"));
        template.Triggers.Add(hover);

        button.Template = template;
        return button;
    }
}
