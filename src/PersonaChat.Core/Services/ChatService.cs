using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core.Services;

public sealed class ChatService(
    AppDbContext db,
    ILlmClient llm,
    PersonaRepository personas,
    PromptBuilder prompts,
    MemoryService memory,
    MemoryQueue memoryQueue,
    ReportService reports,
    ConversationGate gate,
    IOptions<ChatOptions> chatOptions,
    IOptions<MemoryOptions> memoryOptions)
{
    private readonly ChatOptions _chat = chatOptions.Value;
    private readonly MemoryOptions _memory = memoryOptions.Value;

    public Task<string> SendAsync(string personaId, string? text, ChatMode mode, CancellationToken ct)
    {
        var persona = personas.Require(personaId);
        var cleaned = TextSanitizer.CleanInput(text, _chat.MaxInputLength);
        return gate.RunAsync(() => RunTurnAsync(persona, cleaned, mode, ct), ct);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(string personaId, CancellationToken ct)
    {
        var conversation = await db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.PersonaId == personaId, ct);
        return conversation is null ? [] : await LoadRecentAsync(conversation.Id, _chat.MaxDisplayMessages, ct);
    }

    /// <summary>Queues a report of what was written since the last one. Returns null when there is too little.</summary>
    public Task<int?> EndSessionAsync(string personaId, CancellationToken ct)
    {
        var persona = personas.Require(personaId);
        return gate.RunAsync(() => CreateReportAsync(persona, ct), ct);
    }

    private async Task<string> RunTurnAsync(Persona persona, string text, ChatMode mode, CancellationToken ct)
    {
        var conversation = await GetOrCreateConversationAsync(persona.Id, ct);
        var notes = await memory.GetNotesAsync(persona.Id, ct);
        var recent = await LoadRecentAsync(conversation.Id, _memory.ShortTermMessages, ct);
        var stage = StageFor(await db.Messages.CountAsync(m => m.ConversationId == conversation.Id, ct));

        var messages = prompts.TrimHistory([.. recent, new ChatMessage(ChatRole.User, text)], _memory.ShortTermMessages);
        var reply = await llm.SendAsync(new LlmRequest(prompts.Build(persona, mode, stage, notes), messages), ct);

        await SaveTurnAsync(conversation, text, reply, ct);
        if (conversation.MessagesSinceNotes >= _memory.SummarizeEvery)
            memoryQueue.Enqueue(persona.Id);

        return reply;
    }

    private RelationshipStage StageFor(int messageCount)
    {
        if (messageCount < _chat.IntroMessages)
            return RelationshipStage.Introduction;

        return messageCount < _chat.FriendMessages ? RelationshipStage.GettingToKnow : RelationshipStage.Friends;
    }

    private async Task<int?> CreateReportAsync(Persona persona, CancellationToken ct)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.PersonaId == persona.Id, ct);
        if (conversation is null)
            return null;

        var unreported = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && m.Role == ChatRole.User && m.Id > conversation.ReportedThroughMessageId)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Content })
            .ToListAsync(ct);

        var source = string.Join('\n', unreported.Select(m => m.Content));
        if (!reports.HasEnoughText(source))
            return null;

        conversation.ReportedThroughMessageId = unreported[^1].Id;
        return await reports.CreateAsync(new ReportDraft(ReportKind.Chat, $"Chat with {persona.Name}", null, null, source), ct);
    }

    private async Task<ConversationEntity> GetOrCreateConversationAsync(string personaId, CancellationToken ct)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.PersonaId == personaId, ct);
        if (conversation is not null)
            return conversation;

        conversation = new ConversationEntity { PersonaId = personaId, CreatedAt = DateTime.UtcNow };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return conversation;
    }

    private async Task<IReadOnlyList<ChatMessage>> LoadRecentAsync(int conversationId, int count, CancellationToken ct)
    {
        var newestFirst = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.Id)
            .Take(count)
            .Select(m => new ChatMessage(m.Role, m.Content))
            .ToListAsync(ct);

        newestFirst.Reverse();
        return newestFirst;
    }

    // Saved only after a successful reply, so a failed call leaves no half turn behind.
    private async Task SaveTurnAsync(ConversationEntity conversation, string text, string reply, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        db.Messages.AddRange(
            new MessageEntity { ConversationId = conversation.Id, Role = ChatRole.User, Content = text, CreatedAt = now },
            new MessageEntity { ConversationId = conversation.Id, Role = ChatRole.Assistant, Content = reply, CreatedAt = now });

        conversation.MessagesSinceNotes += 2;
        await db.SaveChangesAsync(ct);
    }
}
