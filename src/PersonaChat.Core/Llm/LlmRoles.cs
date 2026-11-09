using PersonaChat.Core.Models;

namespace PersonaChat.Core.Llm;

internal static class LlmRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";

    public static string Of(ChatRole role) => role == ChatRole.User ? User : Assistant;
}
