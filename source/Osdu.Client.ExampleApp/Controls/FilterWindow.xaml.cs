using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Osdu.Client.ExampleApp.Services;

namespace Osdu.Client.ExampleApp.Controls;

public partial class FilterWindow : Window
{
    private readonly string _kindId;
    private readonly List<PropertyInfo> _properties;
    private readonly AppTheme _theme;
    private readonly ObservableCollection<FilterConditionViewModel> _conditions = [];

    // Intellisense state tracking
    private enum IntellisenseMode { None, Property, Operator }
    private IntellisenseMode _intellisenseMode = IntellisenseMode.None;
    private PropertyInfo? _lastResolvedProperty;
    private bool _suppressIntellisense;

    public string? ComposedQuery { get; private set; }
    public bool Applied { get; private set; }

    public FilterWindow(string kindId, AppTheme theme, string? existingQuery = null)
    {
        InitializeComponent();
        _kindId = kindId;
        _theme = theme;
        _properties = KindPropertyResolver.GetProperties(kindId);

        ConditionsList.ItemsSource = _conditions;
        PopulatePropertyTree(_properties);
        AddCondition(); // Start with one empty condition

        if (!string.IsNullOrWhiteSpace(existingQuery))
        {
            ManualQueryBox.Text = existingQuery;
            ManualModeCheck.IsChecked = true;
        }

        UpdatePreview();
        ApplyTheme(theme);
    }

    private void PopulatePropertyTree(List<PropertyInfo> properties)
    {
        PropertyTree.Items.Clear();
        foreach (var prop in properties)
        {
            PropertyTree.Items.Add(CreateTreeItem(prop));
        }
    }

    private TreeViewItem CreateTreeItem(PropertyInfo prop)
    {
        var item = new TreeViewItem
        {
            Header = FormatPropertyHeader(prop),
            Tag = prop,
            FontFamily = AppTheme.MonoFontFamily,
            FontSize = AppTheme.FontSizeSmall
        };

        if (prop.Children.Count > 0)
        {
            foreach (var child in prop.Children)
            {
                item.Items.Add(CreateTreeItem(child));
            }
        }

        item.MouseDoubleClick += (s, e) =>
        {
            if (s is TreeViewItem ti && ti.Tag is PropertyInfo pi && pi.Children.Count == 0)
            {
                AddPropertyToActiveCondition(pi);
                e.Handled = true;
            }
        };

        return item;
    }

    private static string FormatPropertyHeader(PropertyInfo prop)
    {
        var typeLabel = prop.Kind switch
        {
            PropertyKind.String => "str",
            PropertyKind.Number => "num",
            PropertyKind.Boolean => "bool",
            PropertyKind.DateTime => "date",
            PropertyKind.Object => "{ }",
            PropertyKind.Array => "[ ]",
            _ => ""
        };
        return $"{prop.JsonName}  ({typeLabel})";
    }

    private void AddPropertyToActiveCondition(PropertyInfo prop)
    {
        var active = _conditions.LastOrDefault(c => string.IsNullOrEmpty(c.PropertyPath));
        if (active is null)
        {
            AddCondition();
            active = _conditions[^1];
        }
        active.PropertyPath = prop.Path;
        active.PropertyInfo = prop;
        active.UpdateOperators();
        UpdatePreview();
    }

    private void AddCondition()
    {
        var vm = new FilterConditionViewModel(_properties);
        vm.PropertyChanged += (_, _) => UpdatePreview();
        _conditions.Add(vm);
    }

    private void AddCondition_Click(object sender, RoutedEventArgs e) => AddCondition();

