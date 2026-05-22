using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AzureOpenAiBenchmark;

public static class PdfReportGenerator
{
    private static readonly string PrimaryColor = Colors.Blue.Darken3;
    private static readonly string AccentColor = Colors.Blue.Lighten4;
    private static readonly string HeaderRowColor = Colors.Grey.Lighten3;
    private static readonly string BorderColor = Colors.Grey.Medium;

    public static string Save(
        IReadOnlyList<ModelStatistics> stats,
        TimeSpan wallClock,
        DateTimeOffset generatedAt,
        string outputDirectory)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Directory.CreateDirectory(outputDirectory);
        var fileName = $"benchmark-{generatedAt.LocalDateTime:yyyyMMdd-HHmmss}.pdf";
        var path = Path.Combine(outputDirectory, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Azure OpenAI Benchmark Report")
                        .FontSize(20).Bold().FontColor(PrimaryColor);
                    col.Item().Text(
                            $"Generated {generatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} " +
                            $"(UTC offset {generatedAt.Offset:hh\\:mm})")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(6).LineHorizontal(2).LineColor(PrimaryColor);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(20);
                    BuildSummary(col.Item(), stats, wallClock);
                    BuildRankingSection(col.Item(), stats);
                    BuildDetailsSection(col.Item(), stats);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private static void BuildSummary(
        IContainer container,
        IReadOnlyList<ModelStatistics> stats,
        TimeSpan wallClock)
    {
        container.Background(AccentColor).Padding(12).Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Configuration").FontSize(13).Bold().FontColor(PrimaryColor);
            col.Item().Text($"Models tested: {stats.Count}");
            col.Item().Text($"Iterations per model: {BenchmarkConfig.Iterations}");
            col.Item().Text($"Parallel calls per iteration: {BenchmarkConfig.CallsPerIteration}");
            col.Item().Text($"Warm-up calls per model: {BenchmarkConfig.WarmupCallsPerModel}");
            col.Item().Text($"Total billed calls: {stats.Sum(s => s.SampleCount)}");
            col.Item().Text($"Wall-clock duration: {wallClock.TotalSeconds:F1} s");
        });
    }

    private static void BuildRankingSection(IContainer container, IReadOnlyList<ModelStatistics> stats)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("Ranking by P50").FontSize(13).Bold().FontColor(PrimaryColor);

            col.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => RenderRanking(c, "TTFT P50 (ms, lower better)",
                    stats.OrderBy(s => s.TtftMs.P50).Select(s => (s.Deployment, s.TtftMs.P50)).ToArray(), "F1"));
                row.RelativeItem().Element(c => RenderRanking(c, "Total P50 (ms, lower better)",
                    stats.OrderBy(s => s.TotalMs.P50).Select(s => (s.Deployment, s.TotalMs.P50)).ToArray(), "F1"));
                row.RelativeItem().Element(c => RenderRanking(c, "Avg response (chars)",
                    stats.OrderByDescending(s => s.LengthChars.Avg).Select(s => (s.Deployment, s.LengthChars.Avg)).ToArray(), "F0"));
            });
        });
    }

    private static void RenderRanking(IContainer container, string title, (string Name, double Value)[] rows, string format)
    {
        container.Border(1).BorderColor(BorderColor).Padding(8).Column(col =>
        {
            col.Spacing(3);
            col.Item().Text(title).Bold().FontSize(10);
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(18);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                });
                for (var i = 0; i < rows.Length; i++)
                {
                    var (name, value) = rows[i];
                    table.Cell().Text($"{i + 1}.").FontSize(9);
                    table.Cell().Text(name).FontSize(9);
                    table.Cell().AlignRight().Text(value.ToString(format)).FontSize(9);
                }
            });
        });
    }

    private static void BuildDetailsSection(IContainer container, IReadOnlyList<ModelStatistics> stats)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("Detailed statistics").FontSize(13).Bold().FontColor(PrimaryColor);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    foreach (var label in new[] { "Model", "Metric" })
                    {
                        header.Cell().Background(HeaderRowColor).Padding(5)
                            .Text(label).Bold().FontSize(9);
                    }
                    foreach (var label in new[] { "Min", "Avg", "P50", "P75", "P95", "Max" })
                    {
                        header.Cell().Background(HeaderRowColor).Padding(5).AlignRight()
                            .Text(label).Bold().FontSize(9);
                    }
                });

                foreach (var s in stats)
                {
                    AppendMetricRow(table, s.Deployment, "TTFT (ms)", s.TtftMs, "F1", topBorder: true);
                    AppendMetricRow(table, string.Empty, "Total (ms)", s.TotalMs, "F1");
                    AppendMetricRow(table, string.Empty, "Length (chars)", s.LengthChars, "F0");
                }
            });
        });
    }

    private static void AppendMetricRow(
        TableDescriptor table,
        string model,
        string metric,
        MetricStats m,
        string format,
        bool topBorder = false)
    {
        var border = topBorder ? 1f : 0f;

        table.Cell().BorderTop(border).BorderColor(BorderColor).Padding(4)
            .Text(model).FontSize(9).Bold();
        table.Cell().BorderTop(border).BorderColor(BorderColor).Padding(4)
            .Text(metric).FontSize(9);

        foreach (var value in new[] { m.Min, m.Avg, m.P50, m.P75, m.P95, m.Max })
        {
            table.Cell().BorderTop(border).BorderColor(BorderColor).Padding(4).AlignRight()
                .Text(value.ToString(format)).FontSize(9);
        }
    }
}
