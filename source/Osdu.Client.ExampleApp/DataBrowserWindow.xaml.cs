using System.Text.Json;
using System.Windows;
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
    private readonly Stack<string?> _cursorHistory = new(); // previous cursors
    private List<JsonElement> _currentRecords = [];
    private long _totalCount;
    private int _pageIndex;
    private const int PageSize = 100;

    public DataBrowserWindow(IOsduClient osduClient)
    {
        InitializeComponent();
        _service = new DataBrowserService(osduClient);
        KindTree.KindSelected += OnKindSelected;
        ApplyTheme(_theme);
        Loaded += async (_, _) => await LoadKindsAsync();
    }

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
        _cursorHistory.Clear();
        _nextCursor = null;
        _pageIndex = 0;
        FetchAllButton.IsEnabled = true;
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

            var page = await _service.SearchByKindAsync(_selectedKind, PageSize, cursor);
            _currentRecords = page.Results;
            _totalCount = page.TotalCount;
            _nextCursor = page.Cursor;

            DisplayRecords();
            UpdatePagingControls();
            SetStatus($"Kind: {_selectedKind}");
            RecordCountText.Text = $"Showing {_currentRecords.Count} of {_totalCount}";
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
        DataGridView.SetData(_currentRecords);
    }

    private void UpdatePagingControls()
    {
        PrevPageButton.IsEnabled = _pageIndex > 0;
        NextPageButton.IsEnabled = !string.IsNullOrEmpty(_nextCursor);
        int from = _pageIndex * PageSize + 1;
        int to = _pageIndex * PageSize + _currentRecords.Count;
        PageInfoText.Text = _totalCount > 0 ? $"{from}–{to} of {_totalCount}" : "No results";
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _cursorHistory.Push(_nextCursor);
        _pageIndex++;
        await FetchPageAsync(_nextCursor);
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_cursorHistory.Count == 0) return;
        _cursorHistory.Pop(); // discard current
        var prevCursor = _cursorHistory.Count > 0 ? _cursorHistory.Pop() : null;
        _pageIndex = Math.Max(0, _pageIndex - 1);
        await FetchPageAsync(prevCursor);
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
            PrevPageButton.IsEnabled = false;
            NextPageButton.IsEnabled = false;
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

        // Child controls
        KindTree.ApplyTheme(theme);
        RawView.ApplyTheme(theme);
        TabularView.ApplyTheme(theme);
        TreeView.ApplyTheme(theme);
        DataGridView.ApplyTheme(theme);
    }

    private void ClearContent()
    {
        RawView.Clear();
        TabularView.Clear();
        TreeView.Clear();
        DataGridView.Clear();
        _selectedKind = null;
        _currentRecords.Clear();
        RecordCountText.Text = "";
        PageInfoText.Text = "";
        FetchAllButton.IsEnabled = false;
    }

    private void SetStatus(string text) => StatusText.Text = text;
}