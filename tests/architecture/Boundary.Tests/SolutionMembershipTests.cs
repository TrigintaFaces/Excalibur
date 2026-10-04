using System.Text.Json;
using System.Text.RegularExpressions;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Boundary.Tests;

/// <summary>Independent architecture assertions over the repository's exact project inventory.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Governance")]
public sealed class SolutionMembershipTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

    [Fact]
    public void AllShippingProjects_MustBeInSolution() => AssertRootMembership("src");

    [Fact]
    public void AllTestProjects_MustBeInSolution() => AssertRootMembership("tests");

    [Fact]
    public void AllBenchmarkProjects_MustBeInSolution() => AssertRootMembership("benchmarks");

    [Fact]
    public void SolutionManifestConfigurationsAndFilters_MustMatchDisk() =>
        SolutionInventory.Validate(RepoRoot);

    public static TheoryData<string> GovernanceCases()
    {
        var cases = new TheoryData<string>();
        using var fixture = ReadFixture();
        foreach (var entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            cases.Add(entry.GetProperty("name").GetString()!);
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(GovernanceCases))]
    public void GovernancePredicates_DetectDeliberateMutations(string name)
    {
        using var fixture = ReadFixture();
        var entry = fixture.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);
        var files = fixture.RootElement.GetProperty("files").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
        if (entry.TryGetProperty("edits", out var edits))
        {
            foreach (var edit in edits.EnumerateArray())
            {
                var path = edit.GetProperty("path").GetString()!;
                var before = edit.GetProperty("before").GetString()!;
                files[path].ShouldContain(before);
                files[path] = files[path].Replace(before, edit.GetProperty("after").GetString()!, StringComparison.Ordinal);
            }
        }
        if (entry.TryGetProperty("delete", out var deleted))
        {
            foreach (var path in deleted.EnumerateArray()) { files.Remove(path.GetString()!); }
        }
        if (entry.TryGetProperty("add", out var added))
        {
            foreach (var file in added.EnumerateObject()) { files[file.Name] = file.Value.GetString()!; }
        }

        var root = Path.Combine(Path.GetTempPath(), "solution-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var file in files)
            {
                var path = Path.Combine(root, SolutionInventory.CanonicalPath(file.Key));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, file.Value);
            }
            if (entry.GetProperty("pass").GetBoolean())
            {
                SolutionInventory.Validate(root);
            }
            else
            {
                var error = Should.Throw<Exception>(() => SolutionInventory.Validate(root));
                Regex.IsMatch(error.Message, entry.GetProperty("error").GetString()!, RegexOptions.IgnoreCase)
                    .ShouldBeTrue($"Wrong rejection for {name}: {error.Message}");
            }
        }
        finally
        {
            // This exact random directory is created above; no repository directories are mutated.
            Directory.Delete(root, recursive: true);
        }
    }

    private static JsonDocument ReadFixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(RepoRoot, "tests/architecture/Boundary.Tests/Fixtures/SolutionGovernance.json")));

    private static void AssertRootMembership(string root)
    {
        var inventory = SolutionInventory.ReadSolution(RepoRoot);
        var projects = SolutionInventory.EnumerateProjects(RepoRoot, root).ToArray();
        projects.ShouldNotBeEmpty();
        foreach (var path in projects)
        {
            inventory.Projects.ContainsKey(path).ShouldBeTrue($"Exact project path missing from solution: {path}");
        }
    }
}

/// <summary>
/// Test-side parser independent of the PowerShell governance gate. Shared fixtures exercise both
/// implementations, including legitimate duplicate display names and x64-to-Any-CPU mappings.
/// </summary>
internal sealed class SolutionInventory
{
    private static readonly string[] Roots = ["src", "tests", "samples", "benchmarks", "load-tests"];
    private static readonly string[] ConfigurationNames = ["Debug", "Release"];
    private static readonly string[] PlatformNames = ["Any CPU", "x64", "x86"];
    private const string TemplatePackage = "templates/Excalibur.Dispatch.Templates.csproj";
    internal Dictionary<string, string> Projects { get; } = new(StringComparer.Ordinal);
    private HashSet<string> Configurations { get; } = new(StringComparer.Ordinal);
    private Dictionary<string, string> Mappings { get; } = new(StringComparer.Ordinal);
    private HashSet<string> Items { get; } = new(StringComparer.Ordinal);
    private Dictionary<string, string> Names { get; } = new(StringComparer.Ordinal);
    private Dictionary<string, string> Parents { get; } = new(StringComparer.Ordinal);
    private HashSet<string> Folders { get; } = new(StringComparer.Ordinal);

