using Microsoft.EntityFrameworkCore;
using PersonaChat.Core.Services;

namespace PersonaChat.Core.Storage;

public sealed class DatabaseInitializer(AppDbContext db, PersonaRepository personas, ScenarioRepository scenarios)
{
    public async Task RunAsync(CancellationToken ct)
    {
        // Fails at start-up, with a clear message, when a scenario file is broken.
        _ = scenarios.GetSimulations();

        await db.Database.MigrateAsync(ct);
        await SyncPersonasAsync(ct);
    }

    private async Task SyncPersonasAsync(CancellationToken ct)
    {
        var rows = await db.Personas.ToDictionaryAsync(p => p.Id, ct);

        foreach (var persona in personas.GetAll())
        {
            if (rows.TryGetValue(persona.Id, out var row))
                row.Name = persona.Name;
            else
                db.Personas.Add(new PersonaEntity { Id = persona.Id, Name = persona.Name });
        }

        await db.SaveChangesAsync(ct);
    }
}
