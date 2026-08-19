using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Dynamically generates WPF UI elements from an OpenAPI specification.
/// </summary>
public class ApiUiBuilder
{
    private static readonly Color GetColor = Color.FromRgb(97, 175, 254);
    private static readonly Color PostColor = Color.FromRgb(73, 204, 144);
    private static readonly Color PutColor = Color.FromRgb(252, 161, 48);
    private static readonly Color DeleteColor = Color.FromRgb(249, 80, 80);
    private static readonly Color PatchColor = Color.FromRgb(80, 227, 194);
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    private readonly TextBox _responseTextBox;
    private readonly TextBlock _responseStatusText;
    private readonly HttpClient _httpClient;
    private readonly AppTheme _theme;

    public ApiUiBuilder(TextBox responseTextBox, TextBlock responseStatusText, HttpClient httpClient, AppTheme theme)
    {
        _responseTextBox = responseTextBox;
        _responseStatusText = responseStatusText;
        _httpClient = httpClient;
        _theme = theme;
    }

    public void BuildEndpointsUi(JsonElement root, StackPanel container)
    {
        if (!root.TryGetProperty("paths", out var paths))
            return;

        var schemas = root.TryGetProperty("components", out var components)
            && components.TryGetProperty("schemas", out var s) ? s : default;

        var basePath = "";
        if (root.TryGetProperty("servers", out var servers))
        {
            foreach (var server in servers.EnumerateArray())
            {
                if (server.TryGetProperty("url", out var url))
                {
                    basePath = url.GetString()?.TrimEnd('/') ?? "";
                    break;
                }
            }
        }

        // Group by tag
        var grouped = new Dictionary<string, List<(string Path, string Method, JsonElement Operation)>>();

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var tag = "default";
                if (method.Value.TryGetProperty("tags", out var tags))
                {
                    foreach (var t in tags.EnumerateArray())
                    {
                        tag = t.GetString() ?? "default";
                        break;
                    }
                }

                if (!grouped.ContainsKey(tag))
                    grouped[tag] = [];

                grouped[tag].Add((path.Name, method.Name, method.Value));
            }
        }

        foreach (var group in grouped)
        {
            var tagHeader = new TextBlock
            {
                Text = FormatTagName(group.Key),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = _theme.TextPrimaryBrush,
                Margin = new Thickness(0, 8, 0, 12)
            };
            container.Children.Add(tagHeader);

            foreach (var (endpointPath, httpMethod, operation) in group.Value)
            {
                var panel = CreateEndpointPanel(basePath, endpointPath, httpMethod, operation, schemas);
                container.Children.Add(panel);
            }
        }
    }

    private Border CreateEndpointPanel(string basePath, string path, string httpMethod, JsonElement operation, JsonElement schemas)
    {
        var methodColor = GetMethodColor(httpMethod);
        var methodBrush = new SolidColorBrush(methodColor);
        var methodBgAlpha = (byte)(_theme.IsDark ? 20 : 30);
        var methodBgBrush = new SolidColorBrush(Color.FromArgb(methodBgAlpha, methodColor.R, methodColor.G, methodColor.B));

        var border = new Border
        {
            Background = _theme.CardBrush,
            BorderBrush = _theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Opacity = _theme.ShadowOpacity,
                Color = Colors.Black
            }
        };

        // Build the expander header
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var methodBadge = new Border
        {
            Background = methodBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = httpMethod.ToUpperInvariant(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                FontFamily = MonoFont
            }
        };
        headerPanel.Children.Add(methodBadge);

        var pathText = new TextBlock
        {
            Text = path,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            FontFamily = MonoFont,
            Foreground = _theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        headerPanel.Children.Add(pathText);

        if (operation.TryGetProperty("summary", out var summaryEl))
        {
            var summaryText = summaryEl.GetString() ?? "";
            if (summaryText.Length > 60)
                summaryText = summaryText[..57] + "...";

            headerPanel.Children.Add(new TextBlock
            {
                Text = summaryText,
                Foreground = _theme.TextMutedBrush,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        var endpointExpander = new Expander
        {
            Header = headerPanel,
            IsExpanded = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = _theme.TextPrimaryBrush,
            Padding = new Thickness(0)
        };

        // Expander content
        var contentStack = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

        if (operation.TryGetProperty("summary", out var summary))
        {
            contentStack.Children.Add(new TextBlock
            {
                Text = summary.GetString(),
                Foreground = _theme.TextSecondaryBrush,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        if (operation.TryGetProperty("description", out var descEl))
        {
            contentStack.Children.Add(new TextBlock
            {
                Text = descEl.GetString(),
                Foreground = _theme.TextMutedBrush,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }

        // Parameters
        var inputControls = new Dictionary<string, (FrameworkElement Control, string Location)>();

        if (operation.TryGetProperty("parameters", out var parameters))
        {
            var hasVisibleParams = false;
            foreach (var param in parameters.EnumerateArray())
            {
                var inValue = param.GetProperty("in").GetString();
                if (inValue == "header") continue;

                if (!hasVisibleParams)
                {
                    contentStack.Children.Add(new TextBlock
                    {
                        Text = "Parameters",
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12,
                        Foreground = _theme.TextPrimaryBrush,
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                    hasVisibleParams = true;
                }

                var paramName = param.GetProperty("name").GetString()!;
                var required = param.TryGetProperty("required", out var req) && req.GetBoolean();
                var description = param.TryGetProperty("description", out var desc) ? desc.GetString() : "";
                var paramType = "string";
                if (param.TryGetProperty("schema", out var paramSchema) && paramSchema.TryGetProperty("type", out var pt))
                    paramType = pt.GetString()!;

                var control = CreateParameterControl(paramName, paramType, required, description, inValue!);
                contentStack.Children.Add(control.Panel);
                inputControls[paramName] = (control.Input, inValue!);
            }
        }

        // Request body
        TextBox? bodyTextBox = null;
        if (operation.TryGetProperty("requestBody", out var requestBody))
        {
            var bodySchema = GetRequestBodySchema(requestBody, schemas);

            var bodyHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 6) };
            bodyHeader.Children.Add(new TextBlock
            {
                Text = "Request Body",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = _theme.TextPrimaryBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            bodyHeader.Children.Add(new Border
            {
                Background = _theme.TagBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "application/json",
                    FontSize = 10,
                    FontFamily = MonoFont,
                    Foreground = _theme.TextMutedBrush
                }
            });
            contentStack.Children.Add(bodyHeader);

            bodyTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = MonoFont,
                FontSize = 12,
                MinHeight = 140,
                MaxHeight = 350,
                TextWrapping = TextWrapping.NoWrap,
                Text = bodySchema,
                Background = _theme.InputBrush,
                Foreground = _theme.TextPrimaryBrush,
                CaretBrush = _theme.TextPrimaryBrush,
                BorderBrush = _theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10)
            };
            contentStack.Children.Add(bodyTextBox);
        }

        // Execute button
        var executeButton = CreateExecuteButton(methodColor);

        var capturedBasePath = basePath;
        var capturedPath = path;
        var capturedMethod = httpMethod;
        var capturedInputs = inputControls;
        var capturedBody = bodyTextBox;

        executeButton.Click += async (_, _) =>
        {
            await ExecuteRequestAsync(executeButton, capturedBasePath, capturedPath, capturedMethod, capturedInputs, capturedBody);
        };

        contentStack.Children.Add(executeButton);

        endpointExpander.Content = contentStack;

        var wrapperStack = new StackPanel();

        var accentBar = new Border
        {
            Background = methodBgBrush,
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(12, 8, 12, 8),
            Child = endpointExpander
        };

        wrapperStack.Children.Add(accentBar);
        border.Child = wrapperStack;
        return border;
    }

    private Button CreateExecuteButton(Color accentColor)
    {
        var button = new Button
        {
            Padding = new Thickness(20, 8, 20, 8),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var buttonTemplate = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(accentColor));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(20, 8, 20, 8));
        borderFactory.Name = "ButtonBorder";

        var contentFactory = new FrameworkElementFactory(typeof(StackPanel));
        contentFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
        iconFactory.SetValue(TextBlock.TextProperty, "▶");
        iconFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
        iconFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        iconFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
        iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.AppendChild(iconFactory);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, "Execute");
        textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 12.5);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.AppendChild(textFactory);

        borderFactory.AppendChild(contentFactory);
        buttonTemplate.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        var hoverColor = Color.FromArgb(255,
            (byte)Math.Min(accentColor.R + 20, 255),
            (byte)Math.Min(accentColor.G + 20, 255),
            (byte)Math.Min(accentColor.B + 20, 255));
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "ButtonBorder"));
        buttonTemplate.Triggers.Add(hoverTrigger);

        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(Border.BackgroundProperty, _theme.TagBrush, "ButtonBorder"));
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.6));
        buttonTemplate.Triggers.Add(disabledTrigger);

        button.Template = buttonTemplate;
        return button;
    }

    private async Task ExecuteRequestAsync(
        Button executeButton,
        string basePath,
        string path,
        string httpMethod,
        Dictionary<string, (FrameworkElement Control, string Location)> inputControls,
        TextBox? bodyTextBox)
    {
        executeButton.IsEnabled = false;
        _responseTextBox.Text = "";
        _responseStatusText.Text = "⏳ Sending...";

        try
        {
            var queryParams = new List<string>();
            var pathResolved = path;

            foreach (var kvp in inputControls)
            {
                var value = GetControlValue(kvp.Value.Control);
                if (string.IsNullOrEmpty(value)) continue;

                if (kvp.Value.Location == "path")
                    pathResolved = pathResolved.Replace($"{{{kvp.Key}}}", Uri.EscapeDataString(value));
                else
                    queryParams.Add($"{kvp.Key}={Uri.EscapeDataString(value)}");
            }

            var relativePath = $"{basePath}{pathResolved}";
            if (queryParams.Count > 0)
                relativePath = $"{relativePath}?{string.Join("&", queryParams)}";

            using var request = new HttpRequestMessage(
                new HttpMethod(httpMethod.ToUpperInvariant()),
                relativePath);

            if (bodyTextBox != null && !string.IsNullOrWhiteSpace(bodyTextBox.Text))
            {
                request.Content = new StringContent(bodyTextBox.Text, Encoding.UTF8, "application/json");
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(request);
            sw.Stop();

            var responseBody = await response.Content.ReadAsStringAsync();

            var statusCode = (int)response.StatusCode;
            var statusEmoji = statusCode < 300 ? "✅" : statusCode < 400 ? "↪️" : "❌";
            _responseStatusText.Text = $"{statusEmoji} {statusCode} {response.ReasonPhrase} — {sw.ElapsedMilliseconds}ms";
            _responseStatusText.Foreground = statusCode < 300
                ? new SolidColorBrush(PostColor)
                : statusCode < 400 ? new SolidColorBrush(PutColor) : new SolidColorBrush(DeleteColor);

            try
            {
                var jsonDoc = JsonDocument.Parse(responseBody);
                responseBody = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { }

            _responseTextBox.Text = responseBody;
        }
        catch (Exception ex)
        {
            _responseStatusText.Text = "❌ Error";
            _responseStatusText.Foreground = new SolidColorBrush(DeleteColor);
            _responseTextBox.Text = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        }
        finally
        {
            executeButton.IsEnabled = true;
        }
    }

    private (Border Panel, FrameworkElement Input) CreateParameterControl(
        string name, string type, bool required, string? description, string location)
    {
        var panel = new Border
        {
            Background = _theme.InputBrush,
            BorderBrush = _theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var stack = new StackPanel();

        var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        labelRow.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            FontFamily = MonoFont,
            Foreground = _theme.TextPrimaryBrush
        });
        if (required)
        {
            labelRow.Children.Add(new TextBlock
            {
                Text = " *",
                Foreground = _theme.RequiredBrush,
                FontWeight = FontWeights.Bold,
                FontSize = 12
            });
        }
        labelRow.Children.Add(new Border
        {
            Background = _theme.TagBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"{location} · {type}",
                Foreground = _theme.TextMutedBrush,
                FontSize = 10,
                FontFamily = MonoFont
            }
        });
        stack.Children.Add(labelRow);

        if (!string.IsNullOrEmpty(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = _theme.TextMutedBrush,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        FrameworkElement input;
        if (type == "boolean")
        {
            input = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = _theme.TextPrimaryBrush
            };
        }
        else
        {
            input = new TextBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                Background = _theme.InputFieldBrush,
                Foreground = _theme.TextPrimaryBrush,
                CaretBrush = _theme.TextPrimaryBrush,
                BorderBrush = _theme.BorderBrush,
                BorderThickness = new Thickness(1),
                FontFamily = MonoFont,
                FontSize = 12
            };
        }

        stack.Children.Add(input);
        panel.Child = stack;
        return (panel, input);
    }

    private static Color GetMethodColor(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => GetColor,
            "POST" => PostColor,
            "PUT" => PutColor,
            "DELETE" => DeleteColor,
            "PATCH" => PatchColor,
            _ => Color.FromRgb(128, 128, 128)
        };
    }

    private static string FormatTagName(string tag)
    {
        return tag.Replace("-", " ").Replace("_", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..])
            .Aggregate((a, b) => $"{a} {b}");
    }

    private static string GetControlValue(FrameworkElement control)
    {
        return control switch
        {
            TextBox tb => tb.Text,
            CheckBox cb => cb.IsChecked == true ? "true" : "false",
            _ => ""
        };
    }

    private string GetRequestBodySchema(JsonElement requestBody, JsonElement schemas)
    {
        try
        {
            if (requestBody.TryGetProperty("content", out var content)
                && content.TryGetProperty("application/json", out var jsonContent)
                && jsonContent.TryGetProperty("schema", out var schema))
            {
                var resolved = ResolveSchema(schema, schemas);
                return JsonSerializer.Serialize(resolved, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
        }
        catch (Exception ex)
        {
            return $"Request body schema could not be resolved. ERROR: {ex.Message}";
        }

        return "{\n  \n}";
    }

    private Dictionary<string, object?> ResolveSchema(JsonElement schema, JsonElement schemas, int depth = 0)
    {
        if (depth > 5) return new Dictionary<string, object?> { ["..."] = "max depth" };

        if (schema.TryGetProperty("$ref", out var refElement))
        {
            var refPath = refElement.GetString()!;
            var schemaName = refPath.Split('/').Last();
            if (schemas.ValueKind == JsonValueKind.Object && schemas.TryGetProperty(schemaName, out var refSchema))
            {
                return ResolveSchema(refSchema, schemas, depth + 1);
            }
            return new Dictionary<string, object?> { ["$ref"] = refPath };
        }

        var result = new Dictionary<string, object?>();

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                result[prop.Name] = ResolvePropertyExample(prop.Value, schemas, depth + 1);
            }
        }

        return result;
    }

    private object? ResolvePropertyExample(JsonElement prop, JsonElement schemas, int depth)
    {
        if (depth > 5) return "...";

        if (prop.TryGetProperty("$ref", out var refEl))
        {
            var refPath = refEl.GetString()!;
            var schemaName = refPath.Split('/').Last();
            if (schemas.ValueKind == JsonValueKind.Object && schemas.TryGetProperty(schemaName, out var refSchema))
            {
                return ResolveSchema(refSchema, schemas, depth);
            }
            return $"<{schemaName}>";
        }

        if (prop.TryGetProperty("anyOf", out var anyOf))
        {
            foreach (var item in anyOf.EnumerateArray())
            {
                return ResolvePropertyExample(item, schemas, depth);
            }
        }

        if (prop.TryGetProperty("example", out var example))
        {
            return example.ValueKind switch
            {
                JsonValueKind.String => example.GetString(),
                JsonValueKind.Number => example.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(example.GetRawText()),
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(example.GetRawText()),
                _ => example.ToString()
            };
        }

        var type = prop.TryGetProperty("type", out var t) ? t.GetString() : "string";

        return type switch
        {
            "string" => "",
            "integer" => 0,
            "number" => 0.0,
            "boolean" => false,
            "array" => new List<object?>(),
            "object" => ResolveSchema(prop, schemas, depth),
            _ => null
        };
    }
}