    internal static string CanonicalPath(string path)
    {
        path = path.Replace('\\', '/');
        Require(!string.IsNullOrWhiteSpace(path) && !path.StartsWith('/') && !path.Contains(':') &&
            path.Split('/').All(segment => segment is not ("" or "." or "..")), $"Non-canonical repository path: {path}");
        return path;
    }

    internal static IEnumerable<string> EnumerateProjects(string root, string directory)
    {
        var fullDirectory = Path.Combine(root, directory);
        if (!Directory.Exists(fullDirectory)) { return []; }
        return Directory.EnumerateFiles(fullDirectory, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Split('/').Any(segment => segment.StartsWith('.') ||
                segment is "bin" or "obj" or "node_modules" or "labs" or "tools" or "BenchmarkDotNet.Artifacts"));
    }

    internal static SolutionInventory ReadSolution(string root)
    {
        var lines = File.ReadAllLines(Path.Combine(root, "Excalibur.sln"));
        ValidateSolutionStructure(lines);
        var result = new SolutionInventory();
        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        var folder = false;
        foreach (var line in lines.Select(line => line.Trim()))
        {
            if (line.StartsWith("Project(", StringComparison.Ordinal))
            {
                var project = Regex.Match(line, "^Project\\(\"(?<type>\\{[^}]+\\})\"\\) = \"(?<name>[^\"]+)\", \"(?<path>[^\"]+)\", \"(?<id>\\{[^}]+\\})\"$");
                Require(project.Success, $"Malformed solution project: {line}");
                var id = project.Groups["id"].Value.ToUpperInvariant();
                Require(Guid.TryParse(id, out _) && guids.Add(id), $"Invalid or duplicate GUID: {id}");
                folder = project.Groups["type"].Value.Equals("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", StringComparison.OrdinalIgnoreCase);
                result.Names.Add(id, folder ? project.Groups["name"].Value :
                    Path.GetFileNameWithoutExtension(project.Groups["path"].Value.Replace('\\', '/')));
                if (folder) { result.Folders.Add(id); }
                if (!folder)
                {
                    var path = CanonicalPath(project.Groups["path"].Value);
                    Require(path.EndsWith(".csproj", StringComparison.Ordinal) && result.Projects.TryAdd(path, id),
                        $"Unsupported or duplicate solution path: {path}");
                }
                continue;
            }
            var trimmed = line.Trim();
            if (trimmed.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.Ordinal)) { section = "configurations"; continue; }
            if (trimmed.StartsWith("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.Ordinal)) { section = "mappings"; continue; }
            if (trimmed.StartsWith("GlobalSection(NestedProjects)", StringComparison.Ordinal)) { section = "parents"; continue; }
            if (trimmed.StartsWith("ProjectSection(SolutionItems)", StringComparison.Ordinal))
            {
                Require(folder, "SolutionItems must belong to a solution folder");
                section = "items"; continue;
            }
            if (trimmed is "EndGlobalSection" or "EndProjectSection") { section = string.Empty; continue; }
            if (section.Length == 0) { continue; }
            var parts = trimmed.Split(" = ", 2, StringSplitOptions.None);
            Require(parts.Length == 2, $"Invalid solution section entry: {line}");
            if (section == "configurations")
            {
                Require(parts[0] == parts[1] && result.Configurations.Add(parts[0]), $"Invalid or duplicate solution configuration: {line}");
            }
            else if (section == "mappings")
            {
                var mapping = Regex.Match(parts[0], "^(?<id>\\{[^}]+\\})(?<suffix>\\..+\\.(?:ActiveCfg|Build\\.0))$");
                Require(mapping.Success, $"Invalid project mapping: {line}");
                Require(result.Mappings.TryAdd(mapping.Groups["id"].Value.ToUpperInvariant() + mapping.Groups["suffix"].Value, parts[1]),
                    $"Duplicate project mapping: {line}");
            }
            else if (section == "parents")
            {
                Require(result.Parents.TryAdd(parts[0].ToUpperInvariant(), parts[1].ToUpperInvariant()), "Duplicate solution nesting");
            }
            else
            {
                var path = CanonicalPath(parts[0]);
                Require(path == CanonicalPath(parts[1]), $"Solution item mismatch: {line}");
                if (path.EndsWith(".csproj", StringComparison.Ordinal))
                {
                    Require(result.Items.Add(path), $"Duplicate project solution item: {path}");
                }
            }
        }
        foreach (var parent in result.Parents)
        {
            Require(guids.Contains(parent.Key) && result.Folders.Contains(parent.Value), "Invalid solution folder reference");
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var node = parent.Key;
            while (result.Parents.TryGetValue(node, out var next))
            {
                Require(visited.Add(node), "Cyclic solution folder nesting");
                node = next;
            }
        }
        var scopedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in result.Names)
        {
            var parent = result.Parents.GetValueOrDefault(name.Key, "<root>");
            var kind = result.Folders.Contains(name.Key) ? "folder/" : "project/";
            Require(scopedNames.Add(kind + parent + "/" + name.Value), $"Duplicate solution display name in one folder: {name.Value}");
        }
        return result;
    }

