using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;

namespace PersonaChat.Core.Services;

/// <summary>Turns a learner's text into a teacher's review. Model output is untrusted and checked here.</summary>
public sealed partial class ReportAnalyzer(ILlmClient llm, IOptions<ReportOptions> options)
{
    private const int MaxAttempts = 3;
    private const int MaxWordsInReport = 300;
    private const string DefaultType = "grammar";

    private static readonly string[] KnownTypes = ["grammar", "vocabulary", "spelling", "punctuation"];
    private static readonly string[] WordErrorTypes = ["vocabulary", "spelling"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ReportOptions _options = options.Value;

    [GeneratedRegex(@"\p{L}[\p{L}'’-]*")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Small models sometimes leave pieces of JSON at the end of a text value, like: word”}],"
    [GeneratedRegex(@"[""”]\s*[\]},][\s""”,\]}{\[]*$")]
    private static partial Regex JsonDebris();

    public async Task<ReportContent> AnalyzeAsync(ReportKind kind, string? context, string source, CancellationToken ct)
    {
        var reviews = new List<ChunkReview>();
        foreach (var chunk in SplitIntoChunks(kind, source))
            reviews.Add(await ReviewChunkAsync(kind, context, chunk, ct));

        return Merge(reviews, source);
    }

    private IEnumerable<string> SplitIntoChunks(ReportKind kind, string source)
    {
        if (kind == ReportKind.Email)
        {
            yield return source;
            yield break;
        }

        var chunk = new StringBuilder();
        foreach (var line in source.Split('\n'))
        {
            if (chunk.Length > 0 && chunk.Length + line.Length > _options.ChunkChars)
            {
                yield return chunk.ToString();
                chunk.Clear();
            }

            chunk.AppendLine(line);
        }

        if (chunk.Length > 0)
            yield return chunk.ToString();
    }

