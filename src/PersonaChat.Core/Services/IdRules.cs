using System.Text.RegularExpressions;

namespace PersonaChat.Core.Services;

internal static partial class IdRules
{
    [GeneratedRegex(@"^[a-z0-9-]{1,40}\z")]
    private static partial Regex Pattern();

    public static bool IsValid(string? id) => id is not null && Pattern().IsMatch(id);
}
