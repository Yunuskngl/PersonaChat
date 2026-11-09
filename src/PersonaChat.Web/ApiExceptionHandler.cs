using Microsoft.AspNetCore.Diagnostics;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Services;

namespace PersonaChat.Web;

/// <summary>Turns exceptions into friendly JSON. Details stay in the log.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private const string GenericMessage = "Something went wrong. Please try again.";

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return true;

        var (status, message) = Map(exception);
        if (status >= StatusCodes.Status500InternalServerError && exception is not LlmException)
            logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { Error = message }, ct);
        return true;
    }

    private static (int Status, string Message) Map(Exception exception) => exception switch
    {
        InvalidInputException e => (StatusCodes.Status400BadRequest, e.Message),
        PersonaNotFoundException => (StatusCodes.Status404NotFound, "This character does not exist."),
        ResourceNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
        LlmException e => (StatusCodes.Status503ServiceUnavailable, e.Message),
        BadHttpRequestException e => (e.StatusCode, "The request could not be read."),
        _ => (StatusCodes.Status500InternalServerError, GenericMessage),
    };
}
