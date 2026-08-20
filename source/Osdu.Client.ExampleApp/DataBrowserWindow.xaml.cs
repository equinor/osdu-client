using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Services;

namespace Osdu.Client.ExampleApp;

public partial class DataBrowserWindow : Window
{
    private readonly DataBrowserService _service;
    private AppTheme _theme = AppTheme.Light;

    // Paging state
    private string? _selectedKind;
    private string? _nextCursor;
    private readonly List<string?> _cursorByPage = []; // cursor to fetch page[i]
    private List<JsonElement> _allRecords = [];        // accumulated records across pages
    private List<JsonElement> _currentPageRecords = [];
    private long _totalCount;
    private int _pageIndex;
    private int _pageSize = 100;

    public DataBrowserWindow(IOsduClient osduClient)
    {
        InitializeComponent();
        _service = new DataBrowserService(osduClient);
        KindTree.KindSelected += OnKindSelected;
        ApplyTheme(_theme);
        Loaded += async (_, _) => await LoadKindsAsync();
    }

    private int TotalPages => _totalCount > 0 ? (int)Math.Ceiling((double)_totalCount / _pageSize) : 0;

    /// <summary>Gets the starting row index for a given page.</summary>
    private int PageStartRow(int pageIdx) => pageIdx * _pageSize;

