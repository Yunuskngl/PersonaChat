using Microsoft.Extensions.Options;
using PersonaChat.Core.Models;
using PersonaChat.Core.Services;

namespace PersonaChat.Web;

public sealed record SendMessageRequest(string? Text, ChatMode Mode);

public sealed record SimulationMessageRequest(string? Text);

public sealed record SubmitEmailRequest(string? Subject, string? Body);

// An empty JSON body forces a CORS preflight, so other websites cannot trigger these actions.
public sealed record EmptyRequest;

public static class ApiEndpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/ping", () => Results.NoContent());
        api.MapGet("/settings", GetSettings);
        api.MapGet("/personas", (PersonaRepository personas) => personas.GetAll());

        MapPersonas(api);
        MapSimulations(api);
        MapEmails(api);
        MapReports(api);
    }

    private static object GetSettings(IOptions<ChatOptions> chat, IOptions<ReportOptions> reports) =>
        new { chat.Value.MaxInputLength, reports.Value.MaxEmailLength };

    private static void MapPersonas(RouteGroupBuilder api)
    {
        var persona = api.MapGroup("/personas/{id}").AddEndpointFilter(RequirePersona);
        persona.MapGet("/messages", (string id, ChatService chat, CancellationToken ct) => chat.GetHistoryAsync(id, ct));
        persona.MapPost("/messages", SendMessage);
        persona.MapDelete("/messages", Reset);
        persona.MapPost("/end", EndChat);
        persona.MapGet("/memory", (string id, MemoryService memory, CancellationToken ct) => memory.GetNotesAsync(id, ct));
    }

    private static void MapSimulations(RouteGroupBuilder api)
    {
        api.MapGet("/simulations", (ScenarioRepository scenarios) => scenarios.GetSimulations());
        api.MapPost("/simulations/{id}/session", StartSimulation);
        api.MapPost("/simulation-sessions/{sessionId:int}/messages", SendSimulationMessage);
        api.MapPost("/simulation-sessions/{sessionId:int}/end", EndSimulation);
    }

    private static void MapEmails(RouteGroupBuilder api)
    {
        api.MapGet("/emails/spin", (EmailPracticeService emails, CancellationToken ct) => emails.SpinAsync(ct));
        api.MapPost("/emails/{id}/submit", SubmitEmail);
    }

    private static void MapReports(RouteGroupBuilder api)
    {
        api.MapGet("/reports", (ReportService reports, CancellationToken ct) => reports.ListAsync(ct));
        api.MapGet("/reports/{id:int}", (int id, ReportService reports, CancellationToken ct) => reports.GetAsync(id, ct));
        api.MapPost("/reports/{id:int}/retry", RetryReport);
        api.MapGet("/reports/{id:int}/pdf", DownloadPdf);
    }

    private static ValueTask<object?> RequirePersona(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var id = (string)context.HttpContext.GetRouteValue("id")!;
        context.HttpContext.RequestServices.GetRequiredService<PersonaRepository>().Require(id);
        return next(context);
    }

    private static async Task<object> SendMessage(string id, SendMessageRequest request, ChatService chat, CancellationToken ct) =>
        new { Reply = await chat.SendAsync(id, request.Text, request.Mode, ct) };

    private static async Task<IResult> Reset(string id, MemoryService memory, CancellationToken ct)
    {
        await memory.ResetAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<object> EndChat(string id, EmptyRequest _, ChatService chat, CancellationToken ct) =>
        new { ReportId = await chat.EndSessionAsync(id, ct) };

    private static Task<SimulationSessionView> StartSimulation(string id, EmptyRequest _, SimulationService simulations, CancellationToken ct) =>
        simulations.StartAsync(id, ct);

    private static async Task<object> SendSimulationMessage(int sessionId, SimulationMessageRequest request, SimulationService simulations, CancellationToken ct) =>
        new { Reply = await simulations.SendAsync(sessionId, request.Text, ct) };

    private static async Task<object> EndSimulation(int sessionId, EmptyRequest _, SimulationService simulations, CancellationToken ct) =>
        new { ReportId = await simulations.EndAsync(sessionId, ct) };

    private static async Task<object> SubmitEmail(string id, SubmitEmailRequest request, EmailPracticeService emails, CancellationToken ct) =>
        new { ReportId = await emails.SubmitAsync(id, request.Subject, request.Body, ct) };

    private static async Task<IResult> RetryReport(int id, EmptyRequest _, ReportService reports, CancellationToken ct)
    {
        await reports.RetryAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadPdf(int id, ReportService reports, CancellationToken ct)
    {
        var report = await reports.GetAsync(id, ct);
        if (report.Content is null)
            throw new InvalidInputException("This report is not ready yet.");

        return Results.File(ReportPdf.Build(report, report.Content), "application/pdf", $"personachat-report-{id}.pdf");
    }
}
