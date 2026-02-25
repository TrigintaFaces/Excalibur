namespace Boundary.Tests;

/// <summary>
/// Shared helpers for locating repository root and parsing project files.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Walks up from the test assembly output directory to find the repository root
    /// (the directory containing Excalibur.sln).
    /// </summary>
    public static string GetRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find repository root (Excalibur.sln). " +
            "Ensure tests run from within the repository tree.");
    }

    /// <summary>
    /// Returns all .csproj files under a given subdirectory relative to the repo root.
    /// </summary>
    public static IReadOnlyList<string> GetCsprojFiles(string repoRoot, string relativeDir)
    {
        var fullPath = Path.Combine(repoRoot, relativeDir);

        if (!Directory.Exists(fullPath))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(fullPath, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedArtifactPath(path))
            .ToList();
    }

    private static bool IsGeneratedArtifactPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/BenchmarkDotNet.Artifacts", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a .csproj file and returns all ProjectReference Include paths.
    /// </summary>
    public static IReadOnlyList<string> GetProjectReferences(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);

        return doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    /// <summary>
    /// Extracts the project name from a ProjectReference path.
    /// e.g., "..\..\Excalibur.Domain\Excalibur.Domain.csproj" -> "Excalibur.Domain"
    /// </summary>
    public static string ExtractProjectName(string projectReferencePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectReferencePath);
        return fileName;
    }
}
