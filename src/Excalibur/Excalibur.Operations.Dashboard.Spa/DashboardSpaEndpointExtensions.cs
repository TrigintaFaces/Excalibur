using Excalibur.Operations.Dashboard.Spa;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Endpoint-routing extensions that serve the embedded Excalibur Operations
/// Dashboard single-page app.
/// </summary>
public static class DashboardSpaEndpointExtensions
{
    private const string DefaultBasePath = "/dashboard";
    private const string EmbeddedRoot = "wwwroot";

    /// <summary>
    /// Maps the embedded dashboard SPA under <paramref name="basePath"/>. The app
    /// and its hashed assets are served from embedded resources (no external CDN)
    /// with a strict Content-Security-Policy; unmatched sub-paths fall back to the
    /// SPA entry document for client-side routing. Read-only: only GET routes are
    /// mapped, so serving the UI never exposes a mutating surface.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to add the routes to.</param>
    /// <param name="basePath">The path prefix the SPA is served under. Defaults to <c>/dashboard</c>.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> for the mapped routes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointConventionBuilder MapDashboardSpa(this IEndpointRouteBuilder endpoints, string basePath = DefaultBasePath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var normalized = NormalizeBasePath(basePath);

        var fileProvider = new ManifestEmbeddedFileProvider(
            typeof(DashboardSpaEndpointExtensions).Assembly,
            EmbeddedRoot);
        var assets = new DashboardSpaAssets(fileProvider);

        // Use the RequestDelegate overloads (not the reflection-bound Delegate
        // overloads) so the serving path stays trim- and native-AOT-safe.
        // Serve only the SPA's own routes — the exact entry document and its
        // hashed asset files. Deliberately NO broad "{basePath}/{**path}"
        // catch-all: the dashboard is a single page (in-page anchor navigation),
        // and a catch-all under the base path shadows the read API mapped at
        // "{basePath}/api" at the routing-matcher level (the API leaf endpoints
        // stop being match candidates). Keeping the surface to exact routes means
        // the SPA and the API compose cleanly under one prefix.
        var group = endpoints.MapGroup(normalized);

        // Exact SPA entry document at the base path.
        group.MapGet("", assets.ServeIndexAsync);

        // Real hashed static assets. ASP0018: the '{**path}' value is read from
        // RouteValues rather than bound as a handler parameter because a
        // RequestDelegate (used deliberately for trim/AOT safety) does not perform
        // parameter binding.
#pragma warning disable ASP0018
        group.MapGet(
            "/assets/{**path}",
            context => assets.ServeAssetAsync(context, context.Request.RouteValues["path"] as string));
#pragma warning restore ASP0018

        return group;
    }

    private static string NormalizeBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || basePath == "/")
        {
            return string.Empty;
        }

        var trimmed = basePath.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.TrimEnd('/');
    }
}