    private async Task<ChunkReview> ReviewChunkAsync(ReportKind kind, string? context, string chunk, CancellationToken ct)
    {
        var message = new ChatMessage(ChatRole.User, ReportPrompts.BuildUserMessage(context, chunk));
        var request = new LlmRequest(ReportPrompts.BuildSystem(kind), [message], ReportPrompts.Schema, LongReply: true);

        LlmException? lastError = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                var review = Parse(await llm.SendAsync(request, ct));
                if (review is not null)
                    return Clean(review, kind, chunk);
            }
            catch (LlmException ex)
            {
                // Small local models fail now and then on long structured output.
                lastError = ex;
            }
        }

        throw lastError ?? new LlmException("The AI model returned an unreadable report.");
    }

    private static RawReview? Parse(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            return JsonSerializer.Deserialize<RawReview>(reply[start..(end + 1)], JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private ChunkReview Clean(RawReview raw, ReportKind kind, string chunk)
    {
        var text = Normalize(chunk);
        var mistakes = (raw.Mistakes ?? [])
            .Select(CleanMistake)
            .OfType<Mistake>()
            .Where(m => Quotes(text, m.Original) && !IsIgnorable(m, kind))
            .Take(_options.MaxItemsPerChunk)
            .ToList();

        var phrasing = (raw.Phrasing ?? [])
            .Select(CleanPhrasing)
            .OfType<NaturalPhrasing>()
            .Where(p => Quotes(text, p.Original))
            .Take(_options.MaxItemsPerChunk)
            .ToList();

        return new ChunkReview(Field(raw.Summary), Field(raw.Level), mistakes, phrasing, CleanList(raw.Strengths), CleanList(raw.NextSteps), CleanRewrite(raw.Rewrite));
    }

    private Mistake? CleanMistake(RawMistake raw)
    {
        var original = Field(raw.Original);
        var corrected = Field(raw.Corrected);
        if (original.Length == 0 || corrected.Length == 0 || original == corrected)
            return null;

        var type = KnownTypes.FirstOrDefault(known => known == raw.Type?.Trim().ToLowerInvariant()) ?? DefaultType;
        return new Mistake(original, corrected, type, Field(raw.Explanation));
    }

    private NaturalPhrasing? CleanPhrasing(RawPhrasing raw)
    {
        var original = Field(raw.Original);
        var natural = Field(raw.Natural);
        return original.Length == 0 || natural.Length == 0 || original == natural
            ? null
            : new NaturalPhrasing(original, natural, Field(raw.Why));
    }

    private string Field(string? value) =>
        value is null ? "" : TextSanitizer.CleanNote(JsonDebris().Replace(value, ""), _options.MaxFieldLength) ?? "";

    private List<string> CleanList(IReadOnlyList<string>? items) =>
        (items ?? []).Select(Field).Where(item => item.Length > 0).Take(_options.MaxItemsPerChunk).ToList();

    private string? CleanRewrite(string? rewrite)
    {
        if (string.IsNullOrWhiteSpace(rewrite))
            return null;

        var cleaned = rewrite.Trim();
        return cleaned.Length <= _options.MaxRewriteLength ? cleaned : cleaned[.._options.MaxRewriteLength];
    }

    // Small models sometimes "correct" text the learner never wrote.
    private static bool Quotes(string text, string original) => text.Contains(Normalize(original), StringComparison.Ordinal);

    private static string Normalize(string text) => Whitespace().Replace(text.Replace('’', '\''), " ").Trim().ToLowerInvariant();

    // Capital letters and final punctuation do not count as mistakes in casual chat.
    private static bool IsIgnorable(Mistake mistake, ReportKind kind) =>
        kind == ReportKind.Chat && LettersOnly(mistake.Original) == LettersOnly(mistake.Corrected);

    private static string LettersOnly(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static ReportContent Merge(List<ChunkReview> reviews, string source)
    {
        var mistakes = reviews.SelectMany(r => r.Mistakes).DistinctBy(m => (m.Original, m.Corrected)).ToList();
        var corrected = mistakes.Select(m => m.Original).ToHashSet();
        var phrasing = reviews.SelectMany(r => r.Phrasing).Where(p => !corrected.Contains(p.Original)).DistinctBy(p => p.Original).ToList();

        return new ReportContent(
            reviews.Select(r => r.Summary).FirstOrDefault(s => s.Length > 0) ?? "",
            reviews.Select(r => r.Level).FirstOrDefault(l => l.Length > 0) ?? "",
            WordPattern().Count(source),
            mistakes,
            phrasing,
            CountWords(source, mistakes),
            reviews.SelectMany(r => r.Strengths).Distinct().ToList(),
            reviews.SelectMany(r => r.NextSteps).Distinct().ToList(),
            reviews.Select(r => r.Rewrite).FirstOrDefault(r => r is not null));
    }

    private static List<WordUse> CountWords(string source, List<Mistake> mistakes)
    {
        var wrong = WrongWords(mistakes);
        return WordPattern().Matches(source)
            .Select(match => CanonicalWord(match.Value))
            .GroupBy(word => word)
            .Select(group => new WordUse(group.Key, group.Count(), !wrong.Contains(group.Key)))
            .OrderBy(word => word.Correct)
            .ThenByDescending(word => word.Count)
            .ThenBy(word => word.Word, StringComparer.Ordinal)
            .Take(MaxWordsInReport)
            .ToList();
    }

    private static HashSet<string> WrongWords(List<Mistake> mistakes)
    {
        var wrong = new HashSet<string>();
        foreach (var mistake in mistakes.Where(m => WordErrorTypes.Contains(m.Type)))
        {
            var corrected = WordPattern().Matches(mistake.Corrected).Select(m => CanonicalWord(m.Value)).ToHashSet();
            wrong.UnionWith(WordPattern().Matches(mistake.Original).Select(m => CanonicalWord(m.Value)).Where(word => !corrected.Contains(word)));
        }

        return wrong;
    }

    private static string CanonicalWord(string word) => word.Replace('’', '\'').ToLowerInvariant();

    private sealed record ChunkReview(
        string Summary,
        string Level,
        List<Mistake> Mistakes,
        List<NaturalPhrasing> Phrasing,
        List<string> Strengths,
        List<string> NextSteps,
        string? Rewrite);

    private sealed record RawReview(
        string? Summary,
        string? Level,
        IReadOnlyList<RawMistake>? Mistakes,
        IReadOnlyList<RawPhrasing>? Phrasing,
        IReadOnlyList<string>? Strengths,
        IReadOnlyList<string>? NextSteps,
        string? Rewrite);

    private sealed record RawMistake(string? Original, string? Corrected, string? Type, string? Explanation);

    private sealed record RawPhrasing(string? Original, string? Natural, string? Why);
}
