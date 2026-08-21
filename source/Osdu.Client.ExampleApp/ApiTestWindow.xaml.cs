using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Examples;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp;

public partial class ApiTestWindow : Window
{
    private readonly string _definitionsPath;
    private readonly HttpClient _httpClient;
    private readonly IEnumerable<IExample> _examples;
    private readonly IOsduClient _osduClient;
    private Button? _selectedButton;
    private AppTheme _currentTheme = AppTheme.Light;
    private string? _lastLoadedFilePath;
    private IExample? _lastSelectedExample;
    private SidebarMode _activeMode = SidebarMode.Apis;
    private bool _showOnlyFailed;
    private bool _browseDataInitialized;

    private readonly Dictionary<IExample, ExampleResult> _exampleResults = new();
    private readonly Dictionary<IExample, Button> _exampleButtons = new();
    private readonly List<Expander> _categoryExpanders = new();
    private readonly Dictionary<string, bool> _categoryExpanderStates = new();

    // Cached content state per mode
    private ContentState? _apisContentState;
    private ContentState? _examplesContentState;

    private enum SidebarMode
    {
        Apis,
        Examples,
        BrowseData
    }

    private record ExampleResult(bool Success, string Output, long ElapsedMs);

    private record ContentState(
        string Title,
        string Description,
        List<UIElement> EndpointChildren,
        string ResponseText,
        string StatusText,
        Brush StatusForeground);

    public ApiTestWindow(IHttpClientFactory httpClientFactory, IEnumerable<IExample> examples, IOsduClient osduClient)
    {
        InitializeComponent();
        _definitionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Specifications", "Apis");
        _httpClient = httpClientFactory.CreateClient("OsduApi");
        _examples = examples;
        _osduClient = osduClient;
        ApplyTheme();
        ApplyToolbarStyles();
        RebuildSidebarForCurrentMode();

        // Initialize the response display service for DataGrid support
        Controls.ResponseDisplayService.Initialize(ResponseTextBox, ResponseDataGrid, ResponseDataGridPanel, DataGridItemCountText, DataGridStatusBar, _currentTheme);
    }

    // ─── Tab Switching ───────────────────────────────────────────────

