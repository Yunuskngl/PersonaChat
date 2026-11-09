namespace PersonaChat.Web;

public sealed class SecurityHeaders(RequestDelegate next)
{
    private const string ContentPolicy = "default-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    }
}
