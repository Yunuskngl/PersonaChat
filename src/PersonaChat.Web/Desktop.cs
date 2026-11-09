using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PersonaChat.Core.Services;

namespace PersonaChat.Web;

public sealed record DesktopOptions
{
    public const string Section = "Desktop";

    public string Url { get; init; } = "";
    public string UserFolder { get; init; } = "";
    public bool OpenBrowser { get; init; } = true;
    public int IdleMinutes { get; init; } = 5;
}

/// <summary>Runs the web app as a double-click application for one person on one computer.</summary>
public static class DesktopHost
{
    private const string MarkerFile = "desktop.json";
    private const string SettingsFile = "settings.json";
    private const string AppFolderName = "PersonaChat";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);

    private static string MarkerPath => Path.Combine(AppContext.BaseDirectory, MarkerFile);

    // Only the packaged build ships this file, so "dotnet run" keeps working as before.
    public static bool IsPackaged => File.Exists(MarkerPath);

    public static DesktopOptions Configure(WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile(MarkerPath);
        builder.Configuration.AddEnvironmentVariables();

        var options = ReadOptions(builder.Configuration);
        Directory.CreateDirectory(options.UserFolder);

        // Lets each person pick another model without touching the app itself.
        builder.Configuration.AddJsonFile(Path.Combine(options.UserFolder, SettingsFile), optional: true);
        builder.WebHost.UseUrls(options.Url);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ActivityTracker>();
        builder.Services.AddHostedService<IdleShutdown>();
        return options;
    }

    public static async Task<bool> IsRunningAsync(DesktopOptions options)
    {
        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(ConnectTimeout);
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, new Uri(options.Url).Port, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    public static void Attach(WebApplication app, DesktopOptions options)
    {
        var activity = app.Services.GetRequiredService<ActivityTracker>();
        app.Use((context, next) =>
        {
            activity.Touch();
            return next(context);
        });

        app.Logger.LogInformation("User data folder: {Folder}", options.UserFolder);
        app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(options));
    }

    public static void OpenBrowser(DesktopOptions options)
    {
        if (options.OpenBrowser)
            Process.Start(new ProcessStartInfo(options.Url) { UseShellExecute = true });
    }

    private static DesktopOptions ReadOptions(IConfiguration config)
    {
        var options = config.GetSection(DesktopOptions.Section).Get<DesktopOptions>() ?? new DesktopOptions();
        if (!string.IsNullOrWhiteSpace(options.UserFolder))
            return options;

        // Without DoNotVerify the result is empty when the folder does not exist yet.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        return options with { UserFolder = Path.Combine(appData, AppFolderName) };
    }
}

public sealed class ActivityTracker
{
    private int _active;

    public void Touch() => Interlocked.Exchange(ref _active, 1);

    public bool TakeActive() => Interlocked.Exchange(ref _active, 0) == 1;
}

/// <summary>Stops the server after the browser is closed, once no report is still being written.</summary>
public sealed class IdleShutdown(
    ActivityTracker activity,
    DesktopOptions options,
    IServiceScopeFactory scopes,
    IHostApplicationLifetime lifetime,
    ILogger<IdleShutdown> logger) : BackgroundService
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var quietLimit = (int)(TimeSpan.FromMinutes(options.IdleMinutes) / CheckEvery);
        var quietChecks = 0;

        // Counting checks, not clock time: a sleeping laptop must not look idle when it wakes up.
        using var timer = new PeriodicTimer(CheckEvery);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            quietChecks = activity.TakeActive() ? 0 : quietChecks + 1;
            if (quietChecks < quietLimit || await HasPendingReportsAsync(stoppingToken))
                continue;

            logger.LogInformation("No browser activity, shutting down");
            lifetime.StopApplication();
            return;
        }
    }

    private async Task<bool> HasPendingReportsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReportService>().HasPendingAsync(ct);
    }
}
