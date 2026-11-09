namespace PersonaChat.Core.Storage;

/// <summary>
/// Templates ship with the app and are the same for everyone.
/// The user folder holds what belongs to one person: conversations, memory and reports.
/// </summary>
public sealed record DataPaths(string TemplateFolder, string UserFolder)
{
    private const string PersonasFile = "personas.json";
    private const string SimulationsFile = "simulations.json";
    private const string EmailsFile = "email-scenarios.json";
    private const string DatabaseFile = "personachat.db";

    public DataPaths(string folder) : this(folder, folder) { }

    public string Personas => Path.Combine(TemplateFolder, PersonasFile);
    public string Simulations => Path.Combine(TemplateFolder, SimulationsFile);
    public string Emails => Path.Combine(TemplateFolder, EmailsFile);
    public string Database => Path.Combine(UserFolder, DatabaseFile);
}