    internal static HashSet<string> ReadFilter(string root, string filter, IReadOnlyDictionary<string, string> projects)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(filter));
        Require(json.RootElement.ValueKind == JsonValueKind.Object &&
            json.RootElement.TryGetProperty("solution", out var definition) && definition.ValueKind == JsonValueKind.Object,
            $"Invalid filter shape: {filter}");
        var solution = json.RootElement.GetProperty("solution");
        Require(solution.TryGetProperty("path", out var target) && target.ValueKind == JsonValueKind.String &&
            solution.TryGetProperty("projects", out var projectsNode) && projectsNode.ValueKind == JsonValueKind.Array &&
            projectsNode.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())),
            $"Invalid filter shape: {filter}");
        var solutionPath = solution.GetProperty("path").GetString()!.Replace('\\', Path.DirectorySeparatorChar);
        Require(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filter)!, solutionPath)) ==
            Path.Combine(root, "Excalibur.sln"), $"Filter references another solution: {filter}");
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in solution.GetProperty("projects").EnumerateArray())
        {
            var path = CanonicalPath(item.GetString()!);
            Require(members.Add(path) && projects.ContainsKey(path), $"Duplicate or missing solution project in filter: {filter}: {path}");
        }
        Require(members.Count > 0, $"Empty solution filter: {filter}");
        return members;
    }

    private static void ValidateSolutionStructure(string[] lines)
    {
        var header = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        Require(header >= 0 && lines[header] == "Microsoft Visual Studio Solution File, Format Version 12.00", "Invalid solution structure: header");
        var block = string.Empty;
        var section = string.Empty;
        var globalSeen = false;
        var globalClosed = false;
        var globalSections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(header + 1).Select(line => line.Trim()))
        {
            if (line.Length == 0 || line.StartsWith('#')) { continue; }
            if (line.StartsWith("Project(", StringComparison.Ordinal))
            {
                var match = Regex.Match(line, "^Project\\(\"(\\{[^}]+\\})\"\\)");
                Require(block.Length == 0 && !globalSeen && match.Success, "Invalid solution structure: Project");
                Require(match.Groups[1].Value.ToUpperInvariant() is "{2150E333-8FDC-42A3-9474-1A3956D46DE8}" or
                    "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}" or "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}", "Invalid solution structure: project type GUID");
                block = "Project"; continue;
            }
            if (line == "Global")
            {
                Require(block.Length == 0 && !globalSeen, "Invalid solution structure: Global");
                block = "Global"; globalSeen = true; continue;
            }
            if (line is "EndProject" or "EndGlobal")
            {
                Require(section.Length == 0 && line == "End" + block, "Invalid solution structure: block terminator");
                if (block == "Global") { globalClosed = true; }
                block = string.Empty; continue;
            }
            var sectionStart = Regex.Match(line, "^(Project|Global)Section\\(([^)]+)\\) = (preProject|postProject|preSolution|postSolution)$");
            if (sectionStart.Success)
            {
                Require(section.Length == 0 && block == sectionStart.Groups[1].Value, "Invalid solution structure: section");
                section = block;
                Require(section != "Global" || globalSections.Add(sectionStart.Groups[2].Value), "Invalid solution structure: duplicate global section");
                continue;
            }
            if (line is "EndProjectSection" or "EndGlobalSection")
            {
                Require(section.Length > 0 && line == "End" + section + "Section", "Invalid solution structure: section terminator");
                section = string.Empty; continue;
            }
            Require(section.Length > 0 || (block.Length == 0 && !globalSeen && Regex.IsMatch(line,
                "^(VisualStudioVersion|MinimumVisualStudioVersion) = [0-9.]+$")), $"Invalid solution structure: {line}");
        }
        Require(block.Length == 0 && section.Length == 0 && globalClosed, "Invalid solution structure: missing terminator");
    }

    internal static void Validate(string root)
    {
        var disk = Roots.SelectMany(directory => EnumerateProjects(root, directory)).ToHashSet(StringComparer.Ordinal);
        Require(File.Exists(Path.Combine(root, TemplatePackage)), "Missing template package project");
        disk.Add(TemplatePackage);
        ValidateManifest(root, disk);
        var solution = ReadSolution(root);
        EqualSets(disk, solution.Projects.Keys, "Solution membership");

        var configurations = from configuration in ConfigurationNames
                             from platform in PlatformNames
                             select configuration + "|" + platform;
        EqualSets(configurations, solution.Configurations, "Solution configurations");
        foreach (var project in solution.Projects)
        {
            foreach (var configuration in solution.Configurations)
            {
                var key = project.Value + "." + configuration;
                Require(solution.Mappings.TryGetValue(key + ".ActiveCfg", out var active) &&
                    solution.Mappings.TryGetValue(key + ".Build.0", out var build) && active == build,
                    $"Missing or mismatched ActiveCfg/Build.0: {project.Key} {configuration}");
                Require(active!.StartsWith(configuration.Split('|')[0] + "|", StringComparison.Ordinal) &&
                    active.Split('|').Length == 2 && active.Split('|')[1].Length > 0, $"Invalid build target: {key}");
            }
        }
        Require(solution.Mappings.Count == solution.Projects.Count * solution.Configurations.Count * 2, "Unexpected project configuration entries");
        EqualSets(EnumerateProjects(root, "templates").Where(path => path != TemplatePackage), solution.Items, "Template solution items");

        var filterRoot = Path.Combine(root, "eng/ci/shards");
        var filters = Directory.GetFiles(filterRoot, "*.slnf", SearchOption.AllDirectories);
        Require(filters.Length > 0, "Missing solution filters");
        var members = filters.ToDictionary(path => Path.GetRelativePath(filterRoot, path).Replace('\\', '/'),
            path => ReadFilter(root, path, solution.Projects), StringComparer.Ordinal);
        Require(members.ContainsKey("SamplesOnly.slnf"), "Missing SamplesOnly filter");
        EqualSets(disk.Where(path => path.StartsWith("samples/", StringComparison.Ordinal)), members["SamplesOnly.slnf"], "SamplesOnly membership");
    }

    private static void ValidateManifest(string root, HashSet<string> disk)
    {
        var stream = new YamlStream();
        using var reader = File.OpenText(Path.Combine(root, "eng/governance/project-manifest.yaml"));
        stream.Load(reader);
        Require(stream.Documents.Count == 1, "Expected one manifest document");
        var top = Mapping(stream.Documents[0].RootNode);
        Fields(top, ["version", "generated_at", "governance", "governed_directories", "exclusions", "projects"],
            ["version", "generated_at", "governance", "governed_directories", "exclusions", "projects"]);
        Require(Scalar(top["version"]) == "2.0", "Unsupported manifest version");
        _ = Scalar(top["generated_at"]);
        var governance = Mapping(top["governance"]);
        Fields(governance, ["solution_file"], ["solution_file"]);
        Require(Scalar(governance["solution_file"]) == "Excalibur.sln", "Wrong governed solution");
        var roots = Sequence(top["governed_directories"]).Select(Scalar).ToArray();
        EqualSets(Roots.Select(directory => directory + "/**"), roots, "Governed directories");
        Require(roots.Length == roots.Distinct(StringComparer.Ordinal).Count(), "Duplicate governed directories");
        var exclusions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Sequence(top["exclusions"]))
        {
            var exclusion = Mapping(node);
            Fields(exclusion, ["path", "reason"], ["path", "reason"]);
            _ = Scalar(exclusion["reason"]);
            var path = CanonicalPath(Scalar(exclusion["path"]));
            Require(exclusions.Add(path) && !Roots.Contains(path.Split('/')[0], StringComparer.Ordinal), "Duplicate or governed exclusion");
        }
        var manifest = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Sequence(top["projects"]))
        {
            var entry = Mapping(node);
            Fields(entry, ["path", "classification", "in_solution"],
                ["path", "classification", "in_solution", "framework_owner", "tier", "category", "variant", "notes", "reason"]);
            foreach (var field in entry.Values) { _ = Scalar(field); }
            var path = CanonicalPath(Scalar(entry["path"]));
            Require(manifest.Add(path), $"Duplicate manifest project: {path}");
            Require(entry["in_solution"] is YamlScalarNode { Style: ScalarStyle.Plain, Value: "true" }, $"Invalid in_solution: {path}");
            var classification = path == TemplatePackage ? "Template" :
                path.StartsWith("src/", StringComparison.Ordinal) ? "Shipping" :
                path.StartsWith("samples/", StringComparison.Ordinal) ? "Sample" :
                path.StartsWith("benchmarks/", StringComparison.Ordinal) || path.StartsWith("tests/benchmarks/", StringComparison.Ordinal) ? "Benchmark" : "Test";
            Require(Scalar(entry["classification"]) == classification, $"Incorrect classification: {path}");
        }
        EqualSets(disk, manifest, "Manifest membership");
    }

    private static Dictionary<string, YamlNode> Mapping(YamlNode node)
    {
        Require(node is YamlMappingNode, "Expected manifest mapping");
        return ((YamlMappingNode)node).Children.ToDictionary(pair => Scalar(pair.Key), pair => pair.Value, StringComparer.Ordinal);
    }

    private static IEnumerable<YamlNode> Sequence(YamlNode node)
    {
        Require(node is YamlSequenceNode, "Expected manifest sequence");
        return ((YamlSequenceNode)node).Children;
    }

    private static string Scalar(YamlNode node)
    {
        Require(node is YamlScalarNode { Value: not null } scalar && scalar.Tag.IsEmpty &&
            (scalar.Style == ScalarStyle.DoubleQuoted ||
                scalar.Style == ScalarStyle.Plain && Regex.IsMatch(scalar.Value, "^[A-Za-z0-9_./*\\\\-]+$")),
            "Expected untagged plain or double-quoted manifest scalar");
        return ((YamlScalarNode)node).Value!;
    }

    private static void Fields(Dictionary<string, YamlNode> map, string[] required, string[] allowed)
    {
        Require(required.All(map.ContainsKey) && map.Keys.All(key => allowed.Contains(key, StringComparer.Ordinal)),
            "Missing or unexpected manifest field");
    }

    private static void EqualSets(IEnumerable<string> expected, IEnumerable<string> actual, string name)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        Require(expectedSet.SetEquals(actualSet),
            $"{name}: missing [{string.Join(", ", expectedSet.Except(actualSet))}]; unexpected [{string.Join(", ", actualSet.Except(expectedSet))}]");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidDataException(message); }
    }
}
