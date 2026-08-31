using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// The reserved untenanted tenant term has one declaration, and that declaration is deliberately a field
/// rather than a constant. This guard refuses a second one declared as <c>const</c>.
/// </summary>
/// <remarks>
/// <para>
/// A public constant is inlined into every consuming assembly at compile time, so a consumer that
/// referenced one would keep the old literal until it recompiled — leaving its rows in a partition the
/// framework no longer queries, with no error to signal it. That is why the canonical declaration is a
/// <c>static readonly</c> field. A local <c>const</c> holding the same literal reintroduces exactly that
/// hazard inside the package that declares it, and it did: one provider re-typed the value as a private
/// constant purely because a constant cannot be initialised from a field.
/// </para>
/// <para>
/// <b>What this guard tests, and what it does not.</b> It tests the CONSTANT form specifically, because
/// that form is unambiguous in source and is the one that inlines. It does NOT test that the literal is
/// never re-typed at all: the term appears legitimately in prose comments, in migration scripts that
/// cannot reference a C# field, and in test fixtures that assert the value independently of the
/// declaration they are checking. Those re-typings are covered by the per-provider totality locks, which
/// assert the value a real database actually holds.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> restore any of the removed <c>private const string … = "__untenanted__";</c>
/// declarations under <c>src/</c> — the safety arm goes RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed partial class UntenantedSentinelIsNeverAConstShould
{
    private readonly string _repoRoot = TestHelpers.GetRepositoryRoot();

    [Fact]
    public void FindNoConstantHoldingTheSentinelUnderSrc()
    {
        var offenders = ConstantSentinelDeclarations(Path.Combine(_repoRoot, "src")).ToList();

        offenders.ShouldBeEmpty(
            "The untenanted tenant term must be read from its single canonical field at run time, never "
            + "inlined from a constant. Declare a 'private static readonly string' initialised from "
            + "TenantScope.UntenantedSentinel instead. Offending declarations: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void StillDetectAConstantHoldingTheSentinel()
    {
        // Liveness's counterpart. The safety arm above is satisfied by a scanner that matches nothing at
        // all — a mistyped pattern, a directory that no longer exists, a file filter that excludes every
        // candidate. This arm proves the same scanner reports a planted declaration, so a green above
        // means the tree is clean rather than that the query is blind.
        var probe = Path.Combine(Path.GetTempPath(), "sentinel-const-probe-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(probe);

        try
        {
            File.WriteAllText(
                Path.Combine(probe, "ProbeSentinel.cs"),
                "namespace Probe;\n\ninternal static class ProbeSentinel\n{\n\tprivate const string Untenanted = \"__untenanted__\";\n}\n");

            ConstantSentinelDeclarations(probe).ShouldNotBeEmpty();
        }
        finally
        {
            Directory.Delete(probe, recursive: true);
        }
    }

    private static IEnumerable<string> ConstantSentinelDeclarations(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var normalised = file.Replace('\\', '/');

            if (normalised.Contains("/bin/", StringComparison.Ordinal)
                || normalised.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            var lineNumber = 0;

            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;

                if (!line.Contains("const", StringComparison.Ordinal))
                {
                    continue;
                }

                // The declaration and its initialiser on one line is the only shape a constant can take:
                // a constant initialiser must be a compile-time constant expression, so it cannot be
                // assigned anywhere else.
                if (SentinelConstant().IsMatch(line))
                {
                    yield return $"{normalised}:{lineNumber}";
                }
            }
        }
    }

    [GeneratedRegex(@"\bconst\s+string\s+\w+\s*=\s*@?""__untenanted__""")]
    private static partial Regex SentinelConstant();
}
