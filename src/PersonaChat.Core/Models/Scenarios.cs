namespace PersonaChat.Core.Models;

public sealed record SimulationScenario(
    string Id,
    string Category,
    string Channel,
    string Title,
    string Counterpart,
    string Situation,
    string Opening,
    string Goal);

public sealed record EmailScenario(
    string Id,
    string Category,
    string Title,
    string To,
    string Situation,
    string Task);

public sealed record EmailSpin(int Slot, int SlotCount, EmailScenario Scenario);

public sealed record SimulationSessionView(int SessionId, IReadOnlyList<ChatMessage> Messages);