    private void RemoveCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FilterConditionViewModel vm)
        {
            _conditions.Remove(vm);
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (ManualModeCheck?.IsChecked == true)
        {
            // Format the manual query for multiline preview
            var query = ManualQueryBox.Text?.Trim() ?? "";
            QueryPreviewBox.Text = string.IsNullOrWhiteSpace(query) ? "(no filter)" : FormatQueryForPreview(query);
            return;
        }

        var parts = _conditions
            .Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.PropertyPath))
            .Select(c => c.ToCondition().ToLucene())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (parts.Count == 0)
        {
            QueryPreviewBox.Text = "(no filter)";
        }
        else if (parts.Count == 1)
        {
            QueryPreviewBox.Text = parts[0];
        }
        else
        {
            // Show each condition on its own line for readability
            QueryPreviewBox.Text = string.Join("\n  AND ", parts);
        }
    }

    /// <summary>Formats a raw query string for multiline preview readability.</summary>
    private static string FormatQueryForPreview(string query)
    {
        // Split on AND/OR for readability in preview
        return query
            .Replace(" AND ", "\n  AND ", StringComparison.OrdinalIgnoreCase)
            .Replace(" OR ", "\n  OR ", StringComparison.OrdinalIgnoreCase);
    }

    private void ManualModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        bool isManual = ManualModeCheck.IsChecked == true;
        VisualBuilderPanel.Visibility = isManual ? Visibility.Collapsed : Visibility.Visible;
        ManualQueryPanel.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;

        if (isManual && QueryPreviewBox.Text != "(no filter)")
        {
            ManualQueryBox.Text = QueryPreviewBox.Text;
        }
        UpdatePreview();
    }

    private void ManualQueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();

        if (_suppressIntellisense) return;

        // Defer intellisense to after WPF finishes processing the current input event,
        // so caret position and layout are up-to-date.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, ShowIntellisense);
    }

    private void ManualQueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IntellisensePopup.IsOpen)
        {
            if (e.Key == Key.Down)
            {
                IntellisenseList.SelectedIndex = Math.Min(IntellisenseList.SelectedIndex + 1, IntellisenseList.Items.Count - 1);
                IntellisenseList.ScrollIntoView(IntellisenseList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                IntellisenseList.SelectedIndex = Math.Max(IntellisenseList.SelectedIndex - 1, 0);
                IntellisenseList.ScrollIntoView(IntellisenseList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                AcceptIntellisense();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IntellisensePopup.IsOpen = false;
                e.Handled = true;
            }
        }
    }

    private void ShowIntellisense()
    {
        var text = ManualQueryBox.Text;
        var caretIndex = ManualQueryBox.CaretIndex;
        if (caretIndex <= 0 || string.IsNullOrEmpty(text))
        {
            IntellisensePopup.IsOpen = false;
            _intellisenseMode = IntellisenseMode.None;
            return;
        }

        // Determine context: are we after a colon (operator context) or typing a property?
        var context = GetIntellisenseContext(text, caretIndex);

        if (context.Mode == IntellisenseMode.Operator)
        {
            // Show operator suggestions relevant to the resolved property type
            var operators = GetOperatorSuggestions(context.PropertyPath, context.TypedText);
            if (operators.Count == 0)
            {
                IntellisensePopup.IsOpen = false;
                _intellisenseMode = IntellisenseMode.None;
                return;
            }

            _intellisenseMode = IntellisenseMode.Operator;
            IntellisenseList.ItemsSource = operators;
            IntellisenseList.SelectedIndex = 0;
            PositionPopup(caretIndex);
        }
        else if (context.Mode == IntellisenseMode.Property)
        {
            // Show property suggestions
            var currentWord = context.TypedText;
            if (string.IsNullOrEmpty(currentWord))
            {
                IntellisensePopup.IsOpen = false;
                _intellisenseMode = IntellisenseMode.None;
                return;
            }

            var suggestions = GetSuggestions(currentWord);
            if (suggestions.Count == 0)
            {
                IntellisensePopup.IsOpen = false;
                _intellisenseMode = IntellisenseMode.None;
                return;
            }

            _intellisenseMode = IntellisenseMode.Property;
            IntellisenseList.ItemsSource = suggestions;
            IntellisenseList.SelectedIndex = 0;
            PositionPopup(caretIndex);
        }
        else
        {
            IntellisensePopup.IsOpen = false;
            _intellisenseMode = IntellisenseMode.None;
        }
    }

    private void PositionPopup(int caretIndex)
    {
        // Ensure caret index is within bounds
        if (caretIndex > ManualQueryBox.Text.Length)
            caretIndex = ManualQueryBox.Text.Length;

        var rect = ManualQueryBox.GetRectFromCharacterIndex(caretIndex);

        // Fallback if rect is empty (can happen before layout pass)
        if (rect.IsEmpty)
        {
            rect = ManualQueryBox.GetRectFromCharacterIndex(Math.Max(0, caretIndex - 1));
            if (rect.IsEmpty)
            {
                IntellisensePopup.HorizontalOffset = 0;
                IntellisensePopup.VerticalOffset = ManualQueryBox.FontSize + 4;
                IntellisensePopup.PlacementTarget = ManualQueryBox;
                IntellisensePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                IntellisensePopup.IsOpen = true;
                return;
            }
        }

        IntellisensePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint;
        IntellisensePopup.PlacementTarget = ManualQueryBox;
        IntellisensePopup.HorizontalOffset = rect.Left;
        IntellisensePopup.VerticalOffset = rect.Top + rect.Height + 4;
        IntellisensePopup.IsOpen = true;
    }

    /// <summary>
    /// Determines whether intellisense should show properties or operators based on cursor context.
    /// </summary>
    private IntellisenseContext GetIntellisenseContext(string text, int caretIndex)
    {
        // Look backwards from caret to find the start of the current token
        int pos = caretIndex - 1;
        while (pos >= 0 && text[pos] != ' ' && text[pos] != '\n' && text[pos] != '\r')
            pos--;
        pos++;
        var token = text[pos..caretIndex];

        if (string.IsNullOrEmpty(token))
        {
            return new IntellisenseContext { Mode = IntellisenseMode.None, PropertyPath = "", TypedText = "" };
        }

        // Check if there's a colon in the token — indicates we're after "property:"
        int colonIdx = token.IndexOf(':');
        if (colonIdx >= 0)
        {
            // We're in operator/value context after "property:"
            var propertyPart = token[..colonIdx];
            var afterColon = token[(colonIdx + 1)..];

            // Resolve the property to determine its type
            _lastResolvedProperty = ResolvePropertyByPath(propertyPart);

            return new IntellisenseContext
            {
                Mode = IntellisenseMode.Operator,
                PropertyPath = propertyPart,
                TypedText = afterColon
            };
        }

        // Otherwise we're typing a property name
        return new IntellisenseContext
        {
            Mode = IntellisenseMode.Property,
            PropertyPath = "",
            TypedText = token
        };
    }

    /// <summary>Resolves a property path (supports dot notation) to its PropertyInfo.</summary>
    private PropertyInfo? ResolvePropertyByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var parts = path.Split('.');
        var searchIn = _properties;
        PropertyInfo? found = null;

        foreach (var part in parts)
        {
            found = searchIn.FirstOrDefault(p =>
                p.JsonName.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (found is null) return null;
            searchIn = found.Children;
        }

        return found;
    }

    /// <summary>Gets operator suggestions based on property type and what user has typed so far.</summary>
    private List<string> GetOperatorSuggestions(string propertyPath, string typed)
    {
        var prop = _lastResolvedProperty ?? ResolvePropertyByPath(propertyPath);
        var kind = prop?.Kind ?? PropertyKind.String;

        // Check if this property is inside a nested array (e.g. data.Curves.CurveID)
        bool isNested = IsNestedArrayProperty(propertyPath);

        List<string> operators = kind switch
        {
            PropertyKind.Number or PropertyKind.DateTime => new List<string>
            {
                "\"value\"  — equals (phrase match)",
                "[* TO *]  — exists / not null",
                "{value TO *}  — greater than",
                "[value TO *]  — greater than or equal",
                "{* TO value}  — less than",
                "[* TO value]  — less than or equal",
                "[value1 TO value2]  — range between",
            },
            PropertyKind.Boolean => new List<string>
            {
                "true  — equals true",
                "false  — equals false",
            },
            _ => new List<string>
            {
                "\"value\"  — equals (phrase match)",
                "value  — match (term match)",
                "value*  — starts with (prefix)",
                "[* TO *]  — exists / not null",
                "*value*  — contains (may be slow)",
                "*value  — ends with (may be slow)",
            }
        };

        // Add nested hint if inside an array field
        if (isNested)
        {
            var nestedPath = GetNestedArrayPath(propertyPath);
            if (nestedPath is not null)
            {
                operators.Insert(0, $"nested({nestedPath}, {propertyPath}:\"value\")  — nested array query");
            }
        }

        if (!string.IsNullOrEmpty(typed))
        {
            operators.Insert(0, $"{typed}  — literal value");
            operators = operators
                .Where(o => o.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return operators;
    }

    /// <summary>Checks if the property path traverses through an array field.</summary>
    private bool IsNestedArrayProperty(string path)
    {
        var segments = path.Split('.');
        var searchIn = _properties;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            var match = searchIn.FirstOrDefault(p =>
                p.JsonName.Equals(segments[i], StringComparison.OrdinalIgnoreCase));
            if (match is null) return false;
            if (match.Kind == PropertyKind.Array) return true;
            searchIn = match.Children;
        }

        return false;
    }

    /// <summary>Gets the dot-path of the nearest array ancestor.</summary>
    private string? GetNestedArrayPath(string path)
    {
        var segments = path.Split('.');
        var searchIn = _properties;
        var pathParts = new List<string>();

        for (int i = 0; i < segments.Length - 1; i++)
        {
            var match = searchIn.FirstOrDefault(p =>
                p.JsonName.Equals(segments[i], StringComparison.OrdinalIgnoreCase));
            if (match is null) return null;
            pathParts.Add(match.JsonName);
            if (match.Kind == PropertyKind.Array)
                return string.Join(".", pathParts);
            searchIn = match.Children;
        }

        return null;
    }

    private List<string> GetSuggestions(string currentWord)
    {
        var parts = currentWord.Split('.');
        var searchIn = _properties;

        // Navigate dot notation
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var match = searchIn.FirstOrDefault(p =>
                p.JsonName.Equals(parts[i], StringComparison.OrdinalIgnoreCase));
            if (match is null) return [];
            searchIn = match.Children;
        }

        var prefix = parts[^1].ToLowerInvariant();
        var parentPath = parts.Length > 1
            ? string.Join('.', parts[..^1])
            : "";

        return searchIn
            .Where(p => p.JsonName.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(15)
            .Select(p =>
            {
                var fullPath = string.IsNullOrEmpty(parentPath) ? p.JsonName : $"{parentPath}.{p.JsonName}";
                var typeHint = p.Kind switch
                {
                    PropertyKind.String => "str",
                    PropertyKind.Number => "num",
                    PropertyKind.Boolean => "bool",
                    PropertyKind.DateTime => "date",
                    PropertyKind.Object => "{ }",
                    PropertyKind.Array => "[ ]",
                    _ => ""
                };
                return $"{fullPath}  ({typeHint})";
            })
            .ToList();
    }

    private static string GetCurrentWord(string text, int caretIndex)
    {
        int start = caretIndex - 1;
        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == '_'))
            start--;
        start++;
        return text[start..caretIndex];
    }

    /// <summary>Gets the current token including colons and special chars for operator context.</summary>
    private static string GetCurrentToken(string text, int caretIndex)
    {
        int start = caretIndex - 1;
        while (start >= 0 && text[start] != ' ' && text[start] != '\n' && text[start] != '\r')
            start--;
        start++;
        return text[start..caretIndex];
    }

    private void AcceptIntellisense()
    {
        if (IntellisenseList.SelectedItem is not string selected) return;

        // Suppress intellisense from re-triggering during programmatic text change
        _suppressIntellisense = true;

        try
        {
            var text = ManualQueryBox.Text;
            var caretIndex = ManualQueryBox.CaretIndex;

            if (_intellisenseMode == IntellisenseMode.Operator)
            {
                // Extract just the operator value (strip the hint in parentheses)
                var operatorValue = selected.Contains("  (")
                    ? selected[..selected.IndexOf("  (")].Trim()
                    : selected.Trim();

                // Replace "value" placeholder with empty string for user to fill in
                operatorValue = operatorValue
                    .Replace("value1", "")
                    .Replace("value2", "")
                    .Replace("value", "");

                // Replace everything after the colon in the current token
                var token = GetCurrentToken(text, caretIndex);
                int colonInToken = token.IndexOf(':');
                if (colonInToken >= 0)
                {
                    int tokenStart = caretIndex - token.Length;
                    int insertAt = tokenStart + colonInToken + 1;
                    ManualQueryBox.Text = text[..insertAt] + operatorValue + text[caretIndex..];
                    ManualQueryBox.CaretIndex = insertAt + operatorValue.Length;
                }
                else
                {
                    int tokenStart = caretIndex - token.Length;
                    ManualQueryBox.Text = text[..tokenStart] + operatorValue + text[caretIndex..];
                    ManualQueryBox.CaretIndex = tokenStart + operatorValue.Length;
                }
            }
            else
            {
                // Property mode — extract just the property path (strip type hint)
                var propertyValue = selected.Contains("  (")
                    ? selected[..selected.IndexOf("  (")].Trim()
                    : selected.Trim();

                var currentWord = GetCurrentWord(text, caretIndex);
                int start = caretIndex - currentWord.Length;
                ManualQueryBox.Text = text[..start] + propertyValue + text[caretIndex..];
                ManualQueryBox.CaretIndex = start + propertyValue.Length;
            }

            IntellisensePopup.IsOpen = false;
            _intellisenseMode = IntellisenseMode.None;
        }
        finally
        {
            // Re-enable on next dispatcher cycle so the TextChanged from this edit is ignored
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _suppressIntellisense = false);
        }
    }

    private void IntellisenseList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AcceptIntellisense();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var query = QueryPreviewBox.Text;
        if (query == "(no filter)" || string.IsNullOrWhiteSpace(query))
        {
            ComposedQuery = null;
        }
        else
        {
            // Strip formatting newlines for the actual query sent to API
            ComposedQuery = query
                .Replace("\n  AND ", " AND ")
                .Replace("\n  OR ", " OR ")
                .Trim();
        }
        Applied = true;
        DialogResult = true;
        Close();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _conditions.Clear();
        ManualQueryBox.Text = "";
        ComposedQuery = null;
        Applied = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyTheme(AppTheme theme)
    {
        Background = theme.SurfaceBrush;
        FontFamily = AppTheme.FontFamily;
        FontSize = AppTheme.FontSize;

        // Style all borders/panels
        foreach (var border in FindVisualChildren<Border>(this))
        {
            if (border.Name == "HeaderBorder")
            {
                border.Background = theme.SidebarBrush;
                border.BorderBrush = theme.BorderBrush;
            }
        }

        foreach (var tb in FindVisualChildren<TextBlock>(this))
        {
            if (tb.Tag?.ToString() == "secondary")
                tb.Foreground = theme.TextSecondaryBrush;
            else
                tb.Foreground = theme.TextPrimaryBrush;
        }

        foreach (var btn in FindVisualChildren<Button>(this))
        {
            btn.Background = theme.CardBrush;
            btn.Foreground = theme.TextPrimaryBrush;
            btn.BorderBrush = theme.BorderBrush;
        }

        foreach (var tb in FindVisualChildren<TextBox>(this))
        {
            tb.Background = theme.InputFieldBrush;
            tb.Foreground = theme.TextPrimaryBrush;
            tb.BorderBrush = theme.BorderBrush;
            tb.CaretBrush = theme.TextPrimaryBrush;
        }

        // PropertyTree
        PropertyTree.Background = theme.SurfaceBrush;
        PropertyTree.Foreground = theme.TextPrimaryBrush;
        PropertyTree.BorderBrush = theme.BorderBrush;

        // Preview box
        QueryPreviewBox.Background = theme.InputFieldBrush;
        QueryPreviewBox.Foreground = theme.AccentBrush;
        QueryPreviewBox.BorderBrush = theme.BorderBrush;
        QueryPreviewBox.FontFamily = AppTheme.MonoFontFamily;

        // Intellisense popup styling
        if (IntellisensePopup.Child is Border popupBorder)
        {
            popupBorder.Background = theme.CardBrush;
            popupBorder.BorderBrush = theme.BorderBrush;
            if (popupBorder.Child is ListBox lb)
            {
                lb.Background = theme.CardBrush;
                lb.Foreground = theme.TextPrimaryBrush;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var grandChild in FindVisualChildren<T>(child))
                yield return grandChild;
        }
    }

    private record struct IntellisenseContext
    {
        public IntellisenseMode Mode { get; init; }
        public string PropertyPath { get; init; }
        public string TypedText { get; init; }
    }
}