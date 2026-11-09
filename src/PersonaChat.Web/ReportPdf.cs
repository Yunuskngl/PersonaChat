using PersonaChat.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PersonaChat.Web;

public static class ReportPdf
{
    private const float PageMargin = 42;
    private const float BodySize = 10;
    private const float HeadingSize = 13;
    private const float TitleSize = 22;
    private const float SectionGap = 16;
    private const float ItemGap = 8;

    public static byte[] Build(ReportDetails report, ReportContent content) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(PageMargin);
            page.DefaultTextStyle(text => text.FontSize(BodySize).FontColor(Colors.Black));
            page.Content().Column(column => WriteContent(column, report, content));
            page.Footer().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();

    private static void WriteContent(ColumnDescriptor column, ReportDetails report, ReportContent content)
    {
        column.Spacing(SectionGap);
        WriteHeader(column, report, content);
        WriteSection(column, "Teacher's comment", content.Summary.Length > 0, c => c.Item().Text(content.Summary));
        WriteSection(column, $"Mistakes and corrections ({content.Mistakes.Count})", content.Mistakes.Count > 0, c => WriteMistakes(c, content));
        WriteSection(column, "Sound more natural", content.Phrasing.Count > 0, c => WritePhrasing(c, content));
        WriteSection(column, "What you did well", content.Strengths.Count > 0, c => WriteBullets(c, content.Strengths));
        WriteSection(column, "What to practise next", content.NextSteps.Count > 0, c => WriteBullets(c, content.NextSteps));
        WriteSection(column, "How a native speaker would write it", content.Rewrite is not null, c => c.Item().Text(content.Rewrite));
        WriteSection(column, $"Words you used ({content.TotalWords} in total)", content.Words.Count > 0, c => WriteWords(c, content));
    }

    private static void WriteHeader(ColumnDescriptor column, ReportDetails report, ReportContent content)
    {
        column.Item().Text(report.Title).FontSize(TitleSize).Bold();
        column.Item().Text($"{report.Kind} report · {report.CreatedAt:d MMMM yyyy} · Estimated level: {content.Level}");
        column.Item().LineHorizontal(1);
    }

    private static void WriteSection(ColumnDescriptor column, string heading, bool visible, Action<ColumnDescriptor> body)
    {
        if (!visible)
            return;

        column.Item().Column(section =>
        {
            section.Spacing(ItemGap);
            section.Item().Text(heading).FontSize(HeadingSize).Bold();
            body(section);
        });
    }

    private static void WriteMistakes(ColumnDescriptor column, ReportContent content)
    {
        foreach (var mistake in content.Mistakes)
        {
            column.Item().ShowEntire().Column(item =>
            {
                item.Item().Text(text =>
                {
                    text.Span(mistake.Original).Strikethrough();
                    text.Span("  →  ");
                    text.Span(mistake.Corrected).Bold();
                });
                item.Item().Text($"{mistake.Type}: {mistake.Explanation}").Italic();
            });
        }
    }

    private static void WritePhrasing(ColumnDescriptor column, ReportContent content)
    {
        foreach (var phrase in content.Phrasing)
        {
            column.Item().ShowEntire().Column(item =>
            {
                item.Item().Text($"You wrote: {phrase.Original}");
                item.Item().Text($"A native would say: {phrase.Natural}").Bold();
                item.Item().Text(phrase.Why).Italic();
            });
        }
    }

    private static void WriteBullets(ColumnDescriptor column, IReadOnlyList<string> items)
    {
        foreach (var item in items)
            column.Item().Text($"•  {item}");
    }

    private static void WriteWords(ColumnDescriptor column, ReportContent content)
    {
        column.Item().Text(text =>
        {
            foreach (var word in content.Words)
            {
                var span = text.Span($"{word.Word} ×{word.Count}");
                if (!word.Correct)
                    span.Bold().Underline();
                text.Span("   ");
            }
        });
        column.Item().Text("Bold and underlined words were used incorrectly at least once.").Italic();
    }
}
