using System.Text.Json;
using PersonaChat.Core.Models;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core.Services;

public sealed class ScenarioRepository
{
    private const int MaxFieldLength = 600;

    private readonly IReadOnlyList<SimulationScenario> _simulations;
    private readonly IReadOnlyList<EmailScenario> _emails;

    public ScenarioRepository(DataPaths paths)
    {
        _simulations = Load<SimulationScenario>(paths.Simulations, s => s.Id,
            s => [s.Category, s.Channel, s.Title, s.Counterpart, s.Situation, s.Opening, s.Goal]);
        _emails = Load<EmailScenario>(paths.Emails, e => e.Id,
            e => [e.Category, e.Title, e.To, e.Situation, e.Task]);
    }

    public IReadOnlyList<SimulationScenario> GetSimulations() => _simulations;

    public IReadOnlyList<EmailScenario> GetEmails() => _emails;

    public SimulationScenario RequireSimulation(string id) =>
        _simulations.FirstOrDefault(s => s.Id == id) ?? throw new ResourceNotFoundException("This simulation does not exist.");

    public EmailScenario RequireEmail(string id) =>
        _emails.FirstOrDefault(e => e.Id == id) ?? throw new ResourceNotFoundException("This email scenario does not exist.");

    private static List<T> Load<T>(string path, Func<T, string> id, Func<T, string?[]> fields)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"Scenario file not found: {path}");

        var items = Parse<T>(path);
        if (items.Count == 0)
            throw new InvalidDataException($"The scenario file is empty: {path}");

        foreach (var item in items)
            Validate(path, id(item), fields(item));

        var duplicate = items.GroupBy(id).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null ? items : throw new InvalidDataException($"Duplicate scenario id in {path}: '{duplicate.Key}'.");
    }

    private static List<T> Parse<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The scenario file is not valid JSON: {path}: {ex.Message}", ex);
        }
    }

    private static void Validate(string path, string? id, string?[] fields)
    {
        if (!IdRules.IsValid(id))
            throw new InvalidDataException($"Invalid scenario id in {path}: '{id}'.");
        if (fields.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"Scenario '{id}' in {path} has an empty required field.");
        if (fields.Any(field => field!.Length > MaxFieldLength))
            throw new InvalidDataException($"Scenario '{id}' in {path} has a field longer than {MaxFieldLength} characters.");
    }
}
