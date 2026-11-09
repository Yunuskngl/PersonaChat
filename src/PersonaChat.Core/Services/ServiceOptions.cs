namespace PersonaChat.Core.Services;

public sealed class ChatOptions
{
    public const string Section = "Chat";

    public int MaxInputLength { get; set; } = 2000;
    public int MaxDisplayMessages { get; set; } = 200;
    public int IntroMessages { get; set; } = 6;
    public int FriendMessages { get; set; } = 24;
}

public sealed class MemoryOptions
{
    public const string Section = "Memory";

    public int ShortTermMessages { get; set; } = 20;
    public int SummarizeEvery { get; set; } = 20;
    public int MaxNotes { get; set; } = 30;
    public int MaxNoteLength { get; set; } = 200;
}

public sealed class ReportOptions
{
    public const string Section = "Reports";

    public int MinWords { get; set; } = 8;
    public int MaxEmailLength { get; set; } = 3000;
    public int ChunkChars { get; set; } = 3000;
    public int MaxItemsPerChunk { get; set; } = 15;
    public int MaxFieldLength { get; set; } = 400;
    public int MaxRewriteLength { get; set; } = 4000;
}
