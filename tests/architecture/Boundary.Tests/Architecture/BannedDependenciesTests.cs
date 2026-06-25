using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Boundary.Tests;

using Shouldly;

using Xunit;

namespace Boundary.Tests.Architecture;

[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class BannedDependenciesTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

    // Env flag to flip from report-only to enforcing. Default: report-only.
    private static readonly bool Enforce = string.Equals(
        Environment.GetEnvironmentVariable("ARCH_ENFORCE"),
        "true", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Abstractions_Should_Not_Reference_Forbidden_Frameworks_Or_Providers()
    {
        // Banned dependencies common to BOTH abstraction packages.
        var bannedCommon = new[]
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
            PackageReference Include="CloudNative.CloudEvents.SystemTextJson"
            """,
            """
            PackageReference Include="System.Text.Json"
            """,
        };

        // Dapper is BANNED in Excalibur.Dispatch.Abstractions (the messaging abstraction must stay
        // driver-free), but is INTENTIONAL in Excalibur.Data.Abstractions: IDataRequest is built on
        // Dapper to enhance it (user-confirmed; CLAUDE.md "Technical Decisions — Dapper ... IDataRequest
        // is built on Dapper to enhance it. NOT a compliance violation. Do not remove."). So the Dapper
        // ban is scoped to the Dispatch side only, not applied to the Data.Abstractions scan.
        const string bannedDapper = """
            PackageReference Include="Dapper"
            """;

        var scans = new[]
        {
            (file: Path.Combine(RepoRoot, "src", "Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Dispatch.Abstractions.csproj"),
                banned: bannedCommon.Append(bannedDapper).ToArray()),
            (file: Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "Excalibur.Data.Abstractions.csproj"),
                banned: bannedCommon),
        };

        var violations = new List<string>();
        foreach (var (file, banned) in scans.Where(s => File.Exists(s.file)))
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
            true.ShouldBeTrue();
            return;
        }

        var message = "Abstractions banned dependency violations (report-only):\n" + string.Join("\n", violations);
        if (Enforce)
        {
            false.ShouldBeTrue(message);
        }
        else
        {
            // Report-only: attach as output but do not fail the test.
            Console.WriteLine(message);
            true.ShouldBeTrue();
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

        // NOTE: CloudNative.CloudEvents.SystemTextJson is NOT banned in Core. CloudEvents is a
        // first-class core *messaging* feature (CloudEventMiddleware, CloudEventsPipelineExtensions,
        // CloudEventJsonContext (AOT source-gen), CloudEventOptions, ... in src/Dispatch/Excalibur.Dispatch/
        // CloudEvents/) — a CNCF messaging wire-format that is squarely Dispatch's domain (HOW events
        // flow), AOT-handled, and consistent with ADR-295 (JSON-first; STJ is the core default). It is
        // NOT a provider SDK and NOT an opt-in payload serializer. The pre-ADR-295 ban was stale.
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
            true.ShouldBeTrue();
            return;
        }

        var message = "Core banned dependency violations (report-only):\n" + string.Join("\n", violations);
        if (Enforce)
        {
            false.ShouldBeTrue(message);
        }
        else
        {
            Console.WriteLine(message);
            true.ShouldBeTrue();
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

    // Dapper is intentionally permitted in Excalibur.Data.Abstractions (IDataRequest is built on Dapper
    // to enhance it — user-confirmed; CLAUDE.md "Technical Decisions"). Only the concrete provider driver
    // System.Data.SqlClient is banned here (a data-access abstraction must not bind a concrete SQL driver).
    [Fact]
    public void Excalibur_Abstractions_Should_Not_Reference_SystemDataSqlClient()
    {
        var excaliburAbstractions = new[]
        {
            Path.Combine(RepoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions", "Excalibur.Data.Abstractions.csproj"),
        };

        var banned = new[]
        {
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

        // Also scan a few source files for direct 'using' hints to help triage (report-only).
        // 'using Dapper;' is intentionally NOT flagged — Dapper is permitted in Data.Abstractions.
        var hints = new[] { "using System.Data.SqlClient;" };
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
            true.ShouldBeTrue();
            return;
        }

        var message = "Excalibur.Abstractions banned dependencies (report-only):\n" + string.Join("\n", violations);
        if (Enforce)
        {
            false.ShouldBeTrue(message);
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
            true.ShouldBeTrue();
        }
    }

    // Dapper usings are intentionally permitted in Excalibur.Data.Abstractions (IDataRequest is built on
    // Dapper to enhance it — user-confirmed; CLAUDE.md "Technical Decisions"). Only a concrete SQL driver
    // (System.Data.SqlClient) using is banned in the data-access abstraction source.
    [Fact]
    public void Excalibur_Abstractions_Source_Should_Not_Use_SystemDataSqlClient()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var dir = Path.Combine(repoRoot, "src", "Excalibur", "Excalibur.Data.Abstractions");
        if (!Directory.Exists(dir))
        {
            true.ShouldBeTrue();
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
                if (l.Contains("using System.Data.SqlClient;", StringComparison.Ordinal))
                {
                    hits.Add($"{file}:{i + 1}:{l.Trim()}");
                }
            }
        }

        if (hits.Count == 0)
        {
            true.ShouldBeTrue();
            return;
        }

        var message = "Excalibur.Data.Abstractions source banned usings (report-only):\n" + string.Join("\n", hits);
        if (Enforce)
        {
            false.ShouldBeTrue(message);
        }
        else
        {
            Console.WriteLine(message);
            true.ShouldBeTrue();
        }
    }

    // REMOVED (Sprint 848, A2 / 87h7t8): the former Patterns_Should_Not_Use_STJ_Namespace_In_Core test
    // banned 'using System.Text.Json;' in Excalibur.Dispatch.Patterns. That ban is stale — ADR-295
    // ("JSON-First Serialization", Accepted S738) makes System.Text.Json the JSON-first DEFAULT in core
    // (binary serializers opt-in; STJ has first-class AOT support). Patterns/ClaimCheck/
    // JsonClaimCheckSerializer.cs is exactly that pattern (a JSON serializer using STJ, AOT-annotated),
    // so banning STJ in Patterns contradicts the ratified policy. Removed per SoftwareArchitect ruling.
}
