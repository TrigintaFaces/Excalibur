// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

namespace Boundary.Tests;

/// <summary>
/// Holds the index of statements that deliberately carry no tenant term to its own terms: every entry names a
/// confinement kind and states a non-empty literal justification, and the index cannot quietly become empty.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class replaces an earlier version, and most of that version is deliberately gone.</b> It had five
/// arms over <c>DataRequestBase&lt;,&gt;.Tenancy</c>, a member every relational request overrode and nothing
/// read. That member has been removed along with the struct it carried, so four of the five arms lost their
/// subject rather than their value, and are not reconstructed here. What follows says which, and why each
/// death is intended rather than an omission.
/// </para>
/// <para>
/// <b>Gone: "every declaration constructs exactly one disposition."</b> The failure it caught was an override
/// left unassigned, whose value was the struct default and therefore byte-identical to a considered decision.
/// An attribute has no default — a type either carries it or does not — so the state that arm existed to catch
/// is no longer constructible. It was not fixed; it became unrepresentable.
/// </para>
/// <para>
/// <b>Gone: "a scoped declaration must bind the tenant term it declares."</b> This was the strongest arm and
/// it is the one worth being explicit about. It read a request's own claim to be tenant-confined and checked
/// the claim against the request's parameter binding. There are now no such claims: the ordinary confined case
/// declares nothing, because the parameter binding already carries that information in the one place that
/// decides what the query does. A second declaration restating it could only agree with it or lie about it,
/// and the arm existed precisely because it could lie. Removing the claim removes the arm and the lie
/// together. The residual property — a request that accepts a tenant partition and then discards it — is
/// carried by <c>EXDATA002</c>, which sees it at compile time and in consumer code, where a source scan of
/// this repository cannot reach.
/// </para>
/// <para>
/// <b>Gone: "a file constructing a disposition must declare the override."</b> It closed a rename escaping the
/// census from the other side. The control arm below closes the same hole against the attribute, and there is
/// no second construction site to guard now that the factory is deleted.
/// </para>
/// <para>
/// <b>Kept, and rekeyed: the justification arms.</b> These are what the index is worth, and they are kept here
/// rather than left entirely to the analyzer for two reasons. The scan is <em>independent of the analyzer
/// being wired</em>: an analyzer that is mis-registered reports nothing, and reporting nothing is
/// indistinguishable from a clean tree, so a check that needs no analyzer infrastructure is the one that
/// catches a broken one. And it reads every <c>.cs</c> file under <c>src/</c> as text, so a request in a
/// project outside the analyzer's reference graph is still in scope.
/// </para>
/// <para>
/// <b>One arm is deliberately stricter here than the shipped analyzer, and that asymmetry is the point.</b>
/// <c>EXDATA001</c> requires a non-empty justification and stops there. This class additionally requires the
/// justification to be a <em>literal</em>, because a justification assembled from a constant elsewhere cannot
/// be found by anyone grepping the estate for its cross-tenant statements, and that searchability is the
/// index's whole value. That is a house convention, not a correctness property, so it is enforced on our own
/// tree and not shipped as a build error to consumers — under <c>TreatWarningsAsErrors</c> a diagnostic on a
/// style preference breaks their build for a matter of taste.
/// </para>
/// <para>
/// <b>What this scan still cannot see.</b> It reads <c>src/</c> only, so <c>tests/</c> and <c>samples/</c> are
/// outside it — deliberately: a sample carrying the attribute is documentation and a fixture carrying it is a
/// fixture. And it reads declarations as written, never the statement finally executed, so a request whose SQL
/// is assembled by interpolation is beyond it. That residual belongs to the provider conformance arms, which
/// run the statement against real infrastructure.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class NoTenantTermDeclarationsAreCheckedShould
{
    /// <summary>The attribute a request applies to declare that it carries no tenant term.</summary>
    private const string AttributeToken = "[NoTenantTerm(";

    /// <summary>The enum naming the argument that makes the absent tenant term correct.</summary>
    private const string ConfinementToken = "TenantConfinement.";

    /// <summary>The removed marker. Its absence is asserted, so it cannot return unnoticed.</summary>
    private const string RemovedMarkerToken = "TenancyDisposition";

    /// <summary>
    /// The kinds the attribute admits. A declaration naming something else does not compile, so this list is a
    /// readability aid for the failure message rather than a second source of truth.
    /// </summary>
    private static readonly string[] Kinds =
    [
        "EstateWide",
        "IdentityAddressed",
        "ForeignKeyConfined",
        "NoTenantDimension",
    ];

    private static readonly IReadOnlyList<ScannedFile> Scanned = Scan();

    // ---- CONTROL ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The scan must find the attribute it is about, applied a plausible number of times.
    /// </summary>
    /// <remarks>
    /// Without this, every arm below is satisfied by finding nothing — and finding nothing is exactly what a
    /// rename produces. That is not hypothetical for this subject: an acceptance criterion written against it
    /// was permanently green because it grepped a function that had since been renamed, and a search for a dead
    /// name always passes. The predecessor of this class hit the same failure inside its own control, where a
    /// substring match on <c>Tenancy</c> also matched <c>TenancyMode</c> and kept the control green over a
    /// member that no longer existed.
    /// </remarks>
    [Fact]
    public void FindTheAttributeItself_SoARenamedAttributeCannotSilentlyPassEveryOtherArm()
    {
        var applications = Scanned.Sum(f => f.AttributeOffsets.Count);

        applications.ShouldBeGreaterThan(
            40,
            customMessage:
            $"Only {applications} application(s) of {AttributeToken} found under src/. The migration recorded "
            + "52 across 50 files. A number far below that means the attribute was renamed, or the scan no "
            + "longer reaches the source tree — either way every arm below is now searching for a dead name, "
            + "which always passes. Point the tokens in this class at the live name; do not leave the arms "
            + "green over an empty set.");
    }

    // ---- ARM 1: the removed marker stays removed --------------------------------------------------------------

    /// <summary>
    /// The self-attested tenancy marker must not reappear under <c>src/</c>.
    /// </summary>
    /// <remarks>
    /// It was written to at a hundred sites and read at none, was <c>protected</c> on the base and absent from
    /// <c>IDataRequest</c> so no executor could reach it by construction, and its default was byte-identical to
    /// a real answer. Its replacement is an attribute for the exception and nothing at all for the default, so
    /// the marker returning would mean both mechanisms are live at once and a reader cannot tell which one
    /// speaks for a given request.
    /// </remarks>
    [Fact]
    public void KeepTheSelfAttestedMarkerDeleted_SoOneMechanismSpeaksForEachRequest()
    {
        var offenders = Scanned
            .Where(f => f.MentionsRemovedMarker)
            .Select(f => f.RelativePath + ": mentions " + RemovedMarkerToken + ", which was removed.")
            .ToList();

        offenders.ShouldBeEmpty(FormatOffenders(offenders));
    }

    // ---- ARM 2: every entry in the index names its kind --------------------------------------------------------

    /// <summary>
    /// Every application of the attribute must name a confinement kind.
    /// </summary>
    /// <remarks>
    /// The kind is what makes the index answerable. Its predecessor recorded only that a request was "global",
    /// and classifying the justifications it had accumulated put just 23 of 51 in the estate-wide kind: 19 were
    /// addressed by a primary key, 5 by a foreign key to one, and 5 sat on tables with no tenant column at all.
    /// Those are not shades of one thing. On an identity-addressed statement a tenant term is a defect whose
    /// only reachable effect is turning the correct row into zero rows, while on an estate-wide sweep its
    /// absence is the entire intent — so an index that collapses them removes exactly the distinction a
    /// reviewer needs.
    /// </remarks>
    [Fact]
    public void NameAConfinementKind_ForEveryApplicationOfTheAttribute()
    {
        var offenders = new List<string>();

        foreach (var file in Scanned.Where(f => f.AttributeOffsets.Count > 0))
        {
            foreach (var offset in file.AttributeOffsets)
            {
                var argument = ReadFirstArgument(file.Text, offset);

                if (argument is null || !argument.StartsWith(ConfinementToken, StringComparison.Ordinal))
                {
                    offenders.Add(
                        file.RelativePath
                        + ": the first argument is '" + (argument ?? "<unreadable>")
                        + "', not one of " + string.Join("/", Kinds.Select(k => ConfinementToken + k))
                        + ". State which argument makes the absent tenant term correct at this statement.");
                }
            }
        }

        offenders.ShouldBeEmpty(FormatOffenders(offenders));
    }

    // ---- ARM 3: every entry states a non-empty literal justification --------------------------------------------

    /// <summary>
    /// Every application must supply a non-empty justification, written as a literal at the declaration site.
    /// </summary>
    /// <remarks>
    /// The factory this attribute replaces called <c>ThrowIfNullOrWhiteSpace</c> on its reason — but only when
    /// it ran, and the property that would have run it was never read, so an empty reason shipped and threw
    /// nowhere. <c>EXDATA001</c> now makes non-emptiness a compile-time failure for us and for consumers. This
    /// arm adds the literal requirement on top, for the reason given in the class remarks.
    /// </remarks>
    [Fact]
    public void StateANonEmptyLiteralJustification_ForEveryApplicationOfTheAttribute()
    {
        var offenders = new List<string>();

        foreach (var file in Scanned.Where(f => f.AttributeOffsets.Count > 0))
        {
            foreach (var offset in file.AttributeOffsets)
            {
                var justification = ReadSecondArgumentAsLiteral(file.Text, offset);

                if (justification is null)
                {
                    offenders.Add(
                        file.RelativePath
                        + ": the justification is not a string literal at the declaration site. State it "
                        + "inline, so the estate's untenanted statements can be found by searching for them.");
                }
                else if (string.IsNullOrWhiteSpace(justification))
                {
                    offenders.Add(
                        file.RelativePath
                        + ": the justification is empty. A statement may decline a tenant term, but it may not "
                        + "do so silently — an unexplained omission is indistinguishable from an oversight, "
                        + "which is the distinction this declaration exists to make.");
                }
            }
        }

        offenders.ShouldBeEmpty(FormatOffenders(offenders));
    }

    // ---- scanning ----------------------------------------------------------------------------------------------

    private static IReadOnlyList<ScannedFile> Scan()
    {
        var repositoryRoot = TestHelpers.GetRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var scanned = new List<ScannedFile>();

        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var normalised = path.Replace(Path.DirectorySeparatorChar, '/');

            if (normalised.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || normalised.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var raw = File.ReadAllText(path);

            if (!raw.Contains(AttributeToken, StringComparison.Ordinal)
                && !raw.Contains(RemovedMarkerToken, StringComparison.Ordinal))
            {
                continue;
            }

            var text = StripComments(raw);

            scanned.Add(new ScannedFile(
                RelativePath: Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                Text: text,
                AttributeOffsets: FindArgumentOffsets(text, AttributeToken),
                MentionsRemovedMarker: text.Contains(RemovedMarkerToken, StringComparison.Ordinal)));
        }

        return scanned;
    }

    /// <summary>
    /// Removes line and block comments, so prose about tenancy is never mistaken for tenancy code.
    /// </summary>
    /// <remarks>
    /// Deliberately naive: it does not track string literals, so a <c>//</c> inside one would truncate the line.
    /// No file in scope contains one — these requests build SQL, not URLs — and the alternative is a C# parser
    /// for a question that does not need one. Should that change, the arms fail closed: a truncated line loses
    /// its justification and is reported as a violation, which is the safe direction to be wrong in.
    /// </remarks>
    private static string StripComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                var end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end;
                continue;
            }

            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? text.Length : end + 2;
                continue;
            }

            _ = builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>Offsets just past every occurrence of a token.</summary>
    private static IReadOnlyList<int> FindArgumentOffsets(string text, string token)
    {
        var offsets = new List<int>();
        var index = text.IndexOf(token, StringComparison.Ordinal);

        while (index >= 0)
        {
            offsets.Add(index + token.Length);
            index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
        }

        return offsets;
    }

    /// <summary>Reads the first argument as written, up to the separating comma.</summary>
    private static string? ReadFirstArgument(string text, int offset)
    {
        var i = SkipWhitespace(text, offset);
        var start = i;

        while (i < text.Length && text[i] != ',' && text[i] != ')')
        {
            i++;
        }

        return i > start ? text[start..i].Trim() : null;
    }

    /// <summary>
    /// Reads the second argument when — and only when — it is a string literal written at this site.
    /// </summary>
    /// <remarks>
    /// Returning null for any other expression is the literal requirement: a constant referenced from elsewhere
    /// is well-formed C# and is reported here, on purpose.
    /// </remarks>
    private static string? ReadSecondArgumentAsLiteral(string text, int offset)
    {
        var i = offset;

        while (i < text.Length && text[i] != ',' && text[i] != ')')
        {
            i++;
        }

        if (i >= text.Length || text[i] != ',')
        {
            return null;
        }

        i = SkipWhitespace(text, i + 1);

        if (i >= text.Length || text[i] != '"')
        {
            return null;
        }

        i++;
        var builder = new StringBuilder();

        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                _ = builder.Append(text[i + 1]);
                i += 2;
                continue;
            }

            if (text[i] == '"')
            {
                break;
            }

            _ = builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static string FormatOffenders(IReadOnlyCollection<string> offenders) =>
        "A statement that carries no tenant term must name the argument that makes that correct, and say why. "
        + $"{offenders.Count} declaration(s) do not:\n  - "
        + string.Join("\n  - ", offenders);

    /// <summary>One scanned source file, with its comments removed.</summary>
    private sealed record ScannedFile(
        string RelativePath,
        string Text,
        IReadOnlyList<int> AttributeOffsets,
        bool MentionsRemovedMarker);
}
