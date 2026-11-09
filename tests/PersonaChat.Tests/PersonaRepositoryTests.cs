using PersonaChat.Core.Services;
using PersonaChat.Core.Storage;

namespace PersonaChat.Tests;

public sealed class PersonaRepositoryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void LoadsValidPersonas()
    {
        var repository = Load(Persona("marcus"));

        Assert.Equal("Name", repository.Require("marcus").Name);
        Assert.Single(repository.GetAll());
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("UPPER")]
    [InlineData("has space")]
    [InlineData("")]
    public void RejectsUnsafeIds(string id)
    {
        Assert.Throws<InvalidDataException>(() => Load(Persona(id)));
    }

    [Fact]
    public void RejectsMissingRequiredField()
    {
        var error = Assert.Throws<InvalidDataException>(() => Load("""{ "id": "a", "age": 30 }"""));

        Assert.Contains("name", error.Message);
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        Assert.Throws<InvalidDataException>(() => Load(Persona("a"), Persona("a")));
    }

    [Fact]
    public void RejectsBrokenJson()
    {
        File.WriteAllText(new DataPaths(_folder).Personas, "[{");

        Assert.Throws<InvalidDataException>(() => new PersonaRepository(new DataPaths(_folder)));
    }

    [Fact]
    public void Require_ThrowsForUnknownPersona()
    {
        Assert.Throws<PersonaNotFoundException>(() => Load(Persona("a")).Require("b"));
    }

    private PersonaRepository Load(params string[] personas)
    {
        var paths = new DataPaths(_folder);
        File.WriteAllText(paths.Personas, $"[{string.Join(',', personas)}]");
        return new PersonaRepository(paths);
    }

    private static string Persona(string id) =>
        $$"""{ "id": "{{id}}", "name": "Name", "age": 30, "location": "X", "backstory": "b", "expertise": ["e"], "personality": "p", "speakingStyle": "s" }""";
}
