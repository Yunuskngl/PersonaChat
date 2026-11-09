using PersonaChat.Core.Storage;

namespace PersonaChat.Tests;

public class DataPathsTests
{
    private readonly DataPaths _paths = new("/app/Data", "/home/person");

    [Fact]
    public void Templates_ComeFromTheApp()
    {
        Assert.StartsWith("/app/Data", _paths.Personas);
        Assert.StartsWith("/app/Data", _paths.Simulations);
        Assert.StartsWith("/app/Data", _paths.Emails);
    }

    [Fact]
    public void Database_StaysInTheUserFolder()
    {
        Assert.StartsWith("/home/person", _paths.Database);
    }

    [Fact]
    public void OneFolder_HoldsEverythingDuringDevelopment()
    {
        var paths = new DataPaths("/project/Data");

        Assert.Equal("/project/Data", paths.TemplateFolder);
        Assert.Equal("/project/Data", paths.UserFolder);
    }
}