    private void ApisTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == SidebarMode.Apis) return;
        SaveCurrentContentState();
        _activeMode = SidebarMode.Apis;
        _selectedButton = null;
        ApplyToolbarStyles();
        ShowSidebarContent();
        RebuildSidebarForCurrentMode();
        RestoreContentState(_apisContentState, "Select an API from the sidebar", "Choose a service on the left to view its endpoints.");
    }

    private void ExamplesTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == SidebarMode.Examples) return;
        SaveCurrentContentState();
        _activeMode = SidebarMode.Examples;
        _selectedButton = null;
        ApplyToolbarStyles();
        ShowSidebarContent();
        RebuildSidebarForCurrentMode();
        RestoreContentState(_examplesContentState, "Select an Example", "Choose an example on the left to run it against the OSDU platform.");
    }

    private void BrowseDataTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == SidebarMode.BrowseData) return;
        SaveCurrentContentState();
        _activeMode = SidebarMode.BrowseData;
        _selectedButton = null;
        ApplyToolbarStyles();
        ShowBrowseData();
    }

    private void ShowSidebarContent()
    {
        SidebarContentGrid.Visibility = Visibility.Visible;
        BrowseDataControl.Visibility = Visibility.Collapsed;
    }

    private void ShowBrowseData()
    {
        SidebarContentGrid.Visibility = Visibility.Collapsed;
        BrowseDataControl.Visibility = Visibility.Visible;

        if (!_browseDataInitialized)
        {
            BrowseDataControl.Initialize(_osduClient, _currentTheme);
            _browseDataInitialized = true;
        }
    }

    private void SaveCurrentContentState()
    {
        if (_activeMode == SidebarMode.BrowseData) return;

        var children = new List<UIElement>();
        foreach (UIElement child in EndpointsPanel.Children)
            children.Add(child);
        EndpointsPanel.Children.Clear();

        var state = new ContentState(
            ApiTitleText.Text,
            ApiDescriptionText.Text,
            children,
            ResponseTextBox.Text,
            ResponseStatusText.Text,
            ResponseStatusText.Foreground);

        if (_activeMode == SidebarMode.Apis)
            _apisContentState = state;
        else
            _examplesContentState = state;
    }

    private void RestoreContentState(ContentState? state, string defaultTitle, string defaultDescription)
    {
        EndpointsPanel.Children.Clear();

        if (state != null)
        {
            ApiTitleText.Text = state.Title;
            ApiDescriptionText.Text = state.Description;
            foreach (var child in state.EndpointChildren)
                EndpointsPanel.Children.Add(child);
            ResponseTextBox.Text = state.ResponseText;
            ResponseStatusText.Text = state.StatusText;
            ResponseStatusText.Foreground = state.StatusForeground;
        }
        else
        {
            ApiTitleText.Text = defaultTitle;
            ApiDescriptionText.Text = defaultDescription;
            ResponseTextBox.Clear();
            ResponseStatusText.Text = "";
        }
    }

    private void ClearContent(string title, string description)
    {
        ApiTitleText.Text = title;
        ApiDescriptionText.Text = description;
        EndpointsPanel.Children.Clear();
        ResponseTextBox.Clear();
        ResponseStatusText.Text = "";
    }

    private void ApplyToolbarStyles()
    {
        ApisTabButton.Background = _activeMode == SidebarMode.Apis ? _currentTheme.AccentBrush : _currentTheme.TagBrush;
        ApisTabButton.Foreground = _activeMode == SidebarMode.Apis ? Brushes.White : _currentTheme.TextSecondaryBrush;
        ExamplesTabButton.Background = _activeMode == SidebarMode.Examples ? _currentTheme.AccentBrush : _currentTheme.TagBrush;
        ExamplesTabButton.Foreground = _activeMode == SidebarMode.Examples ? Brushes.White : _currentTheme.TextSecondaryBrush;
        BrowseDataTabButton.Background = _activeMode == SidebarMode.BrowseData ? _currentTheme.AccentBrush : _currentTheme.TagBrush;
        BrowseDataTabButton.Foreground = _activeMode == SidebarMode.BrowseData ? Brushes.White : _currentTheme.TextSecondaryBrush;

        SidebarTitle.Text = _activeMode == SidebarMode.Apis ? "🔌 APIs" : "📝 Examples";
        SidebarSubtitle.Text = _activeMode == SidebarMode.Apis ? "Select a service" : "Select an example";
    }

    // ─── Sidebar ─────────────────────────────────────────────────────

    private void RebuildSidebarForCurrentMode()
    {
        ApiButtonsPanel.Children.Clear();
        _selectedButton = null;
        _exampleButtons.Clear();

        if (_activeMode == SidebarMode.Apis) RebuildApiButtons();
        else if (_activeMode == SidebarMode.Examples) RebuildExampleButtons();
    }

    private void RebuildApiButtons()
    {
        if (!Directory.Exists(_definitionsPath)) return;

        foreach (var file in Directory.GetFiles(_definitionsPath, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var button = CreateSidebarButton(name[..1].ToUpperInvariant() + name[1..], file);
            button.Click += ApiButton_Click;
            ApiButtonsPanel.Children.Add(button);

            if (file == _lastLoadedFilePath)
            {
                _selectedButton = button;
                button.Foreground = _currentTheme.AccentBrush;
            }
        }
    }

    private void RebuildExampleButtons()
    {
        _categoryExpanders.Clear();

        // Toolbar
        var toolbar = new Border
        {
            Background = _currentTheme.TagBrush,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(8, 8, 8, 4),
            Padding = new Thickness(6)
        };
        var row = new WrapPanel();

        var runAll = CreateActionButton("▶  Run All", _currentTheme.AccentBrush);
        runAll.Click += async (_, _) => await RunExamplesAsync(_examples);
        row.Children.Add(runAll);

        var reset = CreateActionButton("⟲  Reset", _currentTheme.TextSecondaryBrush);
        reset.Click += (_, _) =>
        {
            _exampleResults.Clear();
            _showOnlyFailed = false;
            RebuildSidebarForCurrentMode();
            ClearContent("Select an Example", "All results cleared.");
        };
        row.Children.Add(reset);

        var failedCount = _exampleResults.Count(r => !r.Value.Success);
        var filter = CreateActionButton(_showOnlyFailed ? "✕  Show All" : $"⚠  Failed ({failedCount})",
            failedCount > 0 ? ExampleColors.FailureBrush : _currentTheme.TextMutedBrush);
        filter.IsEnabled = failedCount > 0 || _showOnlyFailed;
        filter.Click += (_, _) =>
        {
            _showOnlyFailed = !_showOnlyFailed;
            RebuildSidebarForCurrentMode();
        };
        row.Children.Add(filter);

        var expandCollapse = CreateActionButton("⊞  Expand All", _currentTheme.TextSecondaryBrush);
        expandCollapse.Click += (_, _) =>
        {
            var allExpanded = _categoryExpanders.All(exp => exp.IsExpanded);
            foreach (var exp in _categoryExpanders)
                exp.IsExpanded = !allExpanded;
            expandCollapse.Content = allExpanded ? "⊞  Expand All" : "⊟  Collapse All";
        };
        row.Children.Add(expandCollapse);

        toolbar.Child = row;
        ApiButtonsPanel.Children.Add(toolbar);

        // Categories
        foreach (var group in _examples.GroupBy(ex => ex.Category).OrderBy(g => g.Key))
        {
            var all = group.ToList();
            var visible = _showOnlyFailed
                ? all.Where(ex => _exampleResults.TryGetValue(ex, out var r) && !r.Success).ToList()
                : all;

            if (visible.Count == 0) continue;

            var catBorder = new Border
            {
                Background = _currentTheme.IsDark
                    ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8, 6, 8, 2),
                Padding = new Thickness(2)
            };

            var isExpanded = _categoryExpanderStates.TryGetValue(group.Key, out var savedState) && savedState;

            var expander = new Expander
            {
                IsExpanded = isExpanded,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = _currentTheme.TextPrimaryBrush,
                Margin = new Thickness(2)
            };
            // Hide default toggle arrow
            expander.SetResourceReference(Expander.StyleProperty, "ExpanderWithoutToggle");
            expander.Header = BuildCategoryHeader(group.Key, all, expander);
            _categoryExpanders.Add(expander);

            // Track expander state changes
            var categoryKey = group.Key;
            expander.Expanded += (_, _) => _categoryExpanderStates[categoryKey] = true;
            expander.Collapsed += (_, _) => _categoryExpanderStates[categoryKey] = false;

            var items = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            foreach (var example in visible)
            {
                var btn = CreateExampleButton(example);
                btn.Click += ExampleButton_Click;
                items.Children.Add(btn);
                _exampleButtons[example] = btn;
                if (example == _lastSelectedExample) _selectedButton = btn;
            }

            expander.Content = items;
            catBorder.Child = expander;
            ApiButtonsPanel.Children.Add(catBorder);
        }

        ApplyResultColors();
    }

    private StackPanel BuildCategoryHeader(string categoryName, List<IExample> all, Expander expander)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };

        var toggle = new TextBlock
        {
            Text = expander.IsExpanded ? "−" : "+",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = _currentTheme.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        expander.Expanded += (_, _) => toggle.Text = "−";
        expander.Collapsed += (_, _) => toggle.Text = "+";
        header.Children.Add(toggle);

        var runCat = new Button
        {
            Content = "▶",
            Background = Brushes.Transparent,
            Foreground = _currentTheme.AccentBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 6, 0),
            FontSize = 13,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        runCat.Click += async (_, _) => await RunExamplesAsync(all);
        header.Children.Add(runCat);

        header.Children.Add(new TextBlock
        {
            Text = categoryName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = _currentTheme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var failed = all.Count(ex => _exampleResults.TryGetValue(ex, out var r) && !r.Success);
        var passed = all.Count(ex => _exampleResults.TryGetValue(ex, out var r) && r.Success);
        var badgeText = _exampleResults.Any(r => all.Contains(r.Key)) ? $"{passed}✓ {failed}✗" : all.Count.ToString();
        var badgeColor = failed > 0 ? ExampleColors.FailureBrush :
            passed > 0 ? ExampleColors.SuccessBrush : _currentTheme.TextMutedBrush;

        header.Children.Add(new Border
        {
            Background = _currentTheme.TagBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            { Text = badgeText, Foreground = badgeColor, FontSize = 10, FontWeight = FontWeights.SemiBold }
        });

        return header;
    }

    private Button CreateExampleButton(IExample example)
    {
        var hasResult = _exampleResults.TryGetValue(example, out var result);
        var statusColor = hasResult ? ExampleColors.StatusBrush(result!.Success) : _currentTheme.TextMutedBrush;

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = hasResult ? "●" : "○",
            Foreground = statusColor,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var namePanel = new StackPanel();
        namePanel.Children.Add(new TextBlock
        { Text = example.Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
        if (hasResult)
            namePanel.Children.Add(new TextBlock
            {
                Text = $"{result!.ElapsedMs}ms",
                FontSize = 10,
                Foreground = _currentTheme.TextMutedBrush,
                Margin = new Thickness(0, 1, 0, 0)
            });
        content.Children.Add(namePanel);

        var isSelected = example == _lastSelectedExample;
        return new Button
        {
            Content = content,
            Tag = example,
            Background = isSelected ? AccentTint() : Brushes.Transparent,
            Foreground = isSelected ? _currentTheme.AccentBrush : _currentTheme.TextSecondaryBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontWeight = FontWeights.Medium,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private Button CreateSidebarButton(string content, object tag) => new()
    {
        Content = content,
        Tag = tag,
        Background = Brushes.Transparent,
        Foreground = _currentTheme.TextSecondaryBrush,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(14, 10, 14, 10),
        HorizontalContentAlignment = HorizontalAlignment.Left,
        FontSize = 13,
        FontWeight = FontWeights.Medium,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private Button CreateActionButton(string text, SolidColorBrush foreground) => new()
    {
        Content = text,
        Background = Brushes.Transparent,
        Foreground = foreground,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(8, 5, 8, 5),
        HorizontalContentAlignment = HorizontalAlignment.Left,
        FontSize = 11,
        FontWeight = FontWeights.Bold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private SolidColorBrush AccentTint() =>
        new(Color.FromArgb(20, _currentTheme.Accent.R, _currentTheme.Accent.G, _currentTheme.Accent.B));

    private void SelectButton(Button button)
    {
        if (_selectedButton != null)
        {
            _selectedButton.Foreground = _currentTheme.TextSecondaryBrush;
            _selectedButton.Background = Brushes.Transparent;
        }

        _selectedButton = button;
        _selectedButton.Foreground = _currentTheme.AccentBrush;
        _selectedButton.Background = AccentTint();
    }

    private void ApplyResultColors()
    {
        foreach (var (example, button) in _exampleButtons)
        {
            var selected = example == _lastSelectedExample;
            button.Foreground = selected ? _currentTheme.AccentBrush : _currentTheme.TextSecondaryBrush;
            button.Background = selected ? AccentTint() : Brushes.Transparent;
        }
    }

    private void UpdateExampleButtonStatus(IExample example)
    {
        if (!_exampleButtons.TryGetValue(example, out var button)) return;
        if (button.Content is not StackPanel sp || sp.Children[0] is not TextBlock indicator) return;

        if (_exampleResults.TryGetValue(example, out var result))
        {
            indicator.Text = "●";
            indicator.Foreground = ExampleColors.StatusBrush(result.Success);
            if (sp.Children[1] is StackPanel np)
            {
                if (np.Children.Count > 1 && np.Children[1] is TextBlock t) t.Text = $"{result.ElapsedMs}ms";
                else
                    np.Children.Add(new TextBlock
                    {
                        Text = $"{result.ElapsedMs}ms",
                        FontSize = 10,
                        Foreground = _currentTheme.TextMutedBrush,
                        Margin = new Thickness(0, 1, 0, 0)
                    });
            }
        }
        else
        {
            indicator.Text = "○";
            indicator.Foreground = _currentTheme.TextMutedBrush;
        }
    }

    // ─── Run Examples ────────────────────────────────────────────────

    private async System.Threading.Tasks.Task RunExamplesAsync(IEnumerable<IExample> examples)
    {
        var list = examples.ToList();
        ResponseTextBox.Clear();
        ResponseStatusText.Text = $"⏳ Running {list.Count} example(s)...";
        ResponseStatusText.Foreground = _currentTheme.TextSecondaryBrush;
        ApiTitleText.Text = "Running Examples";
        ApiDescriptionText.Text = $"Executing {list.Count} example(s)...";
        EndpointsPanel.Children.Clear();

        int passed = 0, failed = 0;
        var summary = new StringBuilder();

        foreach (var example in list)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await example.RunAsync();
                sw.Stop();
                _exampleResults[example] = new ExampleResult(true, result, sw.ElapsedMilliseconds);
                passed++;
                summary.AppendLine($"  ✅ {example.Text} ({sw.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _exampleResults[example] = new ExampleResult(false,
                    $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", sw.ElapsedMilliseconds);
                failed++;
                summary.AppendLine(
                    $"  ❌ {example.Text} ({sw.ElapsedMilliseconds}ms) - {ex.GetType().Name}: {ex.Message}");
            }

            UpdateExampleButtonStatus(example);
            ResponseStatusText.Text = $"⏳ Running... ({passed + failed}/{list.Count})";
        }

        RebuildSidebarForCurrentMode();

        var totalMs = _exampleResults.Where(r => list.Contains(r.Key)).Sum(r => r.Value.ElapsedMs);
        ResponseStatusText.Text = $"{(failed == 0 ? "✅" : "⚠️")} {passed} passed, {failed} failed — {totalMs}ms total";
        ResponseStatusText.Foreground = failed == 0 ? ExampleColors.SuccessBrush : ExampleColors.FailureBrush;
        ApiTitleText.Text = "Run Results";
        ApiDescriptionText.Text = $"{passed} passed, {failed} failed out of {list.Count} examples.";
        ResponseTextBox.Text =
            $"═══ Execution Summary ═══\nTotal: {list.Count}  |  Passed: {passed}  |  Failed: {failed}  |  Time: {totalMs}ms\n\n{summary}";

        var resultsBuilder = new ResultsSummaryBuilder(_currentTheme);
        resultsBuilder.OnExampleClicked += ShowExampleResult;
        var dict = _exampleResults.ToDictionary(r => r.Key, r => (r.Value.Success, r.Value.Output, r.Value.ElapsedMs));
        resultsBuilder.Build(EndpointsPanel, list, dict);
    }

    private void ShowExampleResult(IExample example)
    {
        if (!_exampleResults.TryGetValue(example, out var result)) return;
        _lastSelectedExample = example;
        if (_exampleButtons.TryGetValue(example, out var btn)) SelectButton(btn);

        ApiTitleText.Text = example.Text;
        ApiDescriptionText.Text =
            result.Success ? $"✅ Completed in {result.ElapsedMs}ms" : $"❌ Failed after {result.ElapsedMs}ms";
        ResponseStatusText.Text = $"{(result.Success ? "✅" : "❌")} {result.ElapsedMs}ms";
        ResponseStatusText.Foreground = ExampleColors.StatusBrush(result.Success);

        var output = result.Output;
        try
        {
            output = JsonSerializer.Serialize(JsonDocument.Parse(output),
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch
        {
        }

        ResponseTextBox.Text = output;
    }

    // ─── API Mode ────────────────────────────────────────────────────

    private void ApiButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        SelectButton(button);
        _lastLoadedFilePath = (string)button.Tag;
        LoadApiFromFile(_lastLoadedFilePath);
    }

    private void LoadApiFromFile(string filePath)
    {
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var root = doc.RootElement;
            ApiTitleText.Text = root.GetProperty("info").GetProperty("title").GetString() ?? "API";
            ApiDescriptionText.Text = root.GetProperty("info").TryGetProperty("description", out var d)
                ? d.GetString() ?? ""
                : "";
            EndpointsPanel.Children.Clear();
            ResponseTextBox.Clear();
            ResponseStatusText.Text = "";
            new ApiUiBuilder(ResponseTextBox, ResponseStatusText, _httpClient, _currentTheme).BuildEndpointsUi(root,
                EndpointsPanel);
        }
        catch (Exception ex)
        {
            ResponseTextBox.Text = $"Error loading API: {ex.Message}";
        }
    }

    // ─── Examples Mode ───────────────────────────────────────────────

    private void ExampleButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var example = (IExample)button.Tag;
        SelectButton(button);
        _lastSelectedExample = example;

        if (_exampleResults.ContainsKey(example)) ShowExampleResult(example);
        ShowExample(example);
    }

    private void ShowExample(IExample example)
    {
        ApiTitleText.Text = example.Text;
        ApiDescriptionText.Text = example.ShortDescription;
        EndpointsPanel.Children.Clear();

        var cardBuilder = new ExampleCardBuilder(_currentTheme);
        cardBuilder.RunRequested += async ex => await RunSingleExampleAsync(ex);
        cardBuilder.ValidationFailed += error =>
        {
            ResponseStatusText.Text = "❌ Validation Error";
            ResponseStatusText.Foreground = ExampleColors.FailureBrush;
            ResponseTextBox.Text = error;
        };
        EndpointsPanel.Children.Add(cardBuilder.Build(example));
    }

    private async System.Threading.Tasks.Task RunSingleExampleAsync(IExample example)
    {
        Controls.ResponseDisplayService.ShowTextBox(); // Reset to text mode
        ResponseTextBox.Clear();
        ResponseStatusText.Text = "⏳ Running...";
        ResponseStatusText.Foreground = _currentTheme.TextSecondaryBrush;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await example.RunAsync();
            sw.Stop();
            _exampleResults[example] = new ExampleResult(true, result, sw.ElapsedMilliseconds);
            UpdateExampleButtonStatus(example);
            ResponseStatusText.Text = $"✅ Completed — {sw.ElapsedMilliseconds}ms";
            ResponseStatusText.Foreground = ExampleColors.SuccessBrush;
            try
            {
                result = JsonSerializer.Serialize(JsonDocument.Parse(result),
                    new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            }
            catch
            {
            }

            ResponseTextBox.Text = result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var error = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
            _exampleResults[example] = new ExampleResult(false, error, sw.ElapsedMilliseconds);
            UpdateExampleButtonStatus(example);
            ResponseStatusText.Text = $"❌ Error — {sw.ElapsedMilliseconds}ms";
            ResponseStatusText.Foreground = ExampleColors.FailureBrush;
            ResponseTextBox.Text = error;
        }
    }

    // ─── Theme ───────────────────────────────────────────────────────

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme.IsDark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme();
        ApplyToolbarStyles();
        RebuildSidebarForCurrentMode();

        if (_browseDataInitialized)
            BrowseDataControl.UpdateTheme(_currentTheme);

        if (_activeMode == SidebarMode.Apis && _lastLoadedFilePath != null) LoadApiFromFile(_lastLoadedFilePath);
        else if (_activeMode == SidebarMode.Examples && _lastSelectedExample != null) ShowExample(_lastSelectedExample);
    }

    private void ApplyTheme()
    {
        Background = _currentTheme.SurfaceBrush;

        // Toolbar
        ToolbarBorder.Background = _currentTheme.SidebarBrush;
        ToolbarBorder.BorderBrush = _currentTheme.BorderBrush;
        AppTitle.Foreground = _currentTheme.TextPrimaryBrush;
        NavButtonsBorder.Background = _currentTheme.TagBrush;
        ThemeToggleButton.Content = _currentTheme.IsDark ? "☀️ Light" : "🌙 Dark";
        ThemeToggleButton.Foreground = _currentTheme.TextSecondaryBrush;
        ThemeToggleButton.Background = _currentTheme.TagBrush;

        // Sidebar
        SidebarBorder.Background = _currentTheme.SidebarBrush;
        SidebarBorder.BorderBrush = _currentTheme.BorderBrush;
        SidebarTitle.Foreground = _currentTheme.TextPrimaryBrush;
        SidebarSubtitle.Foreground = _currentTheme.TextSecondaryBrush;
        SidebarHeaderBorder.BorderBrush = _currentTheme.BorderBrush;

        // Content
        TitleBarBorder.BorderBrush = _currentTheme.BorderBrush;
        ApiTitleText.Foreground = _currentTheme.TextPrimaryBrush;
        ApiDescriptionText.Foreground = _currentTheme.TextSecondaryBrush;
        ResponsePanelBorder.Background = _currentTheme.ResponseBgBrush;
        ResponsePanelBorder.BorderBrush = _currentTheme.BorderBrush;
        ResponseHeaderBorder.BorderBrush = _currentTheme.BorderBrush;
        ResponseLabel.Foreground = _currentTheme.TextPrimaryBrush;
        ResponseStatusText.Foreground = _currentTheme.TextSecondaryBrush;
        ResponseTextBox.Background = Brushes.Transparent;
        ResponseTextBox.Foreground = _currentTheme.IsDark
            ? new SolidColorBrush(Color.FromRgb(200, 200, 212))
            : _currentTheme.TextPrimaryBrush;
        ResponseTextBox.CaretBrush = _currentTheme.TextPrimaryBrush;
        ColumnSplitter.Background = _currentTheme.BorderBrush;
        RowSplitter.Background = _currentTheme.BorderBrush;
        ContentGrid.Background = _currentTheme.SurfaceBrush;
        CopyResponseButton.Foreground = _currentTheme.TextSecondaryBrush;

        Controls.ResponseDisplayService.UpdateTheme(_currentTheme);
    }

    private void CopyResponse_Click(object sender, RoutedEventArgs e)
    {
        if (Controls.ResponseDisplayService.IsDataGridVisible)
        {
            Controls.ResponseDisplayService.CopySelectedToClipboard();
        }
        else if (!string.IsNullOrEmpty(ResponseTextBox.Text))
        {
            Clipboard.SetText(ResponseTextBox.Text);
        }
    }

    private Style CreateExpanderStyle()
    {
        var style = new Style(typeof(Expander));

        var template = new ControlTemplate(typeof(Expander));

        var border = new FrameworkElementFactory(typeof(DockPanel));

        // Header row
        var headerRow = new FrameworkElementFactory(typeof(DockPanel));
        headerRow.SetValue(DockPanel.DockProperty, Dock.Top);

        var toggleButton = new FrameworkElementFactory(typeof(ToggleButton));
        toggleButton.SetValue(ToggleButton.BackgroundProperty, Brushes.Transparent);
        toggleButton.SetValue(ToggleButton.BorderThicknessProperty, new Thickness(0));
        toggleButton.SetValue(ToggleButton.PaddingProperty, new Thickness(4, 0, 6, 0));
        toggleButton.SetValue(ToggleButton.FontSizeProperty, 14.0);
        toggleButton.SetValue(ToggleButton.FontWeightProperty, FontWeights.Bold);
        toggleButton.SetValue(ToggleButton.CursorProperty, System.Windows.Input.Cursors.Hand);
        toggleButton.SetValue(ToggleButton.VerticalAlignmentProperty, VerticalAlignment.Center);
        toggleButton.SetValue(ToggleButton.ForegroundProperty, _currentTheme.TextSecondaryBrush);
        toggleButton.SetValue(ToggleButton.ContentProperty, "+");
        toggleButton.SetBinding(ToggleButton.IsCheckedProperty,
            new System.Windows.Data.Binding("IsExpanded")
            {
                Source = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        // Use triggers on the toggle to swap +/-
        var checkedTrigger = new System.Windows.Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(ToggleButton.ContentProperty, "−"));

        var toggleStyle = new Style(typeof(ToggleButton));
        toggleStyle.Triggers.Add(checkedTrigger);
        toggleButton.SetValue(ToggleButton.StyleProperty, toggleStyle);

        headerRow.AppendChild(toggleButton);

        var contentPresenterHeader = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenterHeader.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentPresenterHeader.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        headerRow.AppendChild(contentPresenterHeader);

        border.AppendChild(headerRow);

        var contentHost = new FrameworkElementFactory(typeof(ContentPresenter));
        contentHost.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        contentHost.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        contentHost.SetBinding(UIElement.VisibilityProperty,
            new System.Windows.Data.Binding("IsExpanded")
            {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
                Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
            });
        border.AppendChild(contentHost);

        template.VisualTree = border;
        style.Setters.Add(new Setter(Expander.TemplateProperty, template));

        return style;
    }
}
