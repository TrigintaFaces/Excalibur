namespace Excalibur.Operations.Dashboard.Spa;

/// <summary>
/// Minimal, allocation-free content-type lookup for the fixed set of asset
/// extensions the embedded SPA emits. Avoids a mutable
/// <c>FileExtensionContentTypeProvider</c> so the serving path stays trim- and
/// AOT-clean.
/// </summary>
internal static class DashboardSpaContentTypes
{
    private const string Fallback = "application/octet-stream";

    /// <summary>Resolves a content type from a file path's extension.</summary>
    /// <param name="path">The asset path or file name.</param>
    /// <returns>The mapped MIME type, or <c>application/octet-stream</c> when unknown.</returns>
    public static string ForPath(string path)
    {
        var lastDot = path.LastIndexOf('.');
        if (lastDot < 0)
        {
            return Fallback;
        }

        var ext = path[lastDot..];
        return ext.ToUpperInvariant() switch
        {
            ".HTML" => "text/html; charset=utf-8",
            ".JS" or ".MJS" => "text/javascript; charset=utf-8",
            ".CSS" => "text/css; charset=utf-8",
            ".JSON" => "application/json; charset=utf-8",
            ".WEBMANIFEST" => "application/manifest+json; charset=utf-8",
            ".SVG" => "image/svg+xml",
            ".PNG" => "image/png",
            ".ICO" => "image/x-icon",
            ".WOFF2" => "font/woff2",
            ".WOFF" => "font/woff",
            ".MAP" => "application/json; charset=utf-8",
            ".TXT" => "text/plain; charset=utf-8",
            _ => Fallback,
        };
    }
}
