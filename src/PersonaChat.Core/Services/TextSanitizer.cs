namespace PersonaChat.Core.Services;

public static class TextSanitizer
{
    private const char LineSeparator = '\u2028';
    private const char ParagraphSeparator = '\u2029';

    public static string CleanInput(string? text, int maxLength)
    {
        var cleaned = new string((text ?? "").Where(IsAllowedInInput).ToArray()).Trim();

        if (cleaned.Length == 0)
            throw new InvalidInputException("Please type a message first.");
        if (cleaned.Length > maxLength)
            throw new InvalidInputException($"Your message is too long. The limit is {maxLength} characters.");

        return cleaned;
    }

    /// <summary>Returns null when nothing usable is left.</summary>
    public static string? CleanNote(string text, int maxLength)
    {
        // Angle brackets are removed so a note cannot close the <memory> data block.
        var cleaned = new string(text.Where(IsAllowedInNote).ToArray()).Trim();
        if (cleaned.Length == 0)
            return null;

        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].TrimEnd();
    }

    private static bool IsAllowedInInput(char c) =>
        c is '\n' or '\t' || !IsControl(c);

    private static bool IsAllowedInNote(char c) =>
        c is not ('<' or '>') && !IsControl(c);

    private static bool IsControl(char c) =>
        char.IsControl(c) || c is LineSeparator or ParagraphSeparator;
}
