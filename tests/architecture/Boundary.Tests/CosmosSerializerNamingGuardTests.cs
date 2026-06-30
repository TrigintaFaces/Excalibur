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
    private static readonly IReadOnlyDictionary<string, string> Allowlist = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CosmosDbPersistenceProvider.cs"] =
            "TRACKED OPEN FINDING (bd-7tdbiu): the generic cloud-native provider builds its client with " +
            "neither serializer mechanism, so a consumer TDocument with PascalCase 'Id' may not emit the " +
            "Cosmos-required lowercase 'id'. Whether a GENERIC provider must force naming (vs leaving it to " +
            "the consumer's document attributes) is a Backend/SoftwareArchitect design call. Remove this " +
            "allowlist entry when bd-7tdbiu is resolved (recommended fix mirrors CdcStateStore's CamelCase policy).",
    };

    [Fact]
    public void EveryFrameworkBuiltCosmosClient_ForcesDeterministicPropertyNaming()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        var clientBuildingFiles = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedArtifactPath(path))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(f => f.Text.Contains("new CosmosClient(", StringComparison.Ordinal))
            .ToList();

        // Non-vacuity floor: there are ~15 framework-built Cosmos clients across the persistence packages.
        // A scan that finds far fewer means the filter or layout drifted and the guard would pass vacuously.
        clientBuildingFiles.Count.ShouldBeGreaterThanOrEqualTo(
            10,
            "Expected to find the framework's Cosmos store files that build their own CosmosClient. " +
            $"Found only {clientBuildingFiles.Count} — the scan filter or source layout likely drifted; " +
            "this guard must not pass vacuously.");

        var violations = clientBuildingFiles
            .Where(f => !f.Text.Contains(StjForcingToken, StringComparison.Ordinal)
                     && !f.Text.Contains(NewtonsoftNamingPolicyToken, StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f.Path))
            .Where(name => !Allowlist.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            "Every Cosmos store that builds its OWN CosmosClient MUST force deterministic property naming " +
            $"(either '{StjForcingToken}' or '{NewtonsoftNamingPolicyToken}' with a CamelCase policy), or its " +
            "STJ-only documents will serialize PascalCase under the SDK-v3 default Newtonsoft serializer and " +
            "break Cosmos's lowercase 'id' point-read (the i2eabb fault). Offending file(s): " +
            string.Join(", ", violations) +
            ". Add the missing serializer configuration, or — if genuinely exempt — allowlist the file with a written reason.");
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

            var text = File.ReadAllText(matches[0]);

            text.Contains("new CosmosClient(", StringComparison.Ordinal).ShouldBeTrue(
                $"Allowlisted file '{fileName}' no longer builds a CosmosClient — remove the stale allowlist entry.");

            var stillViolates = !text.Contains(StjForcingToken, StringComparison.Ordinal)
                             && !text.Contains(NewtonsoftNamingPolicyToken, StringComparison.Ordinal);

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
