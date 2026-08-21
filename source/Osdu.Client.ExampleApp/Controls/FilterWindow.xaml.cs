using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Services;

namespace Osdu.Client.ExampleApp.Controls;

public partial class FilterWindow : Window
{
    private readonly string _kindId;
    private readonly List<PropertyInfo> _properties;
    private readonly AppTheme _theme;
    private readonly ObservableCollection<FilterConditionViewModel> _conditions = [];

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
            QueryPreviewBox.Text = ManualQueryBox.Text;
            return;
        }

        var parts = _conditions
            .Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.PropertyPath))
            .Select(c => c.ToCondition().ToLucene())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        QueryPreviewBox.Text = parts.Count > 0
            ? string.Join(" AND ", parts)
            : "(no filter)";
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
        ShowIntellisense();
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
            return;
        }

        // Extract the current word being typed
        var currentWord = GetCurrentWord(text, caretIndex);
        if (string.IsNullOrEmpty(currentWord))
        {
            IntellisensePopup.IsOpen = false;
            return;
        }

        // Find matching properties (support dot notation)
        var suggestions = GetSuggestions(currentWord);
        if (suggestions.Count == 0)
        {
            IntellisensePopup.IsOpen = false;
            return;
        }

        IntellisenseList.ItemsSource = suggestions;
        IntellisenseList.SelectedIndex = 0;

        // Position popup near caret
        var rect = ManualQueryBox.GetRectFromCharacterIndex(caretIndex);
        IntellisensePopup.PlacementTarget = ManualQueryBox;
        IntellisensePopup.HorizontalOffset = rect.Left;
        IntellisensePopup.VerticalOffset = rect.Bottom + 2;
        IntellisensePopup.IsOpen = true;
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
            .Select(p => string.IsNullOrEmpty(parentPath) ? p.JsonName : $"{parentPath}.{p.JsonName}")
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

    private void AcceptIntellisense()
    {
        if (IntellisenseList.SelectedItem is not string selected) return;

        var text = ManualQueryBox.Text;
        var caretIndex = ManualQueryBox.CaretIndex;
        var currentWord = GetCurrentWord(text, caretIndex);

        int start = caretIndex - currentWord.Length;
        ManualQueryBox.Text = text[..start] + selected + text[caretIndex..];
        ManualQueryBox.CaretIndex = start + selected.Length;
        IntellisensePopup.IsOpen = false;
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
            ComposedQuery = query;
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
}