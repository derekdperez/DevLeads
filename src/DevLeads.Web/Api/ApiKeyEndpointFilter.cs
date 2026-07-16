using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Web.Api;

/// <summary>
/// Requires the operator API key (X-Api-Key header or api_key query param) on every /api
/// endpoint, except the small allowlist a browser must reach without custom headers:
/// OAuth redirect round-trips and the public resume download. A blank stored key fails
/// closed. The Blazor UI is unaffected — it calls services directly, not this API.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";
    public const string QueryName = "api_key";

    // Method + exact path (or prefix for the document download). LinkedIn (and any future
    // OAuth provider) redirects the browser here with query params only — no headers.
    private static readonly (string Method, string Path, bool Prefix)[] Exempt =
    {
        ("GET", "/api/linkedin/authorize", false),
        ("GET", "/api/linkedin/callback", false),
        ("GET", "/api/documents/", true) // resume download link; POST/DELETE stay protected
    };

    private readonly IDbContextFactory<DevLeadsDbContext> _dbFactory;

    public ApiKeyEndpointFilter(IDbContextFactory<DevLeadsDbContext> dbFactory) => _dbFactory = dbFactory;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var path = request.Path.Value ?? "";

        foreach (var (method, exemptPath, prefix) in Exempt)
        {
            if (!HttpMethods.Equals(request.Method, method)) continue;
            if (prefix ? path.StartsWith(exemptPath, StringComparison.OrdinalIgnoreCase)
                       : path.Equals(exemptPath, StringComparison.OrdinalIgnoreCase))
                return await next(context);
        }

        var provided = request.Headers[HeaderName].FirstOrDefault()
                       ?? request.Query[QueryName].FirstOrDefault()
                       ?? "";

        await using var db = await _dbFactory.CreateDbContextAsync(context.HttpContext.RequestAborted);
        var stored = await db.OperatorSettings.AsNoTracking()
            .Select(s => s.ApiKey).FirstOrDefaultAsync(context.HttpContext.RequestAborted) ?? "";

        // Fail closed on a blank stored key; constant-time compare otherwise.
        var ok = stored.Length > 0 && provided.Length > 0 &&
                 CryptographicOperations.FixedTimeEquals(
                     Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(provided));
        if (!ok)
            return Results.Json(
                new { message = "Invalid or missing API key. Pass it as the X-Api-Key header (see Settings)." },
                statusCode: StatusCodes.Status401Unauthorized);

        return await next(context);
    }
}