    private async Task LoadKindsAsync()
    {
        try
        {
            SetStatus("Loading kinds...");
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;

            var groups = await _service.GetGroupedKindsAsync();
            KindTree.LoadKinds(groups);

            int total = groups.Sum(g => g.Kinds.Count);
            SetStatus($"Loaded {total} kinds");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnKindSelected(string kindId)
    {
        _selectedKind = kindId;
        _cursorByPage.Clear();
        _cursorByPage.Add(null); // page 0 starts with null cursor
        _allRecords.Clear();
        _nextCursor = null;
        _pageIndex = 0;
        FetchAllButton.IsEnabled = true;
        PagingBar.Visibility = Visibility.Visible;
        await FetchAndAppendPageAsync(null);
    }

    /// <summary>
    /// Fetches a page and appends it to the accumulated records.
    /// Only fetches if data for this page hasn't been loaded yet.
    /// </summary>
    private async Task FetchAndAppendPageAsync(string? cursor)
    {
        if (_selectedKind is null) return;

        // Check if we already have data for this page
        int expectedStart = PageStartRow(_pageIndex);
        if (expectedStart < _allRecords.Count)
        {
            // Data already loaded — just display the slice and scroll
            int count = Math.Min(_pageSize, _allRecords.Count - expectedStart);
            _currentPageRecords = _allRecords.GetRange(expectedStart, count);
            DisplayAllAccumulated();
            ScrollToPageStart();
            UpdatePagingControls();
            SetStatus($"Kind: {_selectedKind}");
            return;
        }

        try
        {
            SetStatus($"Querying {_selectedKind}...");
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;

            var page = await _service.SearchByKindAsync(_selectedKind, _pageSize, cursor);
            _currentPageRecords = page.Results;
            _totalCount = page.TotalCount;
            _nextCursor = page.Cursor;

            // Append new records to accumulated list
            _allRecords.AddRange(page.Results);

            // Store cursor for next page
            if (!string.IsNullOrEmpty(_nextCursor) && _cursorByPage.Count <= _pageIndex + 1)
            {
                _cursorByPage.Add(_nextCursor);
            }

            DisplayAllAccumulated();
            ScrollToPageStart();
            UpdatePagingControls();
            SetStatus($"Kind: {_selectedKind}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Displays all accumulated records in all views.</summary>
    private void DisplayAllAccumulated()
    {
        RawView.SetData(_allRecords, _totalCount);
        TabularView.SetData(_allRecords);
        TreeView.SetData(_allRecords);
        DetailView.SetData(_allRecords);
    }

    /// <summary>Scrolls the tabular view to the first row of the current page and selects it.</summary>
    private void ScrollToPageStart()
    {
        int startRow = PageStartRow(_pageIndex);
        TabularView.ScrollToRowAndHighlight(startRow);
    }

    private void UpdatePagingControls()
    {
        bool hasPrev = _pageIndex > 0;
        bool hasNext = !string.IsNullOrEmpty(_nextCursor);

        FirstPageButton.IsEnabled = hasPrev;
        PrevPageButton.IsEnabled = hasPrev;
        NextPageButton.IsEnabled = hasNext;
        LastPageButton.IsEnabled = hasNext;

        int from = _pageIndex * _pageSize + 1;
        int to = _pageIndex * _pageSize + _currentPageRecords.Count;
        int totalPages = TotalPages;

        PageInfoText.Text = _totalCount > 0
            ? $"Page {_pageIndex + 1}{(totalPages > 0 ? $" of {totalPages}" : "")}  |  {from}–{to} of {_totalCount}"
            : "No results";

        RecordCountText.Text = _totalCount > 0
            ? $"Showing {_allRecords.Count} of {_totalCount} (loaded)"
            : "";
    }

    private async void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _pageIndex = 0;
        await FetchAndAppendPageAsync(_cursorByPage[0]);
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _pageIndex++;
        await FetchAndAppendPageAsync(_nextCursor);
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex <= 0) return;
        _pageIndex--;
        // Data is already accumulated — just scroll back
        int startRow = PageStartRow(_pageIndex);
        int count = Math.Min(_pageSize, _allRecords.Count - startRow);
        _currentPageRecords = _allRecords.GetRange(startRow, count);
        ScrollToPageStart();
        UpdatePagingControls();
        SetStatus($"Kind: {_selectedKind}");
    }

    private async void LastPage_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKind is null) return;

        try
        {
            SetStatus("Navigating to last page...");
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;

            // Walk through pages until we reach the end
            string? cursor = _nextCursor;
            while (!string.IsNullOrEmpty(cursor))
            {
                _pageIndex++;
                if (_cursorByPage.Count <= _pageIndex)
                    _cursorByPage.Add(cursor);

                var page = await _service.SearchByKindAsync(_selectedKind, _pageSize, cursor);
                _allRecords.AddRange(page.Results);
                _currentPageRecords = page.Results;
                _totalCount = page.TotalCount;
                _nextCursor = page.Cursor;
                cursor = _nextCursor;

                if (!string.IsNullOrEmpty(_nextCursor) && _cursorByPage.Count <= _pageIndex + 1)
                    _cursorByPage.Add(_nextCursor);
            }

            DisplayAllAccumulated();
            ScrollToPageStart();
            UpdatePagingControls();
            SetStatus($"Kind: {_selectedKind}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void GoToPageBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await GoToPageAsync();
    }

    private async void GoToPage_Click(object sender, RoutedEventArgs e)
    {
        await GoToPageAsync();
    }

    private async Task GoToPageAsync()
    {
        if (!int.TryParse(GoToPageBox.Text.Trim(), out int targetPage) || targetPage < 1)
        {
            SetStatus("Enter a valid page number.");
            return;
        }

        int targetIndex = targetPage - 1;
        if (targetIndex == _pageIndex) return;

        // If data is already loaded for this page, just scroll
        int targetStart = PageStartRow(targetIndex);
        if (targetStart < _allRecords.Count)
        {
            _pageIndex = targetIndex;
            int count = Math.Min(_pageSize, _allRecords.Count - targetStart);
            _currentPageRecords = _allRecords.GetRange(targetStart, count);
            ScrollToPageStart();
            UpdatePagingControls();
            SetStatus($"Kind: {_selectedKind}");
            return;
        }

        // If we have the cursor, walk forward fetching and appending
        try
        {
            SetStatus($"Navigating to page {targetPage}...");
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;

            // Start from the last known cursor
            int currentIdx = _cursorByPage.Count - 1;
            string? cursor = _cursorByPage[currentIdx];

            while (currentIdx < targetIndex)
            {
                var page = await _service.SearchByKindAsync(_selectedKind!, _pageSize, cursor);
                _totalCount = page.TotalCount;
                currentIdx++;

                if (_cursorByPage.Count <= currentIdx && !string.IsNullOrEmpty(page.Cursor))
                    _cursorByPage.Add(page.Cursor);

                _allRecords.AddRange(page.Results);

                if (currentIdx == targetIndex)
                {
                    _currentPageRecords = page.Results;
                    _nextCursor = page.Cursor;
                    _pageIndex = targetIndex;
                    DisplayAllAccumulated();
                    ScrollToPageStart();
                    UpdatePagingControls();
                    SetStatus($"Kind: {_selectedKind}");
                    return;
                }

                cursor = page.Cursor;
                if (string.IsNullOrEmpty(cursor))
                {
                    _currentPageRecords = page.Results;
                    _nextCursor = null;
                    _pageIndex = currentIdx;
                    DisplayAllAccumulated();
                    ScrollToPageStart();
                    UpdatePagingControls();
                    SetStatus($"Only {currentIdx + 1} pages available.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void PageSizeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PageSizeCombo.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        if (!int.TryParse(item.Content?.ToString(), out int newSize)) return;
        if (newSize == _pageSize || _selectedKind is null) { _pageSize = newSize; return; }

        _pageSize = newSize;
        _pageIndex = 0;
        _cursorByPage.Clear();
        _cursorByPage.Add(null);
        _allRecords.Clear();
        _nextCursor = null;
        await FetchAndAppendPageAsync(null);
    }

    private async void FetchAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKind is null) return;

        try
        {
            FetchAllButton.IsEnabled = false;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Maximum = _totalCount > 0 ? _totalCount : 1000;
            ProgressBar.Value = 0;
            ProgressBar.Visibility = Visibility.Visible;
            SetStatus($"Fetching all records for {_selectedKind}...");

            var progress = new Progress<int>(count =>
            {
                ProgressBar.Value = count;
                RecordCountText.Text = $"Fetched {count}...";
            });

            var all = await _service.FetchAllAsync(_selectedKind, progress);
            _allRecords = all;
            _currentPageRecords = all;
            _totalCount = all.Count;
            _nextCursor = null;
            _pageIndex = 0;

            DisplayAllAccumulated();

            FirstPageButton.IsEnabled = false;
            PrevPageButton.IsEnabled = false;
            NextPageButton.IsEnabled = false;
            LastPageButton.IsEnabled = false;
            PageInfoText.Text = $"All {all.Count} records";
            RecordCountText.Text = $"Total: {all.Count}";
            SetStatus("Fetch all complete");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            ProgressBar.Visibility = Visibility.Collapsed;
            FetchAllButton.IsEnabled = true;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        ClearContent();
        await LoadKindsAsync();
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _theme = _theme.IsDark ? AppTheme.Light : AppTheme.Dark;
        ThemeToggle.Content = _theme.IsDark ? "☀ Light" : "🌙 Dark";
        ApplyTheme(_theme);
    }

    private void ApplyTheme(AppTheme theme)
    {
        // Apply font to the window so all children inherit it
        FontFamily = AppTheme.FontFamily;
        FontSize = AppTheme.FontSize;

        Background = theme.SurfaceBrush;

        // Toolbar
        MainToolbar.Background = theme.SidebarBrush;
        MainToolbar.Foreground = theme.TextPrimaryBrush;

        // Style toolbar buttons
        foreach (var child in MainToolbar.Items)
        {
            if (child is System.Windows.Controls.Primitives.ButtonBase btn)
            {
                btn.Background = theme.CardBrush;
                btn.Foreground = theme.TextPrimaryBrush;
                btn.BorderBrush = theme.BorderBrush;
            }
        }

        // StatusBar
        MainStatusBar.Background = new SolidColorBrush(theme.Sidebar);
        MainStatusBar.Foreground = theme.TextPrimaryBrush;
        MainStatusBar.BorderBrush = theme.BorderBrush;
        MainStatusBar.BorderThickness = new Thickness(0, 1, 0, 0);
        StatusText.Foreground = theme.TextSecondaryBrush;
        RecordCountText.Foreground = theme.TextSecondaryBrush;
        PageInfoText.Foreground = theme.TextSecondaryBrush;

        // Paging bar
        PagingBar.Background = theme.SidebarBrush;
        PagingBar.BorderBrush = theme.BorderBrush;
        PageSizeLabel.Foreground = theme.TextSecondaryBrush;
        GoToLabel.Foreground = theme.TextSecondaryBrush;

        // Paging buttons and inputs
        StylePagingControls(theme);

        // TabControl — flat VS 2026 style
        ContentTabs.Background = theme.SurfaceBrush;
        ContentTabs.BorderBrush = Brushes.Transparent;
        ContentTabs.BorderThickness = new Thickness(0);
        StyleTabControl(theme);

        // GridSplitter
        var splitter = FindVisualChild<GridSplitter>(this);
        if (splitter is not null)
        {
            splitter.Background = theme.BorderBrush;
        }

        // ProgressBar
        ProgressBar.Foreground = theme.AccentBrush;
        ProgressBar.Background = theme.InputBrush;
        ProgressBar.BorderBrush = theme.BorderBrush;

        // Child controls — KindTree sidebar gets a right border to separate from content
        KindTree.ApplyTheme(theme);
        KindTree.BorderBrush = theme.BorderBrush;
        KindTree.BorderThickness = new Thickness(0, 0, 1, 0);

        RawView.ApplyTheme(theme);
        TabularView.ApplyTheme(theme);
        TreeView.ApplyTheme(theme);
        DetailView.ApplyTheme(theme);
    }

    private void StyleTabControl(AppTheme theme)
    {
        var tabItemStyle = new Style(typeof(TabItem));

        var template = new ControlTemplate(typeof(TabItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "TabBorder");
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(14, 7, 14, 7));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 1, 0));
        borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        borderFactory.SetValue(Border.CursorProperty, Cursors.Hand);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock), "TabText");
        textFactory.SetValue(TextBlock.FontSizeProperty, AppTheme.FontSizeSmall);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Normal);
        textFactory.SetValue(TextBlock.ForegroundProperty, theme.TabInactiveTextBrush);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Header")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        borderFactory.AppendChild(textFactory);
        template.VisualTree = borderFactory;

        // Selected trigger
        var selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, theme.TabActiveBrush, "TabBorder"));
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, theme.SurfaceBrush, "TabBorder"));
        selectedTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold, "TabText"));
        selectedTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, theme.TextPrimaryBrush, "TabText"));
        template.Triggers.Add(selectedTrigger);

        // Hover trigger
        var hoverTrigger = new Trigger { Property = TabItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, theme.Accent.R, theme.Accent.G, theme.Accent.B)), "TabBorder"));
        template.Triggers.Add(hoverTrigger);

        tabItemStyle.Setters.Add(new Setter(TabItem.TemplateProperty, template));

        // Apply to all tab items
        foreach (TabItem tab in ContentTabs.Items)
        {
            tab.Style = tabItemStyle;
        }
    }

    private void StylePagingControls(AppTheme theme)
    {
        // Style all buttons in the paging bar
        foreach (var btn in new[] { FirstPageButton, PrevPageButton, NextPageButton, LastPageButton, FetchAllButton })
        {
            btn.Background = theme.CardBrush;
            btn.Foreground = theme.TextPrimaryBrush;
            btn.BorderBrush = theme.BorderBrush;
        }

        // ComboBox
        PageSizeCombo.Background = theme.InputFieldBrush;
        PageSizeCombo.Foreground = theme.TextPrimaryBrush;
        PageSizeCombo.BorderBrush = theme.BorderBrush;

        // GoTo TextBox
        GoToPageBox.Background = theme.InputFieldBrush;
        GoToPageBox.Foreground = theme.TextPrimaryBrush;
        GoToPageBox.BorderBrush = theme.BorderBrush;
        GoToPageBox.CaretBrush = theme.TextPrimaryBrush;

        // Go button (find it by content)
        foreach (var child in LogicalTreeHelper.GetChildren(GoToPageBox.Parent))
        {
            if (child is Button goBtn && goBtn.Content?.ToString() == "Go")
            {
                goBtn.Background = theme.CardBrush;
                goBtn.Foreground = theme.TextPrimaryBrush;
                goBtn.BorderBrush = theme.BorderBrush;
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindVisualChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }

    private void ClearContent()
    {
        RawView.Clear();
        TabularView.Clear();
        TreeView.Clear();
        DetailView.Clear();
        _selectedKind = null;
        _allRecords.Clear();
        _currentPageRecords.Clear();
        RecordCountText.Text = "";
        PageInfoText.Text = "";
        FetchAllButton.IsEnabled = false;
        PagingBar.Visibility = Visibility.Collapsed;
    }

    private void SetStatus(string text) => StatusText.Text = text;
}