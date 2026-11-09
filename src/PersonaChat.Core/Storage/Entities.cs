using PersonaChat.Core.Models;

namespace PersonaChat.Core.Storage;

// personas.json holds the full definition; this row anchors the foreign keys.
public sealed class PersonaEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}

public sealed class ConversationEntity
{
    public int Id { get; set; }
    public required string PersonaId { get; set; }
    public int MessagesSinceNotes { get; set; }
    public int ReportedThroughMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MessageEntity
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public ChatRole Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MemoryNoteEntity
{
    public int Id { get; set; }
    public required string PersonaId { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SimulationSessionEntity
{
    public int Id { get; set; }
    public required string ScenarioId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class SimulationMessageEntity
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ChatRole Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ReportEntity
{
    public int Id { get; set; }
    public ReportKind Kind { get; set; }
    public ReportStatus Status { get; set; }
    public required string Title { get; set; }
    public string? ScenarioId { get; set; }
    public string? Context { get; set; }
    public required string SourceText { get; set; }
    public string? ResultJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
