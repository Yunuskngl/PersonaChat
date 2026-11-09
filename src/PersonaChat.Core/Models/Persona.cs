namespace PersonaChat.Core.Models;

public sealed record Persona(
    string Id,
    string Name,
    int Age,
    string Location,
    string Backstory,
    IReadOnlyList<string> Expertise,
    string Personality,
    string SpeakingStyle);
