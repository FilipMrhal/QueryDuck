using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QueryDuck.Client;

namespace QueryDuck.VisualStudio;

public partial class PlanFlowGraphControl : UserControl
{
    private PlanDiffVisualizationDto? _planDiff;

    public PlanFlowGraphControl()
    {
        Content = BuildContent();
    }

    public void SetPlanDiff(PlanDiffVisualizationDto? planDiff)
    {
        _planDiff = planDiff;
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        if (_planDiff is null)
        {
            return new TextBlock
            {
                Text = "Enable EmitMermaidPlanGraphs in QueryDuck options to include plan graphs.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA2, 0xC3)),
                Margin = new Thickness(8),
            };
        }

        var hasSteps = _planDiff.OriginalSteps.Count > 0 || _planDiff.ImprovedSteps.Count > 0;
        var hasMermaid = !string.IsNullOrWhiteSpace(_planDiff.OriginalMermaid) ||
            !string.IsNullOrWhiteSpace(_planDiff.ImprovedMermaid);
        if (!hasSteps && !hasMermaid)
        {
            return new TextBlock
            {
                Text = "No plan graph data in this event.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA2, 0xC3)),
                Margin = new Thickness(8),
            };
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (!string.IsNullOrWhiteSpace(_planDiff.SideBySideMermaid))
        {
            root.Children.Add(new TextBlock
            {
                Text = " Combined Mermaid (paste into mermaid.live to preview) ",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA2, 0xC3)),
                Margin = new Thickness(8, 4, 8, 4),
            });
        }

        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var originalColumn = BuildColumn("Original plan", _planDiff.OriginalSteps, _planDiff.OriginalMermaid);
        Grid.SetColumn(originalColumn, 0);
        split.Children.Add(originalColumn);
        var improvedColumn = BuildColumn("Improved plan", _planDiff.ImprovedSteps, _planDiff.ImprovedMermaid);
        Grid.SetColumn(improvedColumn, 1);
        split.Children.Add(improvedColumn);
        Grid.SetRow(split, 1);
        root.Children.Add(split);

        if (!string.IsNullOrWhiteSpace(_planDiff.SideBySideMermaid))
        {
            var combined = new GroupBox
            {
                Header = "Combined Mermaid",
                Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE4, 0xF0)),
                Margin = new Thickness(0, 8, 0, 0),
                Content = new TextBox
                {
                    Text = _planDiff.SideBySideMermaid,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinHeight = 80,
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE4, 0xF0)),
                    BorderThickness = new Thickness(0),
                    FontFamily = new FontFamily("Consolas"),
                },
            };
            Grid.SetRow(combined, 2);
            root.Children.Add(combined);
        }

        return root;
    }

    private static UIElement BuildColumn(string title, IReadOnlyList<PlanStepSummaryDto> steps, string? mermaid)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE4, 0xF0)),
            Margin = new Thickness(0, 0, 0, 4),
        });
        panel.Children.Add(new PlanStepGraphControl { Steps = steps, MinHeight = 180 });

        if (!string.IsNullOrWhiteSpace(mermaid))
        {
            panel.Children.Add(new GroupBox
            {
                Header = "Mermaid",
                Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE4, 0xF0)),
                Margin = new Thickness(0, 8, 0, 0),
                Content = new TextBox
                {
                    Text = mermaid,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinHeight = 80,
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE4, 0xF0)),
                    BorderThickness = new Thickness(0),
                    FontFamily = new FontFamily("Consolas"),
                },
            });
        }

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            Child = panel,
        };
        return border;
    }
}

internal sealed class PlanStepGraphControl : Canvas
{
    public static readonly DependencyProperty StepsProperty =
        DependencyProperty.Register(nameof(Steps), typeof(IReadOnlyList<PlanStepSummaryDto>), typeof(PlanStepGraphControl),
            new PropertyMetadata(Array.Empty<PlanStepSummaryDto>(), (_, e) =>
            {
                if (e.NewValue is not null && _ is PlanStepGraphControl control)
                {
                    control.InvalidateVisual();
                }
            }));

    public IReadOnlyList<PlanStepSummaryDto> Steps
    {
        get => (IReadOnlyList<PlanStepSummaryDto>)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)), null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (Steps.Count == 0)
        {
            dc.DrawText(
                MakeText("No steps", 12, FontWeights.Normal, null, VisualTreeHelper.GetDpi(this)),
                new Point(12, 12));
            return;
        }

        var boxWidth = Math.Max(120, ActualWidth - 40);
        var y = 16.0;
        var steps = Steps.Take(6).ToList();
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var boxHeight = 52.0;
            var fill = i == 0
                ? new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x6B))
                : new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
            dc.DrawRoundedRectangle(fill, null, new Rect(12, y, boxWidth, boxHeight), 10, 10);

            dc.DrawText(
                MakeText(step.Operation, 12, FontWeights.Bold, null, VisualTreeHelper.GetDpi(this)),
                new Point(20, y + 4));
            var detail = $"{step.ObjectName ?? string.Empty} · cost {(step.Cost?.ToString("F0", CultureInfo.InvariantCulture) ?? "?")}".Trim(' ', '·');
            dc.DrawText(
                MakeText(detail, 11, FontWeights.Normal, Color.FromRgb(0xA8, 0xB4, 0xCC), VisualTreeHelper.GetDpi(this)),
                new Point(20, y + 22));

            if (i < steps.Count - 1)
            {
                var arrowX = 12 + boxWidth / 2;
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x96)), 1);
                dc.DrawLine(pen, new Point(arrowX, y + boxHeight), new Point(arrowX, y + boxHeight + 12));
            }

            y += boxHeight + 16;
        }

        Height = 16 + steps.Count * 68;
    }

    private static FormattedText MakeText(
        string text,
        double size,
        FontWeight weight,
        Color? color,
        DpiScale dpi) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontWeights.Regular),
            size,
            new SolidColorBrush(color ?? Color.FromRgb(0xDD, 0xE4, 0xF0)),
            dpi.PixelsPerDip);
}
