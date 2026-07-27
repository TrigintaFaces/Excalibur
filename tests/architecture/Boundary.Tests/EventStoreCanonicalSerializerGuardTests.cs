// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Boundary.Tests;

/// <summary>
/// Structural drift-prevention guard for the jyp40l canonical-serializer seam: every event store MUST
/// source its <c>JsonSerializerOptions</c> from the single <c>EventSerializationDefaults.CreateCanonicalOptions()</c>
/// factory (camelCase + string-enum + null-ignore) rather than declaring its own inline
/// <c>new JsonSerializerOptions</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Event stores historically each declared a private inline options bag that
/// diverged from the DI read path — some omitted the string-enum converter (writing an enum as a number)
/// or the null-ignore condition — so an event written by a store could mis-read when loaded through the
/// serializer. The jyp40l fix converges all stores onto one canonical options instance. The wire-shape
/// round-trip lock (<c>PostgresEventStoreSerializerCanonicalizationShould</c>) proves the behavior for one
/// store; this guard makes "a future store re-introduces a private options bag" inexpressible-without-going-RED
/// across ALL stores (the exact recurrence class of the S855 <c>i2eabb</c> serializer fault).
/// </para>
/// <para>
/// A store that genuinely needs bespoke serialization (unrelated to the event body contract) is allowlisted
/// with a written reason; the companion allowlist-honesty test removes stale exemptions.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class EventStoreCanonicalSerializerGuardTests
{
    // A store "touches JSON serialization" if it calls JsonSerializer.Serialize / SerializeToUtf8Bytes
    // (the "JsonSerializer.Serialize" token is a prefix of "…SerializeToUtf8Bytes") or Deserialize.
    private static readonly string[] JsonSerializerCallTokens =
    [
        "JsonSerializer.Serialize",
        "JsonSerializer.Deserialize",
    ];

    private const string CanonicalFactoryToken = "EventSerializationDefaults";

    /// <summary>
    /// Event-store source files exempt from the canonical-options invariant, each with a written reason
    /// (no silent omission). Keyed by file name. Remove an entry when the underlying reason is resolved so
    /// the guard begins enforcing it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Allowlist =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EncryptingAuditEventStore.cs"] =
                "IAuditStore, not IEventStore. Serializes an EncryptedData envelope via the source-generated " +
                "AuditEncryptionJsonContext — not IDomainEvent bodies — so the canonical event-body contract does not apply.",
            ["EncryptingEventStoreDecorator.cs"] =
                "IEventStore decorator that operates on IDomainEvent instances PRE-serialization; its only " +
                "JsonSerializer call deserializes the EncryptedData encryption envelope — not IDomainEvent bodies. " +
                "Event-body canonical serialization is delegated to the wrapped inner store (which the guard enforces).",
            ["ElasticsearchSecurityEventStore.cs"] =
                "ISecurityEventStore, not IEventStore. Serializes SecurityEvent/SecurityEventQuery via the " +
                "source-generated SecurityEventSerializerContext — not IDomainEvent bodies.",
            ["FileSecurityEventStore.cs"] =
                "ISecurityEventStore, not IEventStore. Serializes SecurityEvent/SecurityEventQuery via the " +
                "source-generated SecurityEventSerializerContext — not IDomainEvent bodies.",
        };

    private static bool CallsJsonSerializer(string text) =>
        JsonSerializerCallTokens.Any(token => text.Contains(token, StringComparison.Ordinal));

    private static bool ViolatesCanonicalInvariant(string text) =>
        CallsJsonSerializer(text) && !text.Contains(CanonicalFactoryToken, StringComparison.Ordinal);

    private static List<(string Path, string Text)> EventStoreFiles()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        // ncql7x: scan *EventStore*.cs (not just *EventStore.cs) so event-store DECORATORS/wrappers
        // (e.g. EncryptingEventStoreDecorator) — which can also serialize event bodies — are covered, not
        // only the concrete leaf stores. A decorator that serializes only an envelope/metadata (not
        // IDomainEvent bodies) is allowlisted with a written reason below.
        return Directory
            .EnumerateFiles(srcRoot, "*EventStore*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedArtifactPath(path))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();
    }

    [Fact]
    public void EveryEventStore_SourcesCanonicalSerializerOptions_NoBypass()
    {
        var stores = EventStoreFiles();

        // Non-vacuity floor: the framework ships ~8 concrete event stores (InMemory/Postgres/SqlServer/
        // MongoDb/Sqlite + the AwsS3/Gcs/AzureBlob cold stores + Cosmos). Far fewer means the scan filter or
        // source layout drifted and the guard would pass vacuously.
        stores.Count.ShouldBeGreaterThanOrEqualTo(
            8,
            "Expected to find the framework's concrete event-store source files (*EventStore.cs). " +
            $"Found only {stores.Count} — the scan filter or source layout likely drifted; this guard must not pass vacuously.");

        // A store violates the canonical invariant when it calls JsonSerializer at all (inline-options bag
        // OR the DEFAULT serializer with no options — the strictly-worse i2eabb bypass) without sourcing the
        // canonical factory. Anchoring on ANY JsonSerializer call — not just "new JsonSerializerOptions" —
        // makes the zero-options bypass inexpressible (ncql7x / enforce-invariants-structurally): a store
        // touching JSON must reference EventSerializationDefaults or be allowlisted with a written reason.
        var violations = stores
            .Where(f => ViolatesCanonicalInvariant(f.Text))
            .Select(f => Path.GetFileName(f.Path))
            .Where(name => !Allowlist.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            "Every event store that serializes with JsonSerializer MUST source its options from the single " +
            $"'{CanonicalFactoryToken}.CreateCanonicalOptions()' factory — whether via an inline options bag OR " +
            "the default (no-options) serializer, which writes PascalCase / enum-as-number / nulls that mis-read " +
            "when loaded through the canonical read path (the i2eabb recurrence class). " +
            "Offending file(s): " + string.Join(", ", violations) +
            $". Source the '{CanonicalFactoryToken}' factory, or — if genuinely exempt (not an IEventStore / not " +
            "serializing IDomainEvent bodies) — allowlist the file with a written reason.");
    }

    [Fact]
    public void CanonicalSerializerAllowlist_OnlyExemptsFilesThatStillExistAndStillViolate()
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
            var stillViolates = ViolatesCanonicalInvariant(text);

            stillViolates.ShouldBeTrue(
                $"Allowlisted file '{fileName}' now sources the canonical options — the underlying issue is fixed. " +
                "Remove the allowlist entry so the guard enforces it.");
        }
    }

    [Fact]
    public void ViolationDetection_IsNonVacuous_FlagsBothInlineBagAndZeroOptionsBypass()
    {
        // Proves the strengthened predicate is not vacuous across all three shapes:
        //  1. inline options bag with no canonical factory  → violation
        //  2. DEFAULT serializer, NO options at all (the ncql7x zero-options bypass) → violation (the key case)
        //  3. a store sourcing the canonical factory → NOT a violation
        const string inlineSample =
            "private readonly JsonSerializerOptions _o = new JsonSerializerOptions { }; " +
            "var b = JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), _o);";
        const string zeroOptionsSample =
            "var b = JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType());";
        const string canonicalSample =
            "private readonly JsonSerializerOptions _o = EventSerializationDefaults.CreateCanonicalOptions(); " +
            "var b = JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), _o);";

        ViolatesCanonicalInvariant(inlineSample)
            .ShouldBeTrue("an inline options bag with a JsonSerializer call and no canonical factory must be flagged");
        ViolatesCanonicalInvariant(zeroOptionsSample)
            .ShouldBeTrue("a DEFAULT (no-options) JsonSerializer call must be flagged — this is the ncql7x zero-options bypass");
        ViolatesCanonicalInvariant(canonicalSample)
            .ShouldBeFalse("a store sourcing 'EventSerializationDefaults.CreateCanonicalOptions()' must pass");
    }

    // ---------------------------------------------------------------------------------------------
    // 4o8i86 (S872, L9 GUARD extension): the canonical-serializer structural invariant is extended from
    // event stores to the OTHER durable delivery stores — Outbox / Inbox / Saga / Snapshot. Those stores
    // persist headers/metadata/state JSON that must ALSO converge on the single canonical options
    // (camelCase + string-enum + null-ignore) rather than each declaring its own inline
    // `new JsonSerializerOptions { … }` that silently diverges (the exact i2eabb recurrence class, one
    // store family over). Trigger for THIS set is narrower and literal: a store that *constructs its own
    // JsonSerializerOptions* (`new JsonSerializerOptions`) without sourcing `EventSerializationDefaults`.
    // ---------------------------------------------------------------------------------------------

    // File-name globs for the durable delivery stores brought under the canonical-options invariant.
    private static readonly string[] DeliveryStoreGlobs =
    [
        "*OutboxStore*.cs",
        "*InboxStore*.cs",
        "*SagaStore*.cs",
        "*SnapshotStore*.cs",
    ];

    private const string OwnOptionsConstructionToken = "new JsonSerializerOptions";

    /// <summary>
    /// Delivery-store source files exempt from the "no own JsonSerializerOptions" invariant, each with a
    /// written reason. Keyed by file name. Remove an entry when the underlying reason is resolved so the
    /// guard begins enforcing it. (Empty today: the current violators — SqlServerOutboxStore,
    /// SqlServerInboxStore, PostgresInboxStore — are the convergence TARGET, not exemptions.)
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DeliveryStoreAllowlist =
        new Dictionary<string, string>(StringComparer.Ordinal);

    // A delivery store violates the invariant when it constructs its OWN JsonSerializerOptions without
    // sourcing the canonical factory — whatever it serializes (headers, metadata, saga state, snapshot
    // body) must use the single canonical options instance so write/read stay byte-compatible.
    private static bool ConstructsOwnOptionsBypassingCanonical(string text) =>
        text.Contains(OwnOptionsConstructionToken, StringComparison.Ordinal)
        && !text.Contains(CanonicalFactoryToken, StringComparison.Ordinal);

    private static List<(string Path, string Text)> DeliveryStoreFiles()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        return DeliveryStoreGlobs
            .SelectMany(glob => Directory.EnumerateFiles(srcRoot, glob, SearchOption.AllDirectories))
            .Where(path => !IsGeneratedArtifactPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();
    }

    [Fact]
    public void EveryDeliveryStore_DoesNotConstructOwnJsonSerializerOptions_BypassingCanonical()
    {
        var stores = DeliveryStoreFiles();

        // Non-vacuity floor: the framework ships many concrete Outbox/Inbox/Saga/Snapshot stores across the
        // provider packages. Far fewer means the scan globs or source layout drifted and this guard would
        // pass vacuously.
        stores.Count.ShouldBeGreaterThanOrEqualTo(
            20,
            "Expected to find the framework's Outbox/Inbox/Saga/Snapshot store source files. " +
            $"Found only {stores.Count} — the scan globs or source layout likely drifted; this guard must not pass vacuously.");

        var violations = stores
            .Where(f => ConstructsOwnOptionsBypassingCanonical(f.Text))
            .Select(f => Path.GetFileName(f.Path))
            .Where(name => !DeliveryStoreAllowlist.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            "Every durable delivery store (Outbox/Inbox/Saga/Snapshot) that serializes JSON MUST source its " +
            $"options from the single '{CanonicalFactoryToken}.CreateCanonicalOptions()' factory rather than " +
            "constructing its own 'new JsonSerializerOptions { … }' — an inline options bag silently diverges " +
            "on enum/null handling from the canonical read path (the i2eabb recurrence class). " +
            "Offending file(s): " + string.Join(", ", violations) +
            $". Source the '{CanonicalFactoryToken}' factory, or — if genuinely exempt — allowlist the file with a written reason.");
    }

    [Fact]
    public void DeliveryStoreAllowlist_OnlyExemptsFilesThatStillExistAndStillViolate()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        foreach (var (fileName, reason) in DeliveryStoreAllowlist)
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

            ConstructsOwnOptionsBypassingCanonical(File.ReadAllText(matches[0])).ShouldBeTrue(
                $"Allowlisted file '{fileName}' now sources the canonical options — remove the allowlist entry so the guard enforces it.");
        }
    }

    [Fact]
    public void DeliveryStoreViolationDetection_IsNonVacuous()
    {
        // 1. an inline options bag with no canonical factory → violation
        // 2. the canonical factory → NOT a violation
        // 3. no JSON options at all → NOT a violation (opaque-payload stores are unaffected)
        const string ownOptionsSample =
            "private readonly JsonSerializerOptions _o = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };";
        const string canonicalSample =
            "private readonly JsonSerializerOptions _o = EventSerializationDefaults.CreateCanonicalOptions();";
        const string noJsonSample =
            "await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);";

        ConstructsOwnOptionsBypassingCanonical(ownOptionsSample)
            .ShouldBeTrue("a store constructing its own 'new JsonSerializerOptions' with no canonical factory must be flagged");
        ConstructsOwnOptionsBypassingCanonical(canonicalSample)
            .ShouldBeFalse("a store sourcing 'EventSerializationDefaults.CreateCanonicalOptions()' must pass");
        ConstructsOwnOptionsBypassingCanonical(noJsonSample)
            .ShouldBeFalse("a store that does not construct JsonSerializerOptions must not be flagged");
    }

    // ---------------------------------------------------------------------------------------------
    // S873 (yk8dq5/f62rwx/iz3wwu + Postgres): the read-model PROJECTION stores converge on a SEPARATE
    // canonical factory — ProjectionSerializationDefaults.CreateReadModelOptions() — NOT the event-body
    // EventSerializationDefaults (read-model wire shape differs from the event-body contract; SA self-
    // correction 22888). Every projection store that serializes read-model JSON MUST source that factory
    // rather than an inline `new JsonSerializerOptions` OR the default (no-options) serializer — both the
    // i2eabb (S855) recurrence class for read models. A glob keeps a FUTURE projection store from
    // re-introducing a private/default options bag without going RED (enforce-invariants-structurally).
    // ---------------------------------------------------------------------------------------------

    private static readonly string[] ProjectionStoreGlobs = ["*ProjectionStore*.cs"];

    private const string ProjectionCanonicalFactoryToken = "ProjectionSerializationDefaults";

    /// <summary>
    /// Projection-store source files exempt from the projection-canonical-options invariant, each with a
    /// written reason. Keyed by file name.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ProjectionStoreAllowlist =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EncryptingProjectionStoreDecorator.cs"] =
                "IProjectionStore decorator operating on projection instances PRE/POST the inner store's " +
                "serialization; its JsonSerializer use handles the EncryptedData field envelope — not the " +
                "read-model body — so read-model canonical serialization is delegated to the wrapped inner " +
                "store (which the guard enforces).",
        };

    // A projection store violates the invariant when it calls JsonSerializer at all (inline-options bag OR
    // the DEFAULT serializer with no options — the strictly-worse i2eabb bypass) without sourcing the
    // projection-canonical factory.
    private static bool ViolatesProjectionCanonicalInvariant(string text) =>
        CallsJsonSerializer(text) && !text.Contains(ProjectionCanonicalFactoryToken, StringComparison.Ordinal);

    private static List<(string Path, string Text)> ProjectionStoreFiles()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        return ProjectionStoreGlobs
            .SelectMany(glob => Directory.EnumerateFiles(srcRoot, glob, SearchOption.AllDirectories))
            .Where(path => !IsGeneratedArtifactPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();
    }

    [Fact]
    public void EveryProjectionStore_SourcesProjectionCanonicalSerializerOptions_NoBypass()
    {
        var stores = ProjectionStoreFiles();

        // Non-vacuity floor: the framework ships the projection-store family across the provider packages
        // (Cosmos/DynamoDb/Firestore/Postgres/SqlServer + abstractions/decorators/resolvers). Far fewer
        // means the scan glob or source layout drifted and this guard would pass vacuously.
        stores.Count.ShouldBeGreaterThanOrEqualTo(
            20,
            "Expected to find the framework's *ProjectionStore*.cs source files. " +
            $"Found only {stores.Count} — the scan glob or source layout likely drifted; this guard must not pass vacuously.");

        var violations = stores
            .Where(f => ViolatesProjectionCanonicalInvariant(f.Text))
            .Select(f => Path.GetFileName(f.Path))
            .Where(name => !ProjectionStoreAllowlist.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        violations.ShouldBeEmpty(
            "Every projection store that serializes read-model JSON MUST source its options from the single " +
            $"'{ProjectionCanonicalFactoryToken}.CreateReadModelOptions()' factory — whether via an inline " +
            "options bag OR the default (no-options) serializer, which writes a wire shape that mis-reads when " +
            "loaded through the canonical read path (the i2eabb recurrence class, read-model side). " +
            "Offending file(s): " + string.Join(", ", violations) +
            $". Source the '{ProjectionCanonicalFactoryToken}' factory, or — if genuinely exempt (serializes an " +
            "envelope/metadata, not the read-model body) — allowlist the file with a written reason.");
    }

    [Fact]
    public void ProjectionStoreAllowlist_OnlyExemptsFilesThatStillExistAndStillViolate()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        foreach (var (fileName, reason) in ProjectionStoreAllowlist)
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

            ViolatesProjectionCanonicalInvariant(File.ReadAllText(matches[0])).ShouldBeTrue(
                $"Allowlisted file '{fileName}' now sources the projection-canonical options — remove the allowlist entry so the guard enforces it.");
        }
    }

    [Fact]
    public void ProjectionStoreViolationDetection_IsNonVacuous()
    {
        // 1. the default (no-options) serializer → violation (the i2eabb zero-options bypass)
        // 2. an inline options bag with no projection factory → violation
        // 3. the projection factory → NOT a violation
        // 4. no JSON serialization at all → NOT a violation
        const string defaultSerializerSample =
            "var json = JsonSerializer.Serialize(projection);";
        const string inlineSample =
            "private readonly JsonSerializerOptions _o = new JsonSerializerOptions { }; " +
            "var json = JsonSerializer.Serialize(projection, _o);";
        const string canonicalSample =
            "private readonly JsonSerializerOptions _o = ProjectionSerializationDefaults.CreateReadModelOptions(); " +
            "var json = JsonSerializer.Serialize(projection, _o);";
        const string noJsonSample =
            "await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync(ct);";

        ViolatesProjectionCanonicalInvariant(defaultSerializerSample)
            .ShouldBeTrue("a DEFAULT (no-options) JsonSerializer call must be flagged — the read-model zero-options bypass");
        ViolatesProjectionCanonicalInvariant(inlineSample)
            .ShouldBeTrue("an inline options bag with no projection factory must be flagged");
        ViolatesProjectionCanonicalInvariant(canonicalSample)
            .ShouldBeFalse("a projection store sourcing 'ProjectionSerializationDefaults.CreateReadModelOptions()' must pass");
        ViolatesProjectionCanonicalInvariant(noJsonSample)
            .ShouldBeFalse("a projection store that does not serialize JSON must not be flagged");
    }

    private static bool IsGeneratedArtifactPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }
}
