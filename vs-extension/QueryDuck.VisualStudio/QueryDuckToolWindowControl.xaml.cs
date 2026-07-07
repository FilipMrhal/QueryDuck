using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QueryDuck.Client;

namespace QueryDuck.VisualStudio;

public partial class QueryDuckToolWindowControl : UserControl
{
    private readonly List<QueryCaptureEventDto> _events = new();
    private QueryDuckEventClient _client = new();
    private readonly DispatcherTimer _refreshTimer;
    private string? _selectedEventId;
    private string? _newestEventId;

    public QueryDuckToolWindowControl()
    {
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) => Refresh(silent: true);
        AutoRefreshCheckBox.Checked += (_, _) => _refreshTimer.Start();
        AutoRefreshCheckBox.Unchecked += (_, _) => _refreshTimer.Stop();
        _refreshTimer.Start();
        Loaded += (_, _) => Refresh(silent: false);
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e) => Reconnect();

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh(silent: false);

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ClearEventsAsync();
    }

    private void FilterChanged(object sender, RoutedEventArgs e) => ApplyFilters(preserveSelection: true);

    private void EventsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EventsGrid.SelectedItem is EventRowViewModel row)
        {
            _selectedEventId = row.EventId;
            ShowSelectedEvent(row.Source);
        }
    }

    private void RecommendationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowSelectedRecommendation();

    private void Reconnect()
    {
        var url = ServerUrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            url = "http://127.0.0.1:17654";
            ServerUrlTextBox.Text = url;
        }

        _client.Dispose();
        _client = new QueryDuckEventClient(url);
        Refresh(silent: false);
    }

    private async void Refresh(bool silent)
    {
        try
        {
            var health = await _client.FetchHealthAsync().ConfigureAwait(true);
            var fetched = await _client.FetchEventsAsync().ConfigureAwait(true);

            var previousNewest = _newestEventId;
            _events.Clear();
            _events.AddRange(fetched.AsEnumerable().Reverse());

            _newestEventId = _events.FirstOrDefault()?.EventId;
            var hasNewEvents = _newestEventId is not null && _newestEventId != previousNewest;

            StatusLabel.Text = $"Connected · {health.Count} event(s) · {_client.BaseUrl}";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xBF, 0x82));
            SessionWarningsLabel.Text = health.SessionWarnings.Count == 0
                ? string.Empty
                : $"Session: {string.Join(" | ", health.SessionWarnings)}";

            ApplyFilters(
                preserveSelection: true,
                preferNewest: hasNewEvents && FollowLatestCheckBox.IsChecked == true);

            if (!silent && EventsGrid.Items.Count > 0 && EventsGrid.SelectedItem is null)
            {
                SelectEventById(_newestEventId);
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Disconnected · {ex.Message}";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x55, 0x55));
            SessionWarningsLabel.Text = string.Empty;
            if (!silent)
            {
                MetaLabel.Text = " Start your app with UseQueryDuckDebugging() or UseQueryDuckCapture(o => o.StartLocalEventServer = true)";
            }
        }
    }

    private async Task ClearEventsAsync()
    {
        try
        {
            await _client.ClearEventsAsync().ConfigureAwait(true);
            _events.Clear();
            _newestEventId = null;
            _selectedEventId = null;
            EventsGrid.ItemsSource = null;
            ClearDetails();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Clear failed: {ex.Message}";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x55, 0x55));
        }
    }

    private void ApplyFilters(bool preserveSelection, bool preferNewest = false)
    {
        var provider = (ProviderFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All providers";
        var tag = TagFilterTextBox.Text.Trim().ToLowerInvariant();

        var filtered = _events.Where(evt =>
        {
            var providerMatch = provider == "All providers" ||
                string.Equals(evt.Provider, provider, StringComparison.OrdinalIgnoreCase);
            var tagMatch = tag.Length == 0 || evt.Tag?.ToLowerInvariant().Contains(tag) == true;
            return providerMatch && tagMatch;
        }).Select(EventRowViewModel.FromEvent).ToList();

        EventsGrid.ItemsSource = filtered;

        string? targetId = preferNewest
            ? filtered.FirstOrDefault()?.EventId
            : preserveSelection && _selectedEventId is not null && filtered.Any(r => r.EventId == _selectedEventId)
                ? _selectedEventId
                : filtered.FirstOrDefault()?.EventId;

        SelectEventById(targetId);
    }

    private void SelectEventById(string? eventId)
    {
        if (eventId is null)
        {
            ClearDetails();
            return;
        }

        foreach (var item in EventsGrid.Items)
        {
            if (item is EventRowViewModel row && row.EventId == eventId)
            {
                EventsGrid.SelectedItem = item;
                return;
            }
        }
    }

    private void ShowSelectedEvent(QueryCaptureEventDto evt)
    {
        MetaLabel.Text =
            $" {evt.Provider} · {evt.MetaSourceLabel()} · {evt.FormattedDuration()} · {evt.EventId[..Math.Min(8, evt.EventId.Length)]} · schema v{evt.SchemaVersion}";

        SqlTextBox.Text = FormatSql(evt.Sql);
        CSharpTextBox.Text = evt.ExpressionCSharp ??
            "-- No C# expression captured.\n-- Enable UseQueryDuckDebugging() or call .WithQueryDuckScope(context) before executing.";
        PlanTextBox.Text = evt.ExecutionPlan ??
            "-- No execution plan captured.\n-- Slow queries auto-capture plans when CapturePlansForSlowQueries is enabled.";

        DiagnosticsListBox.ItemsSource = evt.Diagnostics.Count == 0
            ? new[] { new QueryDiagnosticDto { RuleId = "INFO", Severity = "Info", Message = "No diagnostics for this query." } }
            : evt.Diagnostics;

        ParametersGrid.ItemsSource = evt.Parameters
            .Select(p => new ParameterRow { Parameter = p.Key, Value = p.Value?.ToString() ?? "NULL" })
            .ToList();

        ExpressionTreeView.Items.Clear();
        if (evt.ExpressionTree is not null)
        {
            ExpressionTreeView.Items.Add(BuildTreeItem(evt.ExpressionTree));
        }
        else if (!string.IsNullOrWhiteSpace(evt.ExpressionTreeText))
        {
            ExpressionTreeView.Items.Add(new TreeViewItem { Header = evt.ExpressionTreeText, IsExpanded = true });
        }

        ShowImprovementAnalysis(evt);
    }

    private void ShowImprovementAnalysis(QueryCaptureEventDto evt)
    {
        var analysis = evt.ImprovementAnalysis;
        if (analysis is null)
        {
            RecommendationsListBox.ItemsSource = null;
            SuggestedSqlTextBox.Text =
                "-- No slow-query analysis for this event.\n-- Analysis runs when duration exceeds SlowQueryThresholdMs.";
            PlanDiffTextBox.Text = "-- Plan comparison appears here when a rewrite is recommended.";
            PgStatTextBox.Text = "-- Enable EnablePgStatStatementsInsights for PostgreSQL historical stats.";
            PlanGraphControl.SetPlanDiff(null);
            return;
        }

        RecommendationsListBox.ItemsSource = analysis.Recommendations;
        PgStatTextBox.Text = FormatPgStatInsight(analysis.PgStatStatements);

        if (analysis.Recommendations.Count > 0)
        {
            RecommendationsListBox.SelectedIndex = 0;
        }
        else
        {
            SuggestedSqlTextBox.Text = "-- No recommendations generated.";
            PlanDiffTextBox.Text = analysis.PrimaryPlanDiff?.TextDiff ?? "-- No plan diff available.";
            PlanGraphControl.SetPlanDiff(analysis.PrimaryPlanDiff);
        }
    }

    private void ShowSelectedRecommendation()
    {
        if (RecommendationsListBox.SelectedItem is not SlowQueryRecommendationDto recommendation)
        {
            return;
        }

        var sqlText = string.Empty;
        if (!string.IsNullOrWhiteSpace(recommendation.SuggestedSql))
        {
            sqlText += "-- Suggested rewrite" + Environment.NewLine + recommendation.SuggestedSql + Environment.NewLine + Environment.NewLine;
        }

        if (!string.IsNullOrWhiteSpace(recommendation.SuggestedIndexSql))
        {
            sqlText += "-- Suggested index / schema change" + Environment.NewLine + recommendation.SuggestedIndexSql;
        }

        SuggestedSqlTextBox.Text = sqlText.Length > 0
            ? sqlText
            : $"-- {recommendation.Title}{Environment.NewLine}-- {recommendation.Description}";

        PlanDiffTextBox.Text = recommendation.PlanDiff?.TextDiff ??
            recommendation.ImprovedPlanText ??
            "-- Run the suggested SQL with EXPLAIN on your database to compare plans.";

        var selectedEvent = FindSelectedEvent();
        PlanGraphControl.SetPlanDiff(recommendation.PlanDiff ?? selectedEvent?.ImprovementAnalysis?.PrimaryPlanDiff);
    }

    private QueryCaptureEventDto? FindSelectedEvent() =>
        _selectedEventId is null ? null : _events.FirstOrDefault(e => e.EventId == _selectedEventId);

    private static string FormatPgStatInsight(PgStatStatementInsightDto? insight)
    {
        if (insight is null)
        {
            return "-- pg_stat_statements not included.\n-- Set EnablePgStatStatementsInsights = true and register AddPostgreSql() adapters.";
        }

        var text = "-- Matched pg_stat_statements entry" + Environment.NewLine +
                   $"calls: {insight.Calls}" + Environment.NewLine +
                   $"mean_exec_time_ms: {insight.MeanExecTimeMs.ToString("F1", CultureInfo.InvariantCulture)}" + Environment.NewLine +
                   $"total_exec_time_ms: {insight.TotalExecTimeMs.ToString("F0", CultureInfo.InvariantCulture)}" + Environment.NewLine +
                   $"rows: {insight.Rows}" + Environment.NewLine +
                   $"shared_blocks_hit_ratio: {(insight.SharedBlocksHitRatio * 100).ToString("F0", CultureInfo.InvariantCulture)}%";

        if (!string.IsNullOrWhiteSpace(insight.MatchedQueryText))
        {
            text += Environment.NewLine + Environment.NewLine + "-- matched query" + Environment.NewLine + insight.MatchedQueryText;
        }

        return text;
    }

    private static TreeViewItem BuildTreeItem(ExpressionTreeNodeDto node)
    {
        var header = string.IsNullOrWhiteSpace(node.Name)
            ? $"{node.Kind} ({node.Type})"
            : $"{node.Kind} {node.Name} ({node.Type})";
        if (!string.IsNullOrWhiteSpace(node.Value))
        {
            header += $" = {node.Value}";
        }

        var item = new TreeViewItem { Header = header, IsExpanded = true };
        if (node.Children is null)
        {
            return item;
        }

        foreach (var child in node.Children)
        {
            item.Items.Add(BuildTreeItem(child));
        }

        return item;
    }

    private void ClearDetails()
    {
        MetaLabel.Text = string.Empty;
        SqlTextBox.Text = string.Empty;
        CSharpTextBox.Text = string.Empty;
        PlanTextBox.Text = string.Empty;
        SuggestedSqlTextBox.Text = string.Empty;
        PlanDiffTextBox.Text = string.Empty;
        PgStatTextBox.Text = string.Empty;
        DiagnosticsListBox.ItemsSource = null;
        ParametersGrid.ItemsSource = null;
        ExpressionTreeView.Items.Clear();
        RecommendationsListBox.ItemsSource = null;
        PlanGraphControl.SetPlanDiff(null);
    }

    private static string FormatSql(string sql) =>
        sql.Replace(" SELECT ", "\nSELECT ", StringComparison.Ordinal)
            .Replace(" FROM ", "\nFROM ", StringComparison.Ordinal)
            .Replace(" WHERE ", "\nWHERE ", StringComparison.Ordinal)
            .Replace(" INNER JOIN ", "\nINNER JOIN ", StringComparison.Ordinal)
            .Replace(" LEFT JOIN ", "\nLEFT JOIN ", StringComparison.Ordinal)
            .Replace(" ORDER BY ", "\nORDER BY ", StringComparison.Ordinal)
            .Replace(" GROUP BY ", "\nGROUP BY ", StringComparison.Ordinal)
            .Trim();

    private sealed class ParameterRow
    {
        public string Parameter { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}

internal sealed class EventRowViewModel
{
    public required string EventId { get; init; }

    public required QueryCaptureEventDto Source { get; init; }

    public string FormattedTime => Source.FormattedTime();

    public string Provider => Source.Provider;

    public string TagDisplay => Source.Tag ?? "—";

    public int WarningCount => Source.WarningCount;

    public string FormattedDuration => Source.FormattedDuration();

    public string SqlPreview => Source.SqlPreview();

    public static EventRowViewModel FromEvent(QueryCaptureEventDto evt) =>
        new() { EventId = evt.EventId, Source = evt };
}
