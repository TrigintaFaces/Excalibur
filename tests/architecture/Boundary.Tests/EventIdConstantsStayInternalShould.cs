namespace Boundary.Tests;

/// <summary>
/// Logging event-id constants are plumbing, and plumbing does not belong in a frozen public API.
/// </summary>
/// <remarks>
/// <para>
/// Before this guard, the event-id constant classes accounted for roughly a tenth of the shipped public
/// API baselines — thousands of <c>const int</c> entries whose only purpose is to be handed to
/// <c>ILogger</c>. Nothing outside the framework referenced them: the published documentation quotes the
/// NUMBERS a consumer sees in their logs, which is the right way to document an event id without
/// exporting the constant that carries it.
/// </para>
/// <para>
/// The freeze is what makes this a guard rather than a cleanup. Once the public API is frozen, a
/// constant that slipped in as <c>public</c> is reserved for a new major line, so the moment to catch it
/// is while it is being written. Sibling packages that emit the same ids reach them through
/// <c>InternalsVisibleTo</c>, which keeps them out of the consumer contract without duplicating a range.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class EventIdConstantsStayInternalShould
{
    private readonly string _repoRoot = TestHelpers.GetRepositoryRoot();

    [Fact]
    public void FindNoPublicEventIdClassUnderSrc()
    {
        var offenders = PublicEventIdDeclarations(Path.Combine(_repoRoot, "src")).ToList();

        offenders.ShouldBeEmpty(
            "Event-id constants are logging plumbing, not consumer contract, and the public API baselines "
            + "are frozen at the release candidate. Declare the class 'internal static' and grant any "
            + "sibling package that emits the same ids access with InternalsVisibleTo. Offending files: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void StillDetectAPublicEventIdClass()
    {
        // Liveness's counterpart. The safety arm above is satisfied by a scanner that matches nothing at
        // all — a mistyped pattern, a directory that no longer exists, a file filter that excludes every
        // candidate. This arm proves the same scanner reports a planted declaration, so a green above
        // means the tree is clean rather than that the query is blind.
        var probe = Path.Combine(Path.GetTempPath(), "eventid-guard-probe-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(probe);

        try
        {
            File.WriteAllText(
                Path.Combine(probe, "ProbeEventId.cs"),
                "namespace Probe;\n\npublic static class ProbeEventId\n{\n\tpublic const int Thing = 1;\n}\n");

            PublicEventIdDeclarations(probe).ShouldNotBeEmpty();
        }
        finally
        {
            Directory.Delete(probe, recursive: true);
        }
    }

    private static IEnumerable<string> PublicEventIdDeclarations(string root)
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

            foreach (var line in File.ReadLines(file))
            {
                // Anchored on the declaration itself: a doc comment or a string mentioning the type name
                // is not a declaration, and counting one would make the guard fire on prose.
                if (line.StartsWith("public static class ", StringComparison.Ordinal)
                    && line.TrimEnd().EndsWith("EventId", StringComparison.Ordinal))
                {
                    yield return Path.GetRelativePath(root, file).Replace('\\', '/');
                    break;
                }
            }
        }
    }
}
