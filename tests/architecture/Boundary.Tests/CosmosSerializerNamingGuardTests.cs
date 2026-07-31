// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Boundary.Tests;

/// <summary>
/// Structural guard locking the invariant behind the S855 <c>i2eabb</c> serializer fault on the
/// <i>framework-built-client</i> axis: every Cosmos store that builds its OWN <c>CosmosClient</c> MUST
/// configure deterministic property naming so its documents emit Cosmos's required lowercase keys
/// (notably <c>id</c>) — instead of inheriting the Cosmos SDK v3 DEFAULT serializer (Newtonsoft), which
/// emits PascalCase and silently breaks point-read-by-<c>id</c> / partition-key matching.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Most non-saga Cosmos documents (inbox / outbox / snapshot / event /
/// activity-group / grant) carry ONLY System.Text.Json <c>[JsonPropertyName("...")]</c> attributes. That is
/// correct ONLY because their stores build a client that forces a deterministic serializer; Newtonsoft (the
/// SDK-v3 default) ignores STJ attributes, so a store-owned client that forgot to force naming would emit
/// PascalCase and reintroduce the <c>i2eabb</c> failure (point-read NotFound → load null → silently inert).
/// Nothing previously locked that "the store forces naming" half of the invariant — this guard does, so a
/// future store (or a refactor dropping the serializer config) goes RED naming the file rather than shipping
/// a silently-broken provider.
/// </para>
/// <para>
/// <b>The two accepted mechanisms</b> (a framework-built client must use at least one):
/// <list type="bullet">
/// <item><description><c>UseSystemTextJsonSerializerWithOptions</c> — opt into STJ, honoring the documents'
/// <c>[JsonPropertyName]</c> attributes (used by inbox/outbox/snapshot/event/authorization stores).</description></item>
/// <item><description><c>CosmosSerializationOptions { PropertyNamingPolicy = CamelCase }</c> — keep the
/// default Newtonsoft serializer but force camelCase naming so PascalCase <c>Id</c> → lowercase <c>id</c>
/// (used by <c>CosmosDbCdcStateStore</c>, whose document carries no per-property attributes).</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Out of scope (a complementary guard covers it):</b> stores that take a <i>consumer-supplied</i>
/// <c>Container</c> (e.g. <c>CosmosDbChangeFeedCheckpointStore</c>) do NOT build a client, so the framework
/// cannot force the serializer — those documents must instead carry the dual STJ+Newtonsoft attribute pair,
/// asserted by <c>ChangeFeedCheckpointDocumentSerializationShould</c>. Together the two guards make
/// "is this Cosmos document correctly serialized?" a structurally enforced property.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class CosmosSerializerNamingGuardTests
{
    private const string StjForcingToken = "UseSystemTextJsonSerializerWithOptions";
    private const string NewtonsoftNamingPolicyToken = "CosmosSerializationOptions";

    /// <summary>
    /// Files that build a <c>CosmosClient</c> but are deliberately exempt from the deterministic-naming
    /// invariant, each with a written reason (no silent omission). Keyed by file name; remove an entry when
    /// the underlying issue is resolved so the guard begins enforcing it.
    /// </summary>
    // Zero allowlist entries: CosmosDbPersistenceProvider.cs (formerly tracked bd-7tdbiu) now configures a
    // deterministic serializer, so it no longer violates — the allowlist-honesty guard requires the stale entry
    // be removed. A future genuine-but-exempt violation is re-added here with a written reason.
    private static readonly IReadOnlyDictionary<string, string> Allowlist = new Dictionary<string, string>(StringComparer.Ordinal);

    [Fact]
    public void EveryFrameworkBuiltCosmosClient_ForcesDeterministicPropertyNaming()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        var scanned = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedArtifactPath(path))
            .Select(path => (Path: path, Surfaces: SplitSurfaces(File.ReadAllText(path))))
            .ToList();

        var codeClientFiles = scanned
            .Where(f => BuildsClient(f.Surfaces.Code))
            .ToList();

        // Non-vacuity floor, CODE surface: ~15 framework-built Cosmos clients across the persistence
        // packages. A scan finding far fewer means the filter or layout drifted and the guard would pass
        // vacuously. Measured against the code surface deliberately — before the two-surface split this
        // counted doc recipes too, which would mask real drift with prose.
        codeClientFiles.Count.ShouldBeGreaterThanOrEqualTo(
            10,
            "Expected to find the framework's Cosmos store files that build their own CosmosClient in CODE. " +
            $"Found only {codeClientFiles.Count} — the scan filter or source layout likely drifted; " +
            "this guard must not pass vacuously.");

        var violations = scanned
            .SelectMany(f => new[]
            {
                Violation(f.Path, f.Surfaces.Code, "code"),
                Violation(f.Path, f.Surfaces.Comments, "shipped doc recipe"),
            })
            .Where(v => v is not null)
            .Select(v => v!)
            .Where(v => !Allowlist.ContainsKey(v.Split(' ')[0]))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            "Every Cosmos client the framework SHIPS — whether built in CODE or shown in a DOC RECIPE a " +
            "consumer copies — MUST force deterministic property naming (either " +
            $"'{StjForcingToken}' or '{NewtonsoftNamingPolicyToken}' with a CamelCase policy), or its " +
            "STJ-only documents will serialize PascalCase under the SDK-v3 default Newtonsoft serializer and " +
            "break Cosmos's lowercase 'id' point-read (the i2eabb fault). The two surfaces are checked " +
            "INDEPENDENTLY: a token in prose does not discharge a code obligation, and a correctly " +
            "configured code path does not excuse a recipe that teaches the defect. Offending surface(s): " +
            string.Join(", ", violations) +
            ". Add the missing serializer configuration to the named surface, or — if genuinely exempt — " +
            "allowlist the file with a written reason.");
    }

    /// <summary>
    /// Reports a violation for one surface of one file, or <see langword="null"/> when that surface is clean.
    /// </summary>
    /// <remarks>
    /// A surface violates when it builds a client and does NOT itself carry a naming token. Evaluating each
    /// surface against its OWN text is the whole point: the pre-S905 predicate scanned the undifferentiated
    /// file, so a token anywhere discharged the obligation everywhere — which let a doc comment silence a
    /// code-level defect, and would have let a correct code path silence a defective shipped recipe.
    /// </remarks>
    private static string? Violation(string path, string surfaceText, string surfaceName)
    {
        if (!BuildsClient(surfaceText))
        {
            return null;
        }

        var configured = surfaceText.Contains(StjForcingToken, StringComparison.Ordinal)
                      || surfaceText.Contains(NewtonsoftNamingPolicyToken, StringComparison.Ordinal);

        return configured ? null : $"{Path.GetFileName(path)} ({surfaceName})";
    }

    private static bool BuildsClient(string text) =>
        text.Contains("new CosmosClient(", StringComparison.Ordinal);

    /// <summary>
    /// Splits a source file into its CODE surface and its COMMENT surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why two surfaces.</b> A shipped XML doc comment is a SECOND PRODUCT SURFACE: it compiles into the
    /// package's <c>.xml</c>, surfaces in IntelliSense, and consumers copy its recipes verbatim. A recipe
    /// that builds a <c>CosmosClient</c> without deterministic naming teaches the i2eabb defect just as
    /// surely as code that does it — S905 shipped exactly that, in this repository, and this guard is what
    /// caught it.
    /// </para>
    /// <para>
    /// Line-based by design: whole-line <c>///</c>, <c>//</c>, and block-comment continuations form the
    /// comment surface; everything else is code. A trailing comment on a code line stays with the code,
    /// which is correct — the code on that line is still code.
    /// </para>
    /// </remarks>
    /// <param name="text">Raw file contents.</param>
    /// <returns>The code-only and comment-only projections of the file.</returns>
    private static (string Code, string Comments) SplitSurfaces(string text)
    {
        var lines = text.Split('\n');

        var commentLines = lines.Where(IsCommentLine);
        var codeLines = lines.Where(line => !IsCommentLine(line));

        return (string.Join('\n', codeLines), string.Join('\n', commentLines));

        static bool IsCommentLine(string line)
        {
            var trimmed = line.TrimStart();

            return trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NamingGuardAllowlist_OnlyExemptsFilesThatStillBuildAClientAndStillViolate()
    {
        // Keeps the allowlist honest: a stale entry (file deleted, renamed, or already fixed) must be removed
        // so the guard re-enforces the invariant rather than silently exempting a now-compliant file.
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        foreach (var (fileName, reason) in Allowlist)
        {
            reason.ShouldNotBeNullOrWhiteSpace($"Allowlist entry '{fileName}' must carry a written reason.");

            var matches = Directory
                .EnumerateFiles(srcRoot, fileName, SearchOption.AllDirectories)
                .Where(path => !IsGeneratedArtifactPath(path))
                .ToList();

            matches.Count.ShouldBe(
                1,
                $"Allowlisted file '{fileName}' should resolve to exactly one source file; found {matches.Count}. " +
                "If it was renamed or removed, update or delete the allowlist entry.");

            // Evaluated per surface, matching the main scan: an allowlist entry stays justified only while
            // SOME shipped surface of the file still builds a client and still lacks the naming token.
            var (code, comments) = SplitSurfaces(File.ReadAllText(matches[0]));

            (BuildsClient(code) || BuildsClient(comments)).ShouldBeTrue(
                $"Allowlisted file '{fileName}' no longer builds a CosmosClient — remove the stale allowlist entry.");

            var stillViolates = Violation(matches[0], code, "code") is not null
                             || Violation(matches[0], comments, "shipped doc recipe") is not null;

            stillViolates.ShouldBeTrue(
                $"Allowlisted file '{fileName}' now configures a deterministic serializer — the underlying issue " +
                "is fixed. Remove the allowlist entry so the guard enforces it.");
        }
    }

    private static bool IsGeneratedArtifactPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }
}
