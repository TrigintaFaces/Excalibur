using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ArchitectureTests.Architecture;

public sealed class BannedDependenciesTests
{
    private static readonly string RepoRoot = GetRepoRoot();

    // Env flag to flip from report-only to enforcing. Default: report-only.
    private static readonly bool Enforce = string.Equals(
        Environment.GetEnvironmentVariable("ARCH_ENFORCE"),
        "true", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Abstractions_Should_Not_Reference_Forbidden_Frameworks_Or_Providers()
    {
        var abstractions = new[]
        {
            Path.Combine(RepoRoot, "src", "Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Dispatch.Abstractions.csproj"),
            Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "Excalibur.Data.Abstractions.csproj"),
        };

        var banned = new[]
        {
            """
            FrameworkReference Include="Microsoft.AspNetCore.App"
            """,
            """
            PackageReference Include="Microsoft.Data.SqlClient"
            """,
            """
            PackageReference Include="Npgsql"
            """,
            """
            PackageReference Include="Dapper"
            """,
            """
            PackageReference Include="CloudNative.CloudEvents.SystemTextJson"
            """,
            """
            PackageReference Include="System.Text.Json"
            """,
        };

        var violations = new List<string>();
        foreach (var file in abstractions.Where(File.Exists))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (banned.Any(b => lines[i].Contains(b, StringComparison.Ordinal)))
                {
                    violations.Add($"{file}:{i + 1}:{lines[i].Trim()}");
                }
            }
        }

        if (violations.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var message = "Abstractions banned dependency violations (report-only):\n" + string.Join("\n", violations);
        if (Enforce)
        {
            Assert.False(true, message);
        }
        else
        {
            // Report-only: attach as output but do not fail the test.
            Console.WriteLine(message);
            Assert.True(true);
        }
    }

    [Fact]
    public void Core_Should_Not_Reference_Forbidden_Frameworks_Providers_Or_Json_Providers()
    {
        var coreCandidates = Directory.GetFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(p =>
                p.EndsWith(Path.Combine("src", "Dispatch", "Excalibur.Dispatch", "Excalibur.Dispatch.csproj"), StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(Path.Combine("src", "Dispatch", "Excalibur.Dispatch.Patterns", "Excalibur.Dispatch.Patterns.csproj"), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var banned = new[]
        {
            """
            FrameworkReference Include="Microsoft.AspNetCore.App"
            """,
            """
            PackageReference Include="Microsoft.Data.SqlClient"
            """,
            """
            PackageReference Include="Npgsql"
            """,
            """
            PackageReference Include="Dapper"
            """,
            """
            PackageReference Include="CloudNative.CloudEvents.SystemTextJson"
            """,
        };

        var violations = new List<string>();
        var policyDrift = new List<string>();
        foreach (var file in coreCandidates.Where(File.Exists))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (banned.Any(b => lines[i].Contains(b, StringComparison.Ordinal)))
                {
                    violations.Add($"{file}:{i + 1}:{lines[i].Trim()}");
                }
                // Report-only: R0.14 recommends MemoryPack for core; flag MessagePack as drift (non-failing)
                if (lines[i].Contains("""
					PackageReference Include="MessagePack"
					""", StringComparison.Ordinal))
                {
                    policyDrift.Add($"{file}:{i + 1}:{lines[i].Trim()}");
                }
            }
        }

        if (violations.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var message = "Core banned dependency violations (report-only):\n" + string.Join("\n", violations);
        if (Enforce)
        {
            Assert.False(true, message);
        }
        else
        {
            Console.WriteLine(message);
            Assert.True(true);
        }

        if (policyDrift.Count > 0)
        {
            Console.WriteLine("Core serialization policy drift (R0.14 advisory, report-only):");
            foreach (var d in policyDrift)
            {
                Console.WriteLine(d);
            }
        }
    }

    [Fact]
    public void Excalibur_Abstractions_Should_Not_Reference_Dapper_Or_SystemDataSqlClient()
    {
        var excaliburAbstractions = new[]
        {
            Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "Excalibur.Data.Abstractions.csproj"),
        };

        var banned = new[]
        {
            """
            PackageReference Include="Dapper"
            """,
            """
            PackageReference Include="System.Data.SqlClient"
            """,
        };

        var violations = new List<string>();
        var usages = new List<string>();

        foreach (var file in excaliburAbstractions.Where(File.Exists))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (banned.Any(b => lines[i].Contains(b, StringComparison.Ordinal)))
                {
                    violations.Add($"{file}:{i + 1}:{lines[i].Trim()}");
                }
            }
        }

        // Also scan a few source files for direct 'using' hints to help triage (report-only)
        var hints = new[] { "using Dapper;", "using System.Data.SqlClient;" };
        var hintFiles = new[]
        {
            Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "DataRequestBase.cs"),
            Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "IDataRequest.cs"),
            Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "UnitOfWork.cs"),
        };
        foreach (var f in hintFiles.Where(File.Exists))
        {
            var lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
            {
                if (hints.Any(h => lines[i].Contains(h, StringComparison.Ordinal)))
                {
                    usages.Add($"{f}:{i + 1}:{lines[i].Trim()}");
                }
            }
        }

        if (violations.Count == 0)
        {
            // Report-only output for source usage hints, if any
            if (usages.Count > 0)
            {
                Console.WriteLine("Excalibur.Abstractions source usage hints (report-only):");
                foreach (var u in usages)
                {
                    Console.WriteLine(u);
                }
            }
            Assert.True(true);
            return;
        }

        var message = "Excalibur.Abstractions banned dependencies (report-only):\n" + string.Join("\n", violations);
        if (Enforce)
        {
            Assert.False(true, message);
        }
        else
        {
            Console.WriteLine(message);
            if (usages.Count > 0)
            {
                Console.WriteLine("Excalibur.Abstractions source usage hints (report-only):");
                foreach (var u in usages)
                {
                    Console.WriteLine(u);
                }
            }
            Assert.True(true);
        }
    }

    [Fact]
    public void Excalibur_Abstractions_Source_Should_Not_Use_Dapper_Or_SystemDataSqlClient()
    {
        var repoRoot = GetRepoRoot();
        var dir = Path.Combine(repoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions");
        if (!Directory.Exists(dir))
        {
            Assert.True(true);
            return;
        }

        var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        var hits = new List<string>();
        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (l.Contains("using Dapper;", StringComparison.Ordinal) || l.Contains("using System.Data.SqlClient;", StringComparison.Ordinal))
                {
                    hits.Add($"{file}:{i + 1}:{l.Trim()}");
                }
            }
        }

        if (hits.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var message = "Excalibur.Data.Abstractions source banned usings (report-only):\n" + string.Join("\n", hits);
        if (Enforce)
        {
            Assert.False(true, message);
        }
        else
        {
            Console.WriteLine(message);
            Assert.True(true);
        }
    }

    [Fact]
    public void Patterns_Should_Not_Use_STJ_Namespace_In_Core()
    {
        var repoRoot = GetRepoRoot();
        var patternsDir = Path.Combine(repoRoot, "src", "Dispatch", "Excalibur.Dispatch.Patterns");
        if (!Directory.Exists(patternsDir))
        {
            Assert.True(true);
            return;
        }

        var csFiles = Directory.GetFiles(patternsDir, "*.cs", SearchOption.AllDirectories);
        var hits = new List<string>();
        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("using System.Text.Json;", StringComparison.Ordinal))
                {
                    hits.Add($"{file}:{i + 1}:{lines[i].Trim()}");
                }
            }
        }

        if (hits.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var message = "Excalibur.Dispatch.Patterns source using System.Text.Json (report-only):\n" + string.Join("\n", hits);
        if (Enforce)
        {
            Assert.False(true, message);
        }
        else
        {
            Console.WriteLine(message);
            Assert.True(true);
        }
    }

    private static string GetRepoRoot()
    {
        // Walk up from current directory until we find the solution file as a heuristic.
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }

        // Fallback to CWD if not found.
        return Directory.GetCurrentDirectory();
    }
}
