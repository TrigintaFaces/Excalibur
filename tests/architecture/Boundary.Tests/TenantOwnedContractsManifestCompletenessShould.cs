// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Bounds the completeness of the tenant-owned-contract manifest itself. Its oracle is the
/// <b>namespace</b>, not the manifest: every persistence-store contract declared in the core
/// persistence-contract namespaces must be classified — either present in
/// <c>TenantOwnedContracts.All</c> (or inheriting a manifested contract) OR named on the explicit
/// <see cref="Exclusions"/> list with a written reason.
/// </summary>
/// <remarks>
/// <para>
/// <b>The degree of freedom this closes.</b> The sibling guard
/// (<c>TenantScopingCapabilityMarkerWiredShould</c>) reads the manifest as its oracle: it proves the
/// gate covers every <i>manifested</i> contract. But nothing bounds the manifest's own completeness —
/// a new persistence store that stores tenant-owned rows and is simply never added to the manifest is
/// invisible to that guard and to the runtime assertion, and leaks every tenant's rows on the first
/// host that wires row-discrimination. The gate's own source says so verbatim: "Closing it requires a
/// guard whose oracle is the NAMESPACE rather than the manifest." This is that guard.
/// </para>
/// <para>
/// <b>The oracle (population predicate).</b> A candidate persistence-store contract is a type that is
/// (1) an interface, (2) public, (3) declared in one of the namespaces the manifest itself draws from —
/// exactly <c>Excalibur.EventSourcing</c>, <c>Excalibur.Dispatch</c>, or
/// <c>Excalibur.Dispatch.Messaging</c> — and (4) whose simple name ends in <c>Store</c> or <c>Erasure</c>
/// (the second suffix captures the manifested <see cref="Excalibur.EventSourcing.IEventStoreErasure"/>,
/// which is a store capability that does not end in "Store"). This rule is derived from the manifest's
/// own home namespaces, so the six manifested contracts are a strict subset of the population by
/// construction, and every new store contract added beside them is seen.
/// </para>
/// <para>
/// <b>Inherited membership.</b> A contract that inherits a manifested store (for example
/// <c>ITransactionalEventStore : IEventStore</c>, or the projection refinements
/// <c>IExistsProjectionStore&lt;T&gt; : IProjectionStore&lt;T&gt;</c>) is covered by its base: the gate
/// decorates by the base contract, so the derived contract needs no separate entry. This guard treats
/// such contracts as classified without an exclusion, by scanning implemented interfaces for a
/// manifested store's simple name.
/// </para>
/// <para>
/// <b>Scope boundary, stated rather than implied.</b> The population is intentionally the two core
/// persistence-contract abstraction assemblies' root namespaces. Store contracts in sub-namespaces
/// (<c>Excalibur.Dispatch.Delivery</c>, <c>Excalibur.EventSourcing.Subscriptions</c>, …), in provider
/// assemblies, or in unrelated subsystems (A3 authorization, compliance, CDC providers, security) are
/// out of population by design — a tenant-owned store added there would escape this guard. Closing that
/// wider hole would require every subsystem to declare its own tenant-ownership manifest. This guard
/// locks the real, bounded invariant it can honestly enforce, and says so rather than pretending to
/// more.
/// </para>
/// <para>
/// This project carries no project references — it reasons over the BUILT framework assemblies by
/// reflection (<see cref="Assembly.LoadFrom(string)"/> against the located output DLLs), mirroring
/// <c>AdvertisedStrategyWiredOrFailLoudShould</c>. The manifest is read from its source declaration
/// (mirroring the sibling guard), because the manifest is <c>internal</c> and its <c>typeof(...)</c>
/// array is the human-authored declaration of intent that an edit can actually get wrong.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class TenantOwnedContractsManifestCompletenessShould
{
    /// <summary>The namespaces the manifested contracts are declared in — the population's home.</summary>
    private static readonly HashSet<string> TargetNamespaces = new(StringComparer.Ordinal)
    {
        "Excalibur.EventSourcing",
        "Excalibur.Dispatch",
        "Excalibur.Dispatch.Messaging",

        // Added when the erasure-request and legal-hold stores were manifested. Widening the oracle is what
        // this guard's own failure message prescribes when a manifested contract lives outside the scanned
        // set, and it is the direction that matters: the population must always be a superset of the
        // manifest, or the guard cannot bound the manifest's completeness. Admitting this namespace also
        // brings the rest of the compliance persistence contracts under classification, which is how the
        // audit-store and consent-store gaps below became visible rather than assumed absent.
        "Excalibur.Compliance",
    };

    /// <summary>The declared set of tenant-owned contracts — the manifest whose completeness this bounds.</summary>
    private const string ManifestRelativePath =
        "src/Excalibur/Excalibur.MultiTenancy/TenantOwnedContracts.cs";

    /// <summary>Matches a <c>typeof(IFooStore)</c> entry in the tenant-owned-contracts manifest.</summary>
    private static readonly Regex ManifestContract = new(
        @"typeof\(\s*(?<contract>[^)<,\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Persistence-store contracts in the population that are NOT tenant-owned (or whose tenant scope is
    /// enforced elsewhere), each with a written reason. A contract in the population that is neither
    /// manifested, nor inheriting a manifested contract, nor listed here fails the completeness arm and
    /// forces a human to classify it: tenant-owned rows &#8594; add to the manifest; otherwise &#8594; add
    /// here with a reason.
    /// </summary>
    /// <remarks>
    /// Entries marked "AMBIGUOUS" are genuine judgment calls surfaced for review: they hold tenant-derived
    /// data but are currently excluded rather than manifested. Moving any to the manifest is a deliberate
    /// tenant-ownership decision, not a mechanical one.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> Exclusions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // --- Excalibur.EventSourcing -------------------------------------------------------------------
        ["ITenantFilteredEventStore"] =
            "The tenant-filtering mechanism itself (adds WHERE TenantId to a shared-shard event store), " +
            "discovered via GetService and applied by the routing layer. It is part of the isolation " +
            "apparatus, not a subject of it; decorating it would be circular.",
        ["ICursorMapStore"] =
            "Coordination state: maps projection/subscription cursors to stream positions. Framework " +
            "bookkeeping, not tenant-owned domain rows.",
        ["IColdEventStore"] =
            "KNOWN OPEN cross-tenant cold-read isolation gap: read through tiered storage, no tenant in the " +
            "cold key; the row-discriminator-MT + tiered + cold combination is gated UNSUPPORTED at startup " +
            "(fails fast — cold emits no tenant-scoping capability); real tenant-partitioned-cold fix carried.",
        ["ISnapshotStore"] =
            "AMBIGUOUS (flagged for review). Aggregate-state snapshots derived from a tenant's events, " +
            "keyed by aggregate id and loaded on the same path as the manifested IEventStore, so tenant " +
            "isolation is enforced upstream at the event store. Excluded pending review.",
        ["IIncrementalSnapshotStore"] =
            "AMBIGUOUS (flagged for review). Incremental variant of ISnapshotStore; same reasoning — " +
            "tenant scope inherited from the aggregate/event store that produced it.",
        ["IMaterializedViewStore"] =
            "KNOWN OPEN cross-tenant read-model isolation gap (view rows keyed by (ViewName,ViewId), no " +
            "tenant); real fix carried.",
        ["IAtomicMaterializedViewStore"] =
            "KNOWN OPEN cross-tenant read-model isolation gap (view rows keyed by (ViewName,ViewId), no " +
            "tenant); real fix carried.",

        // --- Excalibur.Dispatch (inbox/outbox capability facets + CDC state) ---------------------------
        ["IDeadLetterableOutboxStore"] =
            "Segregated capability facet of the manifested IOutboxStore (composition, not inheritance): " +
            "dead-letter operations over the same outbox rows already gated via IOutboxStore. Not a " +
            "distinct store.",
        ["IBackoffSchedulableOutboxStore"] =
            "Segregated capability facet of the manifested IOutboxStore: backoff scheduling over the same " +
            "outbox rows. Not a distinct store.",
        ["IClaimableInboxStore"] =
            "Segregated capability facet of the manifested IInboxStore (explicitly composition, not " +
            "inheritance — see its remarks): atomic claim-before-execute over the same inbox rows. Not a " +
            "distinct store.",
        ["IScopedTransactionalInboxStore"] =
            "Segregated capability facet of the manifested IInboxStore: scoped-transactional operations " +
            "over the same inbox rows. Not a distinct store.",
        ["IBackoffSchedulableInboxStore"] =
            "Segregated capability facet of the manifested IInboxStore: backoff scheduling over the same " +
            "inbox rows. Not a distinct store.",
        ["IProcessingTrackingInboxStore"] =
            "Segregated capability facet of the manifested IInboxStore: durable Processing-status tracking " +
            "over the same inbox rows. Not a distinct store.",
        ["ITransactionalInboxStore"] =
            "Segregated capability facet of the manifested IInboxStore: transactional operations over the " +
            "same inbox rows. Not a distinct store.",
        ["ICdcStateStore"] =
            "Coordination state: change-data-capture cursor/position bookkeeping — how far a CDC processor " +
            "has read a source. Not tenant-owned domain rows.",

        // --- Excalibur.Compliance (erasure/legal-hold facets, inventory, audit, consent) ----------------
        ["IErasureQueryStore"] =
            "Segregated query facet of the manifested IErasureStore, implemented on the same store instance " +
            "and reached through GetService: its tenant-facing read applies the same ambient tenant " +
            "predicate as the store, and its due-request drain is deliberately estate-wide (a background " +
            "scheduler runs with no ambient tenant and must drain every tenant). Not a distinct store.",
        ["IErasureCertificateStore"] =
            "Segregated certificate facet of the manifested IErasureStore, on the same instance: certificate " +
            "reads are tenant-scoped through the request they certify, and the retention sweep is " +
            "deliberately estate-wide. Not a distinct store.",
        ["ILegalHoldQueryStore"] =
            "Segregated query facet of the manifested ILegalHoldStore, on the same instance: tenant-facing " +
            "reads apply the same ambient tenant predicate, and the hold-expiry sweep is deliberately " +
            "estate-wide. Not a distinct store.",
        ["IDataInventoryStore"] =
            "AMBIGUOUS (flagged for review). Records WHERE personal data lives — field and table " +
            "registrations discovered from [PersonalData] annotations plus the processing-activities map. " +
            "That is a description of the deployment's own schema rather than a tenant's rows, but " +
            "per-data-subject registrations sit on the same contract. Excluded pending review.",
        ["IDataInventoryQueryStore"] =
            "AMBIGUOUS (flagged for review). Query facet of IDataInventoryStore on the same instance; same " +
            "reasoning, and it inherits whatever classification that contract is given.",
        ["IAuditStore"] =
            "KNOWN OPEN tenant-classification gap. Audit events are tenant-derived and the provider stores " +
            "do thread the ambient ITenantContext, but the contract is neither manifested nor gated, so no " +
            "capability assertion rejects a tenant-unaware audit provider at startup. Surfaced by widening " +
            "this guard's namespace oracle; carried as its own work rather than silently manifested here, " +
            "because gating it without first auditing the provider read paths would fail hosts closed " +
            "without proving isolation.",
        ["IDurableAuditStore"] =
            "Capability facet of IAuditStore (durable-write guarantees over the same audit rows); inherits " +
            "that contract's classification and its KNOWN OPEN gap above.",
        ["IAuditAnnotationStore"] =
            "Annotations attached to audit events by event id, stored alongside them and reached on the " +
            "same provider; inherits IAuditStore's classification and its KNOWN OPEN gap above.",
        ["IComplianceStore"] =
            "KNOWN OPEN tenant-classification gap. Holds consent records and subject-access-request " +
            "tracking, which are tenant-owned by the same argument as erasure requests, but the contract is " +
            "neither manifested nor gated. Surfaced by widening this guard's namespace oracle and carried " +
            "as its own work.",
        ["ISoc2ReportStore"] =
            "Estate-level, not tenant-owned: SOC 2 reports describe the service organisation's own control " +
            "environment over a reporting period, which is a property of the deployment rather than of any " +
            "tenant's data. A per-tenant scope would fragment the evidence an auditor asks for as one set.",
    };

    private static readonly ConcurrentDictionary<string, Assembly?> AssemblyCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// LIVENESS ARM. Reflection actually finds a non-empty population, and every manifested contract is a
    /// member of it. Without this, the completeness arm could pass vacuously against an empty population.
    /// </summary>
    [Fact]
    public void ThePopulationScan_FindsThePersistenceContracts_AndContainsEveryManifestedOne()
    {
        var populationNames = ReadPopulationSimpleNames();
        var manifested = ReadManifestedContracts();

        populationNames.Count.ShouldBeGreaterThanOrEqualTo(
            6,
            "The namespace population scan found fewer persistence-store contracts than the six manifested " +
            "ones. Either the framework assemblies were not built (reflection loaded nothing), or the " +
            "namespace/suffix oracle drifted. This guard would pass vacuously against an under-populated " +
            $"set. Found: {populationNames.Count}.");

        manifested.Count.ShouldBeGreaterThanOrEqualTo(
            4,
            $"The tenant-owned-contract manifest declared fewer contracts than expected ({manifested.Count}). " +
            "The manifest moved or its shape changed; repoint this guard rather than let it read an empty set.");

        var manifestedOutsidePopulation = manifested
            .Where(name => !populationNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        manifestedOutsidePopulation.ShouldBeEmpty(
            "Every manifested tenant-owned contract must be a member of the namespace population, or the " +
            "population oracle no longer covers the manifest and cannot bound its completeness. Manifested " +
            "contract(s) the population scan did not find: " + string.Join(", ", manifestedOutsidePopulation) +
            ". If a manifested contract moved to a namespace this guard does not scan, widen TargetNamespaces.");

        // Anchors: if these ever leave either the manifest or the population, the guard has been narrowed
        // and must be revisited deliberately rather than silently.
        populationNames.ShouldContain("IEventStore", "The event-store contract vanished from the population scan.");
        manifested.ShouldContain("IEventStore", "IEventStore is no longer manifested as tenant-owned.");
        manifested.ShouldContain("ISagaStore", "ISagaStore is no longer manifested as tenant-owned.");
    }

    /// <summary>
    /// SAFETY ARM. Every persistence-store contract in the population is classified: manifested, inheriting
    /// a manifested contract, or explicitly excluded with a reason. An unclassified store fails RED.
    /// </summary>
    /// <remarks>
    /// RED when a new persistence-store contract is added to a scanned namespace without deciding whether
    /// it stores tenant-owned rows. That is the "manifest completeness is unbounded" hole: a tenant-owned
    /// store simply never added to the manifest leaks every tenant's rows on the first host that wires
    /// row-discrimination, and neither the sibling guard nor the runtime assertion would notice.
    /// </remarks>
    [Fact]
    public void EveryPersistenceStoreContract_IsManifestedInheritedOrExcluded()
    {
        var population = ReadPopulation();
        var manifested = ReadManifestedContracts();

        var unclassified = population
            .Where(type => !IsClassified(type, manifested))
            .Select(SimpleName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        unclassified.ShouldBeEmpty(
            "These persistence-store contracts live in a tenant-owned namespace but are neither in " +
            "TenantOwnedContracts.All, nor inherit a manifested contract, nor appear on this guard's " +
            "exclusion list. Classify each: if it stores tenant-owned rows, add it to the manifest AND " +
            "gate it in ApplyRowDiscriminator; otherwise add it to Exclusions with a one-line reason. A " +
            "tenant-owned store left unmanifested leaks every tenant's rows on the first host that wires " +
            "row-discrimination. Unclassified contract(s): " + string.Join(", ", unclassified) + ".");
    }

    /// <summary>
    /// EXCLUSION-DISCIPLINE ARM. Every exclusion names a contract that actually exists in the population
    /// (no stale/typo entries) and carries a non-empty reason, and no exclusion shadows a manifested
    /// contract.
    /// </summary>
    /// <remarks>
    /// A stale exclusion (naming a contract no longer in the population) would silently pre-approve a
    /// future contract of that name; an empty reason would defeat the "name a reason" requirement; an
    /// exclusion that also appears in the manifest is a contradiction — the contract cannot be both
    /// tenant-owned and excluded.
    /// </remarks>
    [Fact]
    public void EveryExclusion_IsInThePopulation_HasAReason_AndDoesNotShadowTheManifest()
    {
        var populationNames = ReadPopulationSimpleNames();
        var manifested = ReadManifestedContracts();

        var stale = Exclusions.Keys
            .Where(name => !populationNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        stale.ShouldBeEmpty(
            "These exclusions name a contract that is not in the population scan — a stale or misspelled " +
            "entry that silently pre-approves any future contract of that name. Remove or correct it: " +
            string.Join(", ", stale) + ".");

        foreach (var (name, reason) in Exclusions)
        {
            reason.ShouldNotBeNullOrWhiteSpace($"Exclusion '{name}' must carry a written reason.");
        }

        var shadowsManifest = Exclusions.Keys
            .Where(manifested.Contains)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        shadowsManifest.ShouldBeEmpty(
            "These contracts are both manifested as tenant-owned AND excluded — a contradiction. Remove the " +
            "exclusion (the manifest wins) or the manifest entry: " + string.Join(", ", shadowsManifest) + ".");
    }

    /// <summary>
    /// INHERITANCE-LIVENESS ARM. Contracts that inherit a manifested store are recognised as classified by
    /// inheritance — not accidentally shunted onto the exclusion list, and not left unclassified.
    /// </summary>
    /// <remarks>
    /// If the inheritance scan silently broke (stopped matching a base contract's simple name), these
    /// derived contracts would either fall into the unclassified set (a false RED) or be papered over by an
    /// exclusion (a false GREEN). This arm pins that at least one real derived contract is covered by its
    /// base, exercising the mechanism against the built assemblies.
    /// </remarks>
    [Fact]
    public void ContractsInheritingAManifestedStore_AreCoveredByTheirBase()
    {
        var population = ReadPopulation();
        var manifested = ReadManifestedContracts();

        var inheritedCovers = population
            .Where(type => !manifested.Contains(SimpleName(type)) && InheritsAManifestedStore(type, manifested))
            .Select(SimpleName)
            .ToHashSet(StringComparer.Ordinal);

        inheritedCovers.ShouldNotBeEmpty(
            "No population contract was recognised as inheriting a manifested store, yet refinements such " +
            "as ITransactionalEventStore : IEventStore exist in the built assemblies. The inheritance scan " +
            "has drifted and derived contracts are no longer auto-covered by their base.");

        // None of the inherited-covered contracts should also be sitting on the exclusion list: an
        // exclusion for a contract already covered by inheritance is redundant and hides the mechanism.
        var redundantlyExcluded = inheritedCovers
            .Where(Exclusions.ContainsKey)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        redundantlyExcluded.ShouldBeEmpty(
            "These contracts are covered by inheriting a manifested store and must not also be excluded " +
            "(the exclusion is dead and masks the inheritance mechanism): " +
            string.Join(", ", redundantlyExcluded) + ".");
    }

    /// <summary>
    /// NON-VACUITY SELF-TEST. Proves the completeness comparison actually goes RED on an unclassified store
    /// and GREEN on a fully classified set, against synthetic inputs (mutating the real manifest or source
    /// would risk clobbering a file another agent holds uncommitted, and is not git-recoverable).
    /// </summary>
    [Fact]
    public void TheCompletenessComparison_GoesRed_WhenAStoreIsUnclassified_AndGreen_WhenAllAreClassified()
    {
        var manifested = new HashSet<string>(StringComparer.Ordinal) { "IEventStore", "ISagaStore" };
        var exclusions = new HashSet<string>(StringComparer.Ordinal) { "ISnapshotStore" };

        // A ghost store that is neither manifested, nor inherited, nor excluded MUST be reported.
        var population = new[] { "IEventStore", "ISagaStore", "ISnapshotStore", "IGhostStore" };

        var unclassified = population
            .Where(name => !manifested.Contains(name) && !exclusions.Contains(name))
            .ToList();

        unclassified.ShouldBe(
            ["IGhostStore"],
            "The completeness comparison must report exactly the unclassified store. If this is empty, the " +
            "real safety arm cannot fail and is decoration.");

        // And the converse: once the ghost is classified (manifested here), the comparison reports nothing.
        var classifiedManifest = new HashSet<string>(StringComparer.Ordinal) { "IEventStore", "ISagaStore", "IGhostStore" };
        population
            .Where(name => !classifiedManifest.Contains(name) && !exclusions.Contains(name))
            .ShouldBeEmpty("A fully classified population must report no unclassified stores, or the arm " +
                "would fail on correct code.");
    }

    // ---- classification -----------------------------------------------------------------------------------

    /// <summary>A population type is classified when manifested, inheriting a manifested store, or excluded.</summary>
    private static bool IsClassified(Type type, HashSet<string> manifested)
    {
        var name = SimpleName(type);
        return manifested.Contains(name)
            || Exclusions.ContainsKey(name)
            || InheritsAManifestedStore(type, manifested);
    }

    /// <summary>
    /// True when the type implements an interface whose simple name is a manifested store — so the gate
    /// decorates it by that base and it needs no separate entry.
    /// </summary>
    private static bool InheritsAManifestedStore(Type type, HashSet<string> manifested) =>
        type.GetInterfaces().Any(implemented => manifested.Contains(SimpleName(implemented)));

    // ---- population (reflection over the namespace oracle) -------------------------------------------------

    /// <summary>Reads the population of persistence-store contracts from the namespace oracle by reflection.</summary>
    private static IReadOnlyList<Type> ReadPopulation()
    {
        var population = new List<Type>();
        foreach (var assembly in LoadAllFrameworkAssemblies())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is { IsInterface: true, IsPublic: true }
                    && type.Namespace is { } ns
                    && TargetNamespaces.Contains(ns)
                    && HasStoreContractSuffix(SimpleName(type)))
                {
                    population.Add(type);
                }
            }
        }

        return population;
    }

    private static HashSet<string> ReadPopulationSimpleNames() =>
        ReadPopulation().Select(SimpleName).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// A persistence-store contract's simple name ends in <c>Store</c> or <c>Erasure</c>. The second
    /// suffix admits the manifested IEventStoreErasure (a store capability that does not end in "Store").
    /// </summary>
    private static bool HasStoreContractSuffix(string simpleName) =>
        simpleName.EndsWith("Store", StringComparison.Ordinal)
        || simpleName.EndsWith("Erasure", StringComparison.Ordinal);

    /// <summary>Reduces a type to its simple name without generic arity (<c>IProjectionStore`1</c> &#8594; <c>IProjectionStore</c>).</summary>
    private static string SimpleName(Type type)
    {
        var name = type.Name;
        var backtick = name.IndexOf('`', StringComparison.Ordinal);
        return backtick < 0 ? name : name[..backtick];
    }

    // ---- manifest (source declaration) --------------------------------------------------------------------

    /// <summary>Reads the simple names of the contracts declared tenant-owned in the manifest source.</summary>
    private static HashSet<string> ReadManifestedContracts()
    {
        var manifestPath = Path.Combine(TestHelpers.GetRepositoryRoot(), ManifestRelativePath);

        File.Exists(manifestPath).ShouldBeTrue(
            $"Expected the tenant-owned-contract manifest at '{manifestPath}'. If it moved, repoint this " +
            "guard — it is the declaration this bounds against, not an incidental file.");

        return ManifestContract
            .Matches(File.ReadAllText(manifestPath))
            .Select(match => match.Groups["contract"].Value.Trim())
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    // ---- reflection assembly loading (mirrors AdvertisedStrategyWiredOrFailLoudShould) ---------------------

    /// <summary>
    /// Loads every built framework assembly (<c>Excalibur*.dll</c> under <c>src/**/bin</c>), one per simple
    /// name, preferring this test's build configuration + TFM.
    /// </summary>
    private static IReadOnlyList<Assembly> LoadAllFrameworkAssemblies()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var baseDir = AppContext.BaseDirectory.Replace('\\', '/');
        var preferConfig = baseDir.Contains("/release/", StringComparison.OrdinalIgnoreCase) ? "/Release/" : "/Debug/";

        var byName = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "Excalibur*.dll", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(p => p.Contains(preferConfig, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.Contains("/net10.0/", StringComparison.OrdinalIgnoreCase))
                .First());

        var assemblies = new List<Assembly>();
        foreach (var dll in byName)
        {
            var simpleName = Path.GetFileNameWithoutExtension(dll);
            var assembly = AssemblyCache.GetOrAdd(simpleName, _ => TryLoad(dll));
            if (assembly is not null)
            {
                assemblies.Add(assembly);
            }
        }

        return assemblies;
    }

    private static Assembly? TryLoad(string dll)
    {
        var simpleName = Path.GetFileNameWithoutExtension(dll);
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
        if (loaded is not null)
        {
            return loaded;
        }

        try
        {
            return Assembly.LoadFrom(dll);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException)
        {
            return null;
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        // LoadFrom does not eagerly resolve transitive dependencies, so GetTypes() can throw on a few
        // unloadable types — the loadable ones are still returned and are sufficient for this scan.
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
