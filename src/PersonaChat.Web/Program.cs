using System.Text.Json;
using System.Text.Json.Serialization;
using PersonaChat.Core;
using PersonaChat.Core.Storage;
using PersonaChat.Web;

const string DataFolderName = "Data";
const long MaxRequestBodyBytes = 64 * 1024;
const string StaticCachePolicy = "no-cache";

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // A double-clicked app does not start in its own folder.
    ContentRootPath = DesktopHost.IsPackaged ? AppContext.BaseDirectory : null,
});

var desktop = DesktopHost.IsPackaged ? DesktopHost.Configure(builder) : null;
if (desktop is not null && await DesktopHost.IsRunningAsync(desktop))
{
    DesktopHost.OpenBrowser(desktop);
    return;
}

var templateFolder = Path.Combine(builder.Environment.ContentRootPath, DataFolderName);
builder.Services.AddPersonaChatCore(builder.Configuration, new DataPaths(templateFolder, desktop?.UserFolder ?? templateFolder));
builder.Services.AddHostedService<MemoryWorker>();
builder.Services.AddHostedService<ReportWorker>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

var app = builder.Build();

await InitializeDatabaseAsync(app);

if (desktop is not null)
    DesktopHost.Attach(app, desktop);

app.UseMiddleware<SecurityHeaders>();
app.UseExceptionHandler(_ => { });
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // The browser must revalidate, so a new version never mixes with old cached files.
    OnPrepareResponse = file => file.Context.Response.Headers.CacheControl = StaticCachePolicy,
});
app.MapApi();

await app.RunAsync();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().RunAsync(app.Lifetime.ApplicationStopping);
}
