using System.Text.Json;
using System.Windows;
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
    private List<JsonElement> _currentRecords = [];
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
        _nextCursor = null;
        _pageIndex = 0;
        FetchAllButton.IsEnabled = true;
        PagingBar.Visibility = Visibility.Visible;
        await FetchPageAsync(null);
    }

    private async Task FetchPageAsync(string? cursor)
    {
        if (_selectedKind is null) return;

        try
        {
            SetStatus($"Querying {_selectedKind}...");
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;

            var page = await _service.SearchByKindAsync(_selectedKind, _pageSize, cursor);
            _currentRecords = page.Results;
            _totalCount = page.TotalCount;
            _nextCursor = page.Cursor;

            // Store the cursor for the next page if we haven't seen it yet
            if (!string.IsNullOrEmpty(_nextCursor) && _cursorByPage.Count <= _pageIndex + 1)
            {
                _cursorByPage.Add(_nextCursor);
            }

            DisplayRecords();
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

    private void DisplayRecords()
    {
        RawView.SetData(_currentRecords, _totalCount);
        TabularView.SetData(_currentRecords);
        TreeView.SetData(_currentRecords);
        DetailView.SetData(_currentRecords);
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
        int to = _pageIndex * _pageSize + _currentRecords.Count;
        int totalPages = TotalPages;

        PageInfoText.Text = _totalCount > 0
            ? $"Page {_pageIndex + 1}{(totalPages > 0 ? $" of {totalPages}" : "")}  |  {from}–{to} of {_totalCount}"
            : "No results";

        RecordCountText.Text = _totalCount > 0
            ? $"Showing {_currentRecords.Count} of {_totalCount}"
            : "";
    }

    private async void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _pageIndex = 0;
        await FetchPageAsync(_cursorByPage[0]);
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _pageIndex++;
        await FetchPageAsync(_nextCursor);
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex <= 0) return;
        _pageIndex--;
        var cursor = _pageIndex < _cursorByPage.Count ? _cursorByPage[_pageIndex] : null;
        await FetchPageAsync(cursor);
    }

    private async void LastPage_Click(object sender, RoutedEventArgs e)
    {
        // Fetch all to reach the last page, then display only the last page's worth
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
                _currentRecords = page.Results;
                _totalCount = page.TotalCount;
                _nextCursor = page.Cursor;
                cursor = _nextCursor;

                if (!string.IsNullOrEmpty(_nextCursor) && _cursorByPage.Count <= _pageIndex + 1)
                    _cursorByPage.Add(_nextCursor);
            }

            DisplayRecords();
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

        // If we already have the cursor for that page, jump directly
        if (targetIndex < _cursorByPage.Count)
        {
            _pageIndex = targetIndex;
            await FetchPageAsync(_cursorByPage[_pageIndex]);
            return;
        }

        // Otherwise walk forward from the furthest known page
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

                if (!string.IsNullOrEmpty(page.Cursor) && _cursorByPage.Count <= currentIdx)
                    _cursorByPage.Add(page.Cursor);

                if (currentIdx == targetIndex)
                {
                    _currentRecords = page.Results;
                    _nextCursor = page.Cursor;
                    _pageIndex = targetIndex;
                    DisplayRecords();
                    UpdatePagingControls();
                    SetStatus($"Kind: {_selectedKind}");
                    return;
                }

                cursor = page.Cursor;
                if (string.IsNullOrEmpty(cursor))
                {
                    // Reached the end before the target page
                    _currentRecords = page.Results;
                    _nextCursor = null;
                    _pageIndex = currentIdx - 1;
                    DisplayRecords();
                    UpdatePagingControls();
                    SetStatus($"Only {currentIdx} pages available.");
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
        _nextCursor = null;
        await FetchPageAsync(null);
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
            _currentRecords = all;
            _totalCount = all.Count;
            _nextCursor = null;
            _pageIndex = 0;

            DisplayRecords();

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
        Background = theme.SurfaceBrush;

        // Toolbar
        MainToolbar.Background = theme.SidebarBrush;
        MainToolbar.Foreground = theme.TextPrimaryBrush;

        // StatusBar
        MainStatusBar.Background = new SolidColorBrush(theme.Sidebar);
        StatusText.Foreground = theme.TextSecondaryBrush;
        RecordCountText.Foreground = theme.TextSecondaryBrush;
        PageInfoText.Foreground = theme.TextSecondaryBrush;

        // Paging bar
        PagingBar.Background = theme.SidebarBrush;
        PagingBar.BorderBrush = theme.TextSecondaryBrush;
        PageSizeLabel.Foreground = theme.TextSecondaryBrush;
        GoToLabel.Foreground = theme.TextSecondaryBrush;

        // Child controls
        KindTree.ApplyTheme(theme);
        RawView.ApplyTheme(theme);
        TabularView.ApplyTheme(theme);
        TreeView.ApplyTheme(theme);
        DetailView.ApplyTheme(theme);
    }

    private void ClearContent()
    {
        RawView.Clear();
        TabularView.Clear();
        TreeView.Clear();
        DetailView.Clear();
        _selectedKind = null;
        _currentRecords.Clear();
        RecordCountText.Text = "";
        PageInfoText.Text = "";
        FetchAllButton.IsEnabled = false;
        PagingBar.Visibility = Visibility.Collapsed;
    }

    private void SetStatus(string text) => StatusText.Text = text;
}