using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Operations.Dashboard.Spa;

/// <summary>
/// Serves the embedded Svelte SPA assets from a <see cref="ManifestEmbeddedFileProvider"/>.
/// Hashed asset files are served with an immutable long-lived cache; any
/// unmatched path falls back to <c>index.html</c> (client-side routing) with a
/// no-cache policy. Every response carries a strict Content-Security-Policy and
/// hardening headers.
/// </summary>
internal sealed class DashboardSpaAssets
{
    // Strict CSP: no inline/eval, same-origin scripts/styles/fetch only. The
    // Vite build emits an external module script + external stylesheet, so no
    // 'unsafe-inline' / 'unsafe-eval' is required. data: is allowed for images
    // only (inlined icons). connect-src 'self' covers the same-origin read API.
    private const string ContentSecurityPolicy =
        "default-src 'none'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "base-uri 'none'; " +
        "form-action 'none'; " +
        "frame-ancestors 'none'";

    private const string IndexPath = "index.html";

    private readonly IFileProvider _fileProvider;

    public DashboardSpaAssets(IFileProvider fileProvider)
    {
        ArgumentNullException.ThrowIfNull(fileProvider);
        _fileProvider = fileProvider;
    }

    /// <summary>
    /// Serves a hashed static asset from the embedded <c>assets/</c> directory, or responds
    /// <c>404 Not Found</c> when the file does not exist (a missing asset must not masquerade as the
    /// SPA document).
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="path">The requested asset sub-path under <c>assets/</c>.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    public Task ServeAssetAsync(HttpContext context, string? path)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrEmpty(path) && !path.EndsWith('/'))
        {
            var file = _fileProvider.GetFileInfo($"assets/{path}");
            if (file.Exists && !file.IsDirectory)
            {
                return WriteFileAsync(context, file, DashboardSpaContentTypes.ForPath(path), immutable: true);
            }
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    /// <summary>Serves the SPA entry document (<c>index.html</c>).</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    public Task ServeIndexAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var index = _fileProvider.GetFileInfo(IndexPath);
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return WriteFileAsync(context, index, "text/html; charset=utf-8", immutable: false);
    }

    private static async Task WriteFileAsync(HttpContext context, IFileInfo file, string contentType, bool immutable)
    {
        var headers = context.Response.Headers;
        headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers[HeaderNames.CacheControl] = immutable
            ? "public, max-age=31536000, immutable"
            : "no-cache";

        context.Response.ContentType = contentType;
        context.Response.ContentLength = file.Length;

        await using var stream = file.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }
}
