using System.Text.Json;
using PersonaChat.Core.Models;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core.Services;

public sealed class PersonaRepository
{
    private const int MinAge = 18;
    private const int MaxAge = 120;
    private const int MaxShortText = 80;
    private const int MaxLongText = 1000;
    private const int MaxExpertiseItems = 10;

    private readonly IReadOnlyList<Persona> _all;
    private readonly Dictionary<string, Persona> _byId;

    public PersonaRepository(DataPaths paths)
    {
        _all = Load(paths.Personas);
        _byId = _all.ToDictionary(p => p.Id);
    }

    public IReadOnlyList<Persona> GetAll() => _all;

    public Persona Require(string id) =>
        _byId.TryGetValue(id, out var persona) ? persona : throw new PersonaNotFoundException(id);

    private static List<Persona> Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"Persona file not found: {path}");

        var personas = Parse(File.ReadAllText(path));
        if (personas.Count == 0)
            throw new InvalidDataException("The persona file is empty.");

        personas.ForEach(Validate);
        RejectDuplicates(personas);
        return personas;
    }

    private static List<Persona> Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Persona>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The persona file is not valid JSON: {ex.Message}", ex);
        }
    }

    private static void Validate(Persona persona)
    {
        if (!IdRules.IsValid(persona.Id))
            throw new InvalidDataException($"Invalid persona id: '{persona.Id}'.");
        if (persona.Age is < MinAge or > MaxAge)
            throw new InvalidDataException($"Persona '{persona.Id}': age must be {MinAge}-{MaxAge}.");

        RequireText(persona, "name", persona.Name, MaxShortText);
        RequireText(persona, "location", persona.Location, MaxShortText);
        RequireText(persona, "backstory", persona.Backstory, MaxLongText);
        RequireText(persona, "personality", persona.Personality, MaxLongText);
        RequireText(persona, "speakingStyle", persona.SpeakingStyle, MaxLongText);
        ValidateExpertise(persona);
    }

    private static void ValidateExpertise(Persona persona)
    {
        if (persona.Expertise is null || persona.Expertise.Count is 0 or > MaxExpertiseItems)
            throw new InvalidDataException($"Persona '{persona.Id}': expertise needs 1-{MaxExpertiseItems} items.");

        foreach (var item in persona.Expertise)
            RequireText(persona, "expertise", item, MaxShortText);
    }

    private static void RequireText(Persona persona, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Persona '{persona.Id}': {field} is required.");
        if (value.Length > maxLength)
            throw new InvalidDataException($"Persona '{persona.Id}': {field} is longer than {maxLength} characters.");
    }

    private static void RejectDuplicates(List<Persona> personas)
    {
        var duplicate = personas.GroupBy(p => p.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Duplicate persona id: '{duplicate.Key}'.");
    }
}
