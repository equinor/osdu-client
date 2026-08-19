using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Osdu.Client;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp;

public partial class RecordBrowserControl : UserControl
{
    private readonly IOsduClient _osduClient = null!;
    private AppTheme _currentTheme = AppTheme.Light;
    private CancellationTokenSource? _searchCts;

    private readonly List<SchemaKindItem> _allKinds = [];
    private string? _selectedKind;
    private JsonElement? _selectedRecord;
    private DetailTab _activeDetailTab = DetailTab.Json;

    // Pagination state
    private string? _currentCursor;
    private long? _totalCount;
    private int _loadedCount;
    private int _pageSize = 50;
    private bool _isLoadingMore;

    // Filter state
    private string _recordQuery = "*";

    // Tree options
    private bool _showNullsInTree;

    private enum DetailTab { Json, DataGrid, Tree }

    public RecordBrowserControl()
    {
        InitializeComponent();
    }

    public void Initialize(IOsduClient osduClient, AppTheme theme)
    {
        if (_osduClient is not null) return; // already initialized

        // Use reflection-free field init via unsafe cast — or just store it
        var field = typeof(RecordBrowserControl).GetField("_osduClient",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(this, osduClient);

        _currentTheme = theme;
        ApplyTheme();
        ApplyDetailTabStyles();
        ApplyPaginationStyles();
        _ = LoadKindsAsync();
    }

    public void UpdateTheme(AppTheme theme)
    {
        _currentTheme = theme;
        ApplyTheme();
        ApplyDetailTabStyles();
        ApplyPaginationStyles();
    }

    // ─── Load Kinds from Schema Service ──────────────────────────────

    private async Task LoadKindsAsync()
    {
        StatusText.Text = "⏳ Loading schemas...";
        KindListBox.Items.Clear();

        try
        {
            var offset = 0;
            const int limit = 100;
            var allSchemas = new List<SchemaKindItem>();

            while (true)
            {
                var response = await _osduClient.Schema.GetSchemaAsync(
                    latestVersion: false,
                    limit: limit,
                    offset: offset);

                if (response?.SchemaInfos is null || response.SchemaInfos.Count == 0)
                    break;

                foreach (var schema in response.SchemaInfos)
                {
                    var id = schema.SchemaIdentity;
                    var kindId = id.Id ?? $"{id.Authority}:{id.Source}:{id.EntityType}:{id.SchemaVersionMajor}.{id.SchemaVersionMinor}.{id.SchemaVersionPatch}";
                    var category = id.EntityType.Contains("--")
                        ? id.EntityType[..id.EntityType.IndexOf("--")]
                        : "other";
                    var version = $"{id.SchemaVersionMajor}.{id.SchemaVersionMinor}.{id.SchemaVersionPatch}";

                    allSchemas.Add(new SchemaKindItem(kindId, id.EntityType, category, version));
                }

                offset += response.SchemaInfos.Count;
                if (response.SchemaInfos.Count < limit) break;
            }

            _allKinds.Clear();
            _allKinds.AddRange(allSchemas.OrderBy(k => k.Category).ThenBy(k => k.EntityType).ThenBy(k => k.Version));
            FilterKinds(string.Empty);
            StatusText.Text = $"✅ Loaded {_allKinds.Count} kinds";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Failed to load schemas: {ex.Message}";
        }
    }

    private void FilterKinds(string filter)
    {
        KindListBox.Items.Clear();
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allKinds
            : _allKinds.Where(k =>
                k.KindId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                k.EntityType.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        string? lastCategory = null;
        foreach (var kind in filtered)
        {
            if (kind.Category != lastCategory)
            {
                lastCategory = kind.Category;
                KindListBox.Items.Add(new ListBoxItem
                {
                    Content = new TextBlock
                    {
                        Text = $"── {kind.Category} ──",
                        FontWeight = FontWeights.Bold,
                        Foreground = _currentTheme.AccentBrush,
                        FontSize = 11,
                        Margin = new Thickness(0, 8, 0, 2)
                    },
                    IsEnabled = false,
                    Background = Brushes.Transparent
                });
            }

            var displayPanel = new StackPanel { Orientation = Orientation.Horizontal };
            displayPanel.Children.Add(new TextBlock
            {
                Text = kind.EntityType,
                Foreground = _currentTheme.TextSecondaryBrush,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            displayPanel.Children.Add(new TextBlock
            {
                Text = $":{kind.Version}",
                Foreground = _currentTheme.TextMutedBrush,
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            });

            KindListBox.Items.Add(new ListBoxItem
            {
                Content = displayPanel,
                Tag = kind.KindId,
                ToolTip = kind.KindId,
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand
            });
        }
    }

    // ─── Search Records ──────────────────────────────────────────────

    private void KindFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterKinds(KindFilterBox.Text);
    }

    private void KindListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KindListBox.SelectedItem is ListBoxItem { Tag: string kindId })
        {
            _selectedKind = kindId;
            _recordQuery = "*";
            RecordQueryBox.Text = "";
            _ = SearchRecordsAsync(kindId, resetCursor: true);
        }
    }

    private void PageSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageSizeCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var size))
        {
            _pageSize = size;

            if (_selectedKind is not null)
                _ = SearchRecordsAsync(_selectedKind, resetCursor: true);
        }
    }

    private void RecordQueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyRecordFilter();
            e.Handled = true;
        }
    }

    private void RecordFilterApply_Click(object sender, RoutedEventArgs e)
    {
        ApplyRecordFilter();
    }

    private void RecordFilterClear_Click(object sender, RoutedEventArgs e)
    {
        RecordQueryBox.Text = "";
        _recordQuery = "*";
        if (_selectedKind is not null)
            _ = SearchRecordsAsync(_selectedKind, resetCursor: true);
    }

    private void ApplyRecordFilter()
    {
        var query = RecordQueryBox.Text.Trim();
        _recordQuery = string.IsNullOrEmpty(query) ? "*" : query;

        if (_selectedKind is not null)
            _ = SearchRecordsAsync(_selectedKind, resetCursor: true);
    }

    private async Task SearchRecordsAsync(string kind, bool resetCursor)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        if (resetCursor)
        {
            RecordsListBox.Items.Clear();
            ClearDetail();
            _currentCursor = null;
            _totalCount = null;
            _loadedCount = 0;
        }

        StatusText.Text = resetCursor
            ? $"⏳ Searching {kind}..."
            : $"⏳ Loading more records...";

        try
        {
            var response = await _osduClient.Search.PostQueryWithCursorAsync(new CursorQueryRequest
            {
                Kind = kind,
                Query = _recordQuery,
                Limit = _pageSize,
                Cursor = _currentCursor,
                TrackTotalCount = true
            }, cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            _currentCursor = response?.Cursor;
            if (resetCursor) _totalCount = response?.TotalCount;

            if (response?.Results is null || response.Results.Count == 0)
            {
                if (_loadedCount == 0)
                    StatusText.Text = $"No records found for {kind}";
                else
                    StatusText.Text = $"✅ {_loadedCount} record(s) loaded — no more results";

                _currentCursor = null;
                UpdatePaginationBar();
                return;
            }

            _loadedCount += response.Results.Count;

            foreach (var result in response.Results)
            {
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
                var doc = JsonDocument.Parse(json);
                var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "unknown" : "unknown";

                RecordsListBox.Items.Add(new ListBoxItem
                {
                    Content = new TextBlock
                    {
                        Text = id,
                        Foreground = _currentTheme.TextSecondaryBrush,
                        FontSize = 11.5,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    Tag = doc.RootElement.Clone(),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(8, 5, 8, 5),
                    Cursor = Cursors.Hand,
                    ToolTip = id
                });
            }

            var totalLabel = _totalCount.HasValue ? $" of {_totalCount}" : "";
            StatusText.Text = $"✅ {_loadedCount}{totalLabel} record(s) loaded";
            UpdatePaginationBar();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                StatusText.Text = $"❌ Search failed: {ex.Message}";
        }
    }

    private void LoadMore_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKind is not null && _currentCursor is not null && !_isLoadingMore)
            _ = SearchRecordsAsync(_selectedKind, resetCursor: false);
    }

    private async void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKind is null || _currentCursor is null || _isLoadingMore) return;

        _isLoadingMore = true;
        ShowAllButton.IsEnabled = false;
        LoadMoreButton.IsEnabled = false;
        ShowAllButton.Content = "⏳ ...";

        try
        {
            while (_currentCursor is not null && _selectedKind is not null)
            {
                await SearchRecordsAsync(_selectedKind, resetCursor: false);
            }
        }
        finally
        {
            _isLoadingMore = false;
            ShowAllButton.Content = "⤓ All";
            UpdatePaginationBar();
        }
    }

    private void UpdatePaginationBar()
    {
        var hasMore = _currentCursor is not null;
        PaginationButtonsPanel.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
        LoadMoreButton.IsEnabled = hasMore && !_isLoadingMore;
        ShowAllButton.IsEnabled = hasMore && !_isLoadingMore;

        if (hasMore && _totalCount.HasValue)
        {
            var remaining = _totalCount.Value - _loadedCount;
            PaginationStatusText.Text = $"{_loadedCount} of {_totalCount} loaded — {remaining} remaining";
            PaginationStatusText.Visibility = Visibility.Visible;
        }
        else if (hasMore)
        {
            PaginationStatusText.Text = $"{_loadedCount} loaded — more available";
            PaginationStatusText.Visibility = Visibility.Visible;
        }
        else
        {
            PaginationStatusText.Visibility = Visibility.Collapsed;
        }
    }

    private void RecordsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordsListBox.SelectedItem is ListBoxItem { Tag: JsonElement element })
        {
            _selectedRecord = element;
            RenderActiveTab();
        }

        UpdateDeleteButtonState();
    }

    private void ClearDetail()
    {
        _selectedRecord = null;
        RecordJsonBox.Clear();
        RecordDataGrid.ItemsSource = null;
        RecordTreeView.Items.Clear();
        UpdateDeleteButtonState();
    }

    // ─── Delete Records ──────────────────────────────────────────────

    private void UpdateDeleteButtonState()
    {
        var hasSelection = RecordsListBox.SelectedItems.Count > 0;
        DeleteSelectedButton.IsEnabled = hasSelection;
        DeleteSelectedButton.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        if (hasSelection)
            DeleteSelectedButton.Content = $"🗑 Delete ({RecordsListBox.SelectedItems.Count})";
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = RecordsListBox.SelectedItems.Cast<ListBoxItem>().ToList();
        if (selectedItems.Count == 0) return;

        var recordIds = new List<string>();
        foreach (var item in selectedItems)
        {
            if (item.Tag is JsonElement element &&
                element.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                if (!string.IsNullOrEmpty(id))
                    recordIds.Add(id);
            }
        }

        if (recordIds.Count == 0) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete {recordIds.Count} record(s)?\n\n" +
            $"This performs a soft delete via the OSDU Storage API.\n\n" +
            string.Join("\n", recordIds.Take(10)) +
            (recordIds.Count > 10 ? $"\n... and {recordIds.Count - 10} more" : ""),
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        DeleteSelectedButton.IsEnabled = false;
        DeleteSelectedButton.Content = "⏳ Deleting...";
        StatusText.Text = $"⏳ Deleting {recordIds.Count} record(s)...";

        try
        {
            var response = await _osduClient.Storage.PostRecordsDeleteAsync(recordIds);

            foreach (var item in selectedItems)
                RecordsListBox.Items.Remove(item);

            _loadedCount -= recordIds.Count;
            if (_totalCount.HasValue) _totalCount -= recordIds.Count;

            var failedCount = response?.NotDeletedRecords?.Count ?? 0;
            if (failedCount > 0)
            {
                StatusText.Text = $"⚠️ Deleted {recordIds.Count - failedCount}, failed {failedCount}";
                RecordJsonBox.Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var totalLabel = _totalCount.HasValue ? $" of {_totalCount}" : "";
                StatusText.Text = $"✅ Deleted {recordIds.Count} record(s) — {_loadedCount}{totalLabel} remaining";
            }

            ClearDetail();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Delete failed: {ex.Message}";
            RecordJsonBox.Text = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        }
        finally
        {
            UpdateDeleteButtonState();
        }
    }

    // ─── Detail Tabs ─────────────────────────────────────────────────

    private void TabJson_Click(object sender, RoutedEventArgs e)
    {
        _activeDetailTab = DetailTab.Json;
        ApplyDetailTabStyles();
        RenderActiveTab();
    }

    private void TabDataGrid_Click(object sender, RoutedEventArgs e)
    {
        _activeDetailTab = DetailTab.DataGrid;
        ApplyDetailTabStyles();
        RenderActiveTab();
    }

    private void TabTree_Click(object sender, RoutedEventArgs e)
    {
        _activeDetailTab = DetailTab.Tree;
        ApplyDetailTabStyles();
        RenderActiveTab();
    }

    private void ShowNullsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        _showNullsInTree = ShowNullsCheckBox.IsChecked == true;
        if (_activeDetailTab == DetailTab.Tree)
            RenderActiveTab();
    }

    private void ApplyDetailTabStyles()
    {
        RecordJsonBox.Visibility = _activeDetailTab == DetailTab.Json ? Visibility.Visible : Visibility.Collapsed;
        RecordDataGrid.Visibility = _activeDetailTab == DetailTab.DataGrid ? Visibility.Visible : Visibility.Collapsed;
        RecordTreeView.Visibility = _activeDetailTab == DetailTab.Tree ? Visibility.Visible : Visibility.Collapsed;
        TreeOptionsPanel.Visibility = _activeDetailTab == DetailTab.Tree ? Visibility.Visible : Visibility.Collapsed;

        TabJsonButton.Background = _activeDetailTab == DetailTab.Json ? _currentTheme.AccentBrush : _currentTheme.TagBrush;
        TabJsonButton.Foreground = _activeDetailTab == DetailTab.Json ? Brushes.White : _currentTheme.TextSecondaryBrush;
        TabDataGridButton.Background = _activeDetailTab == DetailTab.DataGrid ? _currentTheme.AccentBrush : _currentTheme.TagBrush;
        TabDataGridButton.Foreground = _activeDetailTab == DetailTab.DataGrid ? Brushes.White : _currentTheme.TextSecondaryBrush;
        TabTreeButton.Background = _activeDetailTab == DetailTab.Tree ? _currentTheme.AccentBrush : _currentTheme.TagBrush;
        TabTreeButton.Foreground = _activeDetailTab == DetailTab.Tree ? Brushes.White : _currentTheme.TextSecondaryBrush;
    }

    private void RenderActiveTab()
    {
        if (_selectedRecord is not { } element) return;

        switch (_activeDetailTab)
        {
            case DetailTab.Json:
                RenderJsonTab(element);
                break;
            case DetailTab.DataGrid:
                RenderDataGridTab(element);
                break;
            case DetailTab.Tree:
                RenderTreeTab(element);
                break;
        }
    }

    private void RenderJsonTab(JsonElement element)
    {
        var pretty = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
        RecordJsonBox.Text = pretty;
    }

    private void RenderDataGridTab(JsonElement element)
    {
        RecordDataGrid.ItemsSource = null;
        RecordDataGrid.Columns.Clear();

        if (element.ValueKind != JsonValueKind.Object)
        {
            RecordDataGrid.ItemsSource = null;
            return;
        }

        var table = new DataTable();
        table.Columns.Add("Property", typeof(string));
        table.Columns.Add("Value", typeof(string));
        table.Columns.Add("Type", typeof(string));

        foreach (var prop in element.EnumerateObject())
        {
            var value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? "",
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Array => $"[Array: {prop.Value.GetArrayLength()} items]",
                JsonValueKind.Object => "{Object}",
                _ => prop.Value.GetRawText()
            };

            var type = prop.Value.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                JsonValueKind.Array => "array",
                JsonValueKind.Object => "object",
                _ => "unknown"
            };

            table.Rows.Add(prop.Name, value, type);
        }

        RecordDataGrid.AutoGenerateColumns = true;
        RecordDataGrid.ItemsSource = table.DefaultView;
    }

    private void RenderTreeTab(JsonElement element)
    {
        RecordTreeView.Items.Clear();
        var rootItem = BuildTreeNode("record", element);
        rootItem.IsExpanded = true;
        RecordTreeView.Items.Add(rootItem);
    }

    private bool IsNullOrAllNulls(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.Object => element.EnumerateObject().All(p => IsNullOrAllNulls(p.Value)),
            JsonValueKind.Array => element.GetArrayLength() == 0 || element.EnumerateArray().All(IsNullOrAllNulls),
            _ => false
        };
    }

    private TreeViewItem BuildTreeNode(string key, JsonElement element)
    {
        var item = new TreeViewItem { IsExpanded = false };

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                item.Header = BuildTreeHeader(key, "{…}", _currentTheme.TextMutedBrush);
                foreach (var prop in element.EnumerateObject())
                {
                    if (!_showNullsInTree && IsNullOrAllNulls(prop.Value))
                        continue;
                    item.Items.Add(BuildTreeNode(prop.Name, prop.Value));
                }
                break;

            case JsonValueKind.Array:
                item.Header = BuildTreeHeader(key, $"[{element.GetArrayLength()}]", _currentTheme.TextMutedBrush);
                var index = 0;
                foreach (var child in element.EnumerateArray())
                {
                    if (!_showNullsInTree && IsNullOrAllNulls(child))
                    {
                        index++;
                        continue;
                    }
                    item.Items.Add(BuildTreeNode($"[{index}]", child));
                    index++;
                }
                break;

            case JsonValueKind.String:
                item.Header = BuildTreeHeader(key, $"\"{element.GetString()}\"", new SolidColorBrush(Color.FromRgb(106, 171, 115)));
                break;

            case JsonValueKind.Number:
                item.Header = BuildTreeHeader(key, element.GetRawText(), new SolidColorBrush(Color.FromRgb(180, 142, 94)));
                break;

            case JsonValueKind.True or JsonValueKind.False:
                item.Header = BuildTreeHeader(key, element.GetRawText(), new SolidColorBrush(Color.FromRgb(86, 156, 214)));
                break;

            case JsonValueKind.Null:
                item.Header = BuildTreeHeader(key, "null", _currentTheme.TextMutedBrush);
                break;

            default:
                item.Header = BuildTreeHeader(key, element.GetRawText(), _currentTheme.TextSecondaryBrush);
                break;
        }

        return item;
    }

    private StackPanel BuildTreeHeader(string key, string value, Brush valueBrush)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = key,
            Foreground = _currentTheme.AccentBrush,
            FontWeight = FontWeights.Medium,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 4, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = ": ",
            Foreground = _currentTheme.TextMutedBrush,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12
        });
        panel.Children.Add(new TextBlock
        {
            Text = value.Length > 200 ? value[..200] + "…" : value,
            Foreground = valueBrush,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 600
        });
        return panel;
    }

    // ─── Theme ───────────────────────────────────────────────────────

    private void ApplyTheme()
    {
        Background = _currentTheme.SurfaceBrush;
        SidebarBorder.Background = _currentTheme.SidebarBrush;
        SidebarBorder.BorderBrush = _currentTheme.BorderBrush;
        SidebarTitle.Foreground = _currentTheme.TextPrimaryBrush;
        KindFilterBox.Background = _currentTheme.InputFieldBrush;
        KindFilterBox.Foreground = _currentTheme.TextPrimaryBrush;
        KindFilterBox.BorderBrush = _currentTheme.BorderBrush;
        KindListBox.Background = Brushes.Transparent;
        RecordsPanel.Background = _currentTheme.CardBrush;
        RecordsListBox.Background = Brushes.Transparent;
        RecordJsonBox.Background = _currentTheme.ResponseBgBrush;
        RecordJsonBox.Foreground = _currentTheme.IsDark
            ? new SolidColorBrush(Color.FromRgb(200, 200, 212))
            : _currentTheme.TextPrimaryBrush;
        RecordJsonBox.CaretBrush = _currentTheme.TextPrimaryBrush;
        RecordJsonBox.BorderBrush = _currentTheme.BorderBrush;
        RecordDataGrid.Background = _currentTheme.ResponseBgBrush;
        RecordDataGrid.Foreground = _currentTheme.TextPrimaryBrush;
        RecordDataGrid.BorderBrush = _currentTheme.BorderBrush;
        RecordDataGrid.RowBackground = _currentTheme.ResponseBgBrush;
        RecordDataGrid.AlternatingRowBackground = _currentTheme.CardBrush;
        RecordTreeView.Background = _currentTheme.ResponseBgBrush;
        RecordTreeView.Foreground = _currentTheme.TextPrimaryBrush;
        RecordTreeView.BorderBrush = _currentTheme.BorderBrush;
        StatusText.Foreground = _currentTheme.TextSecondaryBrush;
        RecordsPanelTitle.Foreground = _currentTheme.TextPrimaryBrush;
        DetailTitle.Foreground = _currentTheme.TextPrimaryBrush;
        PageSizeLabel.Foreground = _currentTheme.TextSecondaryBrush;
        PageSizeCombo.Background = _currentTheme.InputFieldBrush;
        PageSizeCombo.Foreground = _currentTheme.TextPrimaryBrush;
        PageSizeCombo.BorderBrush = _currentTheme.BorderBrush;
        PaginationStatusText.Foreground = _currentTheme.TextMutedBrush;
        DeleteSelectedButton.Background = new SolidColorBrush(Color.FromRgb(200, 50, 50));
        DeleteSelectedButton.Foreground = Brushes.White;
        RecordQueryBox.Background = _currentTheme.InputFieldBrush;
        RecordQueryBox.Foreground = _currentTheme.TextPrimaryBrush;
        RecordQueryBox.BorderBrush = _currentTheme.BorderBrush;
        RecordQueryBox.CaretBrush = _currentTheme.TextPrimaryBrush;
        RecordFilterApplyButton.Background = _currentTheme.AccentBrush;
        RecordFilterApplyButton.Foreground = Brushes.White;
        RecordFilterClearButton.Background = _currentTheme.TagBrush;
        RecordFilterClearButton.Foreground = _currentTheme.TextSecondaryBrush;
        RecordQueryLabel.Foreground = _currentTheme.TextMutedBrush;
        ShowNullsLabel.Foreground = _currentTheme.TextSecondaryBrush;
        DetailBorder.Background = _currentTheme.SurfaceBrush;
    }

    private void ApplyPaginationStyles()
    {
        LoadMoreButton.Background = _currentTheme.AccentBrush;
        LoadMoreButton.Foreground = Brushes.White;
        ShowAllButton.Background = _currentTheme.TagBrush;
        ShowAllButton.Foreground = _currentTheme.TextSecondaryBrush;
    }

    private record SchemaKindItem(string KindId, string EntityType, string Category, string Version);
}
