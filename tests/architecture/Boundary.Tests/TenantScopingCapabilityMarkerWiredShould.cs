// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Structural guard on the tenant-scoping capability gate: every persistence contract that
/// <c>AddMultiTenancy</c> <i>requires</i> a capability marker for MUST have at least one shipped provider
/// that <i>registers</i> that marker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <c>RequireTenantScopingCapability&lt;TContract&gt;</c> throws at registration when
/// <c>ITenantScopingCapability&lt;TContract&gt;</c> is absent from the service collection. That is a fail-closed
/// guard, and it is correct — but it is only <i>reachable</i> if some provider actually registers the marker.
/// A contract that is <b>required but never provided</b> makes the gate reject every host, including correct
/// ones. A contract that is <b>provided but never required</b> silently escapes tenant scoping.
/// </para>
/// <para>
/// <b>The vacuity trap this guard is built to avoid.</b> A unit test that registers a <i>fake</i> marker into
/// a <c>ServiceCollection</c> and asserts <c>AddMultiTenancy</c> succeeds proves only that the gate reads the
/// marker it was handed. It cannot see that no <i>real</i> provider registers one. That test passes while
/// every production host throws. This guard reads the shipped source instead, so the question it answers is
/// "does a provider register this marker?" rather than "does the gate notice a marker I just invented?"
/// </para>
/// <para>
/// <b>Both arms, deliberately.</b> The safety arm ("every required contract has a provider marker") is
/// satisfied by a gate that requires nothing at all. The liveness arm ("the contracts that ARE correctly
/// wired are seen as satisfied") is satisfied by a scanner that matches everything. Only together do they
/// constrain the real invariant, per the safety/liveness pairing rule. A third arm proves the scanner is not
/// matching vacuously.
/// </para>
/// <para>
/// <b>Exhaustiveness caveat, stated rather than implied.</b> This guard enumerates the contracts the gate
/// <i>already names</i>. A future persistence contract that is added with <i>no</i>
/// <c>RequireTenantScopingCapability</c> call at all is invisible to it — the gate would simply never ask,
/// and this guard would never notice. Closing that hole requires the gate to derive its contract set
/// structurally rather than from hand-written call sites. Until it does, this guard locks the weaker (but
/// real) invariant: nothing the gate requires may go unprovided.
/// </para>
/// <para>
/// <b>"Required" means NAMED, not REACHED.</b> Each gate block sits inside an <c>if (services.Any(...))</c>
/// that fires only when the contract is registered as a service type. This is a source scan: it sees the
/// <c>RequireTenantScopingCapability&lt;T&gt;</c> call and reports T as required. It cannot see whether the
/// enclosing predicate is ever true. A contract whose service type is never registered anywhere yields a
/// gate block that never executes — the guard will still list it as unprovided, correctly, while the
/// runtime consequence is <i>inert</i> rather than <i>throwing</i>.
/// </para>
/// <para>
/// This distinction is load-bearing and was learned the expensive way: a true finding from this guard was
/// read as "the gate rejects every host" when the gate in fact could not run at all. Do not infer a runtime
/// consequence from this guard's output. It tells you the gate <i>names</i> a contract no provider proves.
/// Whether that gate is reachable is a question about DI registration, which no source scan can honestly
/// answer — and a guard that pretended to would be worse than one that says so.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class TenantScopingCapabilityMarkerWiredShould
{
    /// <summary>The gate that declares which contracts must prove tenant-scoping capability.</summary>
    private const string GateRelativePath =
        "src/Excalibur/Excalibur.MultiTenancy/MultiTenancyServiceCollectionExtensions.cs";

    /// <summary>
    /// The open-generic marker interfaces. A declaration is not a registration, so these files are excluded
    /// from the provided-scan. There are two because the gate demands two DIFFERENT capabilities: ambient
    /// scoping for the stores that apply the ambient discriminator, and row-partitioning for the stores whose
    /// reads are deliberately estate-wide and whose tenant travels on the row.
    /// </summary>
    private static readonly string[] MarkerInterfaceRelativePaths =
    [
        "src/Dispatch/Excalibur.Dispatch.Abstractions/ContextValues/ITenantScopingCapability.cs",
        "src/Dispatch/Excalibur.Dispatch.Abstractions/ContextValues/ITenantPartitionedCapability.cs",
    ];

    /// <summary>
    /// Matches <c>RequireTenantScopingCapability&lt;IFooStore&gt;</c> or
    /// <c>RequireTenantPartitionedCapability&lt;IFooStore&gt;</c>, capturing the contract. Both are gates; a
    /// pattern that saw only the scoping one would drop every row-partitioned contract out of the required
    /// set, and the manifest-coverage arm would then report a gated contract as ungated.
    /// </summary>
    private static readonly Regex RequiredContract = new(
        @"Require(?:TenantScoping|TenantPartitioned)Capability<\s*(?<contract>[^>,\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Patterns that each capture the tenant-scoped CONTRACT a provider registers a marker for. wxbwef: a
    /// provider registers a marker through one of FOUR seams, not just the explicit marker type — the earlier
    /// single-regex scan saw only the explicit form and false-flagged <c>IProjectionStore</c> (registered via
    /// the projection seam's capability-family arg) and <c>IEventStoreErasure</c> (via the direct capability
    /// seam) as "unprovided". Each seam ultimately funnels into the internal
    /// <c>AddTenantScopingCapability&lt;T&gt;</c> that emits <c>ITenantScopingCapability&lt;T&gt;</c>, so the
    /// scan must recognise the contract at whichever seam the provider actually uses. Generic type parameters
    /// captured from the seams' OWN declarations/forwarding calls (<c>TContract</c>/<c>TCapabilityFamily</c>/…)
    /// are filtered by <see cref="IsGenericTypeParameter"/>.
    /// </summary>
    private static readonly Regex[] ProvidedContractPatterns =
    [
        // Explicit marker type: ITenantScopingCapability<IFooStore> / TenantScopingCapabilityMarker<IFooStore>,
        // and their row-partitioned counterparts.
        new(@"(?:TenantScopingCapabilityMarker|ITenantScopingCapability|TenantPartitionedCapabilityMarker|ITenantPartitionedCapability)<\s*(?<contract>[^>,\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Store seam: AddTenantAwareStore<IFooStore, FooStoreImpl> — the marker is the 1st generic (contract).
        // One verb covers both the ambient-scoped and row-partitioned mechanisms (ADR-348): the seam derives
        // which capability to emit from the store's own constructor shape, so a single call-site pattern is
        // enough to recognise a provider registering EITHER marker for this contract.
        new(@"AddTenantAwareStore<\s*(?<contract>[^>,\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Projection seam: AddTenantScopedProjectionStore<IProjectionStore<T>, ConcreteStore<T>, IProjectionStore<object>>
        // — the marker is the LAST (3rd) generic, the capability family. The 1st and 2nd are the service type
        // and the concrete store whose constructor the seam reads to derive the mechanism; each may itself be
        // generic (no comma inside its own argument list).
        new(@"AddTenantScopedProjectionStore<\s*[^,]+,\s*[^,]+,\s*(?<contract>[^>\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Direct capability seam: AddTenantScopingCapability<IFooErasure> — the marker is the 1st generic. (Not
        // ITenantScopingCapability — the "Add" prefix distinguishes the registration call from the marker type.)
        new(@"AddTenantScopingCapability<\s*(?<contract>[^>,\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Direct row-partitioned capability seam: AddTenantPartitionedCapability<IFooStore>.
        new(@"AddTenantPartitionedCapability<\s*(?<contract>[^>,\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
    ];

    /// <summary>The declared set of tenant-owned contracts — the manifest the gate must cover.</summary>
    private const string ManifestRelativePath =
        "src/Excalibur/Excalibur.MultiTenancy/TenantOwnedContracts.cs";

    /// <summary>Matches a <c>typeof(IFooStore)</c> entry in the tenant-owned-contracts manifest.</summary>
    private static readonly Regex ManifestContract = new(
        @"typeof\(\s*(?<contract>[^)<,\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// The gate's own generic type parameter. It appears in the private helper's declaration
    /// (<c>RequireTenantScopingCapability&lt;TContract&gt;</c>) and is not a contract.
    /// </summary>
    private const string GenericTypeParameter = "TContract";

    /// <summary>
    /// Contracts whose <c>RequireTenantScopingCapability</c> requirement is a deliberate fail-fast TRIPWIRE
    /// for an UNSUPPORTED combination, not a requirement any provider is meant to satisfy.
    /// </summary>
    /// <remarks>
    /// <c>IColdEventStore</c>: under (row-discriminator multi-tenancy + tiered storage + a non-tenant-aware
    /// cold leg) the gate rejects the host at startup rather than silently leak another tenant's archived
    /// events. No provider registers this marker BY DESIGN — cold/blob storage cannot row-scope — so it being
    /// unprovided is correct, and the requirement exists precisely to reject that combination. tracked: ecll67.
    /// The safety arm still RED-detects any OTHER required-but-unprovided contract, and the coupling assertion
    /// below forces this exemption to be revisited if the runtime tripwire is ever removed.
    /// </remarks>
    private static readonly HashSet<string> TripwireOnlyContracts =
        new(StringComparer.Ordinal) { "IColdEventStore" };

    /// <summary>
    /// The contracts the PER-PROVIDER arm below covers. Deliberately a NAMED, verified subset rather than
    /// "every gated contract": every provider of these three has been read and classified, so the arm can
    /// demand that each one either attests or is named as an exemption below. Widening it to contracts whose
    /// provider population has NOT been triaged would either force blanket exemptions (cover that rots) or
    /// report providers as defective without anyone having established that they are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this arm exists, and what the per-CONTRACT arm above cannot see.</b>
    /// <see cref="EveryRequiredContract_HasAProviderThatRegistersItsMarker"/> asks whether SOME provider
    /// registers a marker for a contract. That is satisfied by ONE provider out of ten. Every other provider
    /// of the same contract is then invisible to it — and each of those is a host the gate refuses at startup
    /// for a store that is in fact tenant-scoped. That is the liveness half of the safety/liveness pairing:
    /// the safety arm asserts an unattested store is REFUSED, and nothing asserted the converse, that an
    /// attested store is ACCEPTED, for EVERY provider rather than for one representative.
    /// </para>
    /// <para>
    /// <b>Scope caveat, stated rather than implied.</b> Contracts outside
    /// <see cref="PerProviderContracts"/> — the inbox, audit, erasure, legal-hold and cold-store
    /// families — are NOT covered by this arm. Their provider populations have not been triaged, and several
    /// are known to present no marker today. Do not read this arm's green as coverage of them. The saga and
    /// outbox families ARE covered: every provider of each was read and classified, which is what admitted
    /// them here — six saga registration paths that scoped by tenant and declared nothing, and the one
    /// outbox provider that carries the tenant on the document and had not said so.
    /// </para>
    /// </remarks>
    private static readonly string[] PerProviderContracts =
        ["IEventStore", "ISnapshotStore", "IDeadLetterQueue", "ISagaStore", "IOutboxStore"];

    /// <summary>
    /// A registration whose SERVICE TYPE is one of <see cref="PerProviderContracts"/>, capturing the contract
    /// and a short tail used to recognise (and skip) the keyed-"default" forwarding aliases. An alias promises
    /// a contract without providing a store for it, and the runtime sweep skips those for the same reason
    /// (<c>IsKeyedDefaultForwardingAlias</c>), so counting one here would demand a marker of a store the host
    /// never registered.
    /// </summary>
    private static readonly Regex ProviderStoreRegistration = new(
        @"(?:TryAdd|Add)(?:Keyed)?(?:Singleton|Scoped|Transient)<\s*(?:[A-Za-z0-9_]+\.)*(?<contract>IEventStore|ISnapshotStore"
        + @"|IDeadLetterQueue|ISagaStore|IOutboxStore)\s*(?:,[^>]*)?>\s*(?<tail>[^;]{0,90})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Matches a keyed registration whose FIRST argument is the <c>"default"</c> forwarding key. Anchored to
    /// the first argument on purpose: a real provider registration may mention <c>"default"</c> later in its
    /// factory body, and a bare substring test would drop that registration and report the provider clean.
    /// </summary>
    private static readonly Regex KeyedDefaultAliasCall = new(
        @"^\s*\(\s*""default""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Matches a factory body that resolves the keyed <c>"default"</c> registration of a contract — the
    /// NON-keyed forwarding alias, which promises a contract without providing a store for it.
    /// </summary>
    /// <remarks>
    /// The keyed form above is anchored to the first argument and cannot see this one, which is keyless and
    /// identifiable only by what its factory does. Both are the same thing the runtime sweep skips via
    /// <c>IsKeyedDefaultForwardingAlias</c>, and for the same reason: counting a forwarder as a registration
    /// makes this arm demand a marker of a store the package never registered. The core outbox package is
    /// the live case — it registers this alias, and only when a provider already backs it, so the contract
    /// is never present unbacked; it owns no store and has no mechanism to attest.
    /// </remarks>
    private static readonly Regex NonKeyedDefaultForwardingAlias = new(
        @"GetRequiredKeyedService<[^>]*>\(\s*""default""\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Matches a C# string literal, capturing its content.</summary>
    private static readonly Regex StringLiteral = new(
        @"@?""(?<content>(?:\\.|[^""\\])*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// A literal whose content is a plain DI key rather than embedded prose or example code. Such literals
    /// are PRESERVED by the stripper, because the alias check must tell a forwarding alias from a provider
    /// registration and both are keyed by a string.
    /// </summary>
    private static readonly Regex PlainKeyContent = new(
        @"^[A-Za-z0-9_.:-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Providers that register a contract in <see cref="PerProviderContracts"/> and CORRECTLY present no
    /// marker, because the store implements no tenancy mechanism at all — not an ambient discriminator, not a
    /// tenant carried on the row. RowDiscriminator refusing these hosts is the gate working: the store cannot
    /// confine a read to one tenant, so admitting it would leak. Attesting them would be the lying-marker
    /// defect, which is strictly worse than the gap because it converts a refusal into a silent leak.
    /// </summary>
    /// <remarks>
    /// An entry here is exempt from the ABSENCE of a marker, not from tenancy: "unattested" is the honest
    /// state while a store genuinely has no tenancy mechanism, and this arm must not pressure anyone into
    /// inventing an attestation to make it green.
    /// <see cref="TheExemptedProviders_StillImplementNoTenancyMechanism"/> couples the exemption to that
    /// justification so it cannot rot into cover for a provider that has since become tenant-aware.
    /// <para>
    /// The four document-database event stores — Cosmos DB, DynamoDB, Firestore and MongoDB — were listed
    /// here. They now compose the ambient tenant into their document keys and attest it, so their entries
    /// are gone: leaving them would have made the gate refuse hosts those providers are now correct for,
    /// which is the failure mode the coupling arm exists to force.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> UnattestedByDesign = new(StringComparer.Ordinal)
    {
        // The in-memory outbox carries no tenant on the row and reads no ambient tenant: it contains no
        // tenant term at all. RowDiscriminator refusing it is the gate working, and attesting it would be
        // the lying-marker defect. Coupled to the arm below, which reddens if it ever gains a tenant context.
        ["Excalibur.Outbox.InMemory"] = "IOutboxStore",
    };

    /// <summary>
    /// SAFETY ARM. Every contract the gate requires a marker for must have a provider that registers it,
    /// except the deliberate fail-fast tripwires (see <see cref="TripwireOnlyContracts"/>).
    /// </summary>
    /// <remarks>
    /// RED when a contract is required but unprovided: the gate then throws for every host that registers
    /// that store, including a perfectly configured one. That is an inert-by-rejection control — it can
    /// never pass, so it can never protect anything.
    /// </remarks>
    [Fact]
    public void EveryRequiredContract_HasAProviderThatRegistersItsMarker()
    {
        var required = ReadRequiredContracts();
        var provided = ReadProvidedContracts();

        var unprovided = required
            .Where(contract => !provided.Contains(contract))
            .Where(contract => !TripwireOnlyContracts.Contains(contract))
            .OrderBy(contract => contract, StringComparer.Ordinal)
            .ToList();

        // Couple the tripwire exemption to the runtime tripwire's continued existence: if someone deletes the
        // RequireTenantScopingCapability<IColdEventStore> throw, IColdEventStore drops out of `required`, this
        // assertion goes RED, and the exemption is forced to be reconsidered deliberately rather than rotting
        // into silent cover for a real unprovided contract (ecll67 / enforce-invariants-structurally).
        required.ShouldContain(
            "IColdEventStore",
            "The IColdEventStore fail-fast tripwire (RequireTenantScopingCapability<IColdEventStore>) is no " +
            "longer present in the gate. Its named exemption in TripwireOnlyContracts must be removed or " +
            "re-justified — a standing exemption for a requirement that no longer exists is dead cover.");

        unprovided.ShouldBeEmpty(
            "AddMultiTenancy names an ITenantScopingCapability<T> requirement for these contracts, but no " +
            "shipped provider registers the marker. For a contract whose service type IS registered, " +
            "RowDiscriminator then rejects that host — correct hosts included. For a contract whose service " +
            "type is registered NOWHERE, the gate block never executes and the requirement is inert: it " +
            "looks like coverage and enforces nothing. Check which case you are in before concluding a " +
            "runtime consequence — this is a source scan and cannot tell them apart. Fix by registering the " +
            "marker in the provider's DI extension, or by removing the requirement until a provider can " +
            "prove the capability. Unprovided contract(s): " + string.Join(", ", unprovided) + ".");
    }

    /// <summary>
    /// NON-VACUITY SELF-TEST (ecll67) for the tripwire exemption. Proves the exemption removes ONLY the
    /// cold-store tripwire and still reports a genuinely required-but-unprovided contract — so the safety
    /// arm's teeth survive the exemption.
    /// </summary>
    [Fact]
    public void TheTripwireExemption_RemovesOnlyTheColdStore_AndKeepsTeeth()
    {
        // Synthetic required set: the tripwire contract + a genuinely-unwired contract. No providers.
        var required = new HashSet<string>(StringComparer.Ordinal) { "IColdEventStore", "ISomeOtherStore" };
        var provided = new HashSet<string>(StringComparer.Ordinal);

        var unprovided = required
            .Where(contract => !provided.Contains(contract))
            .Where(contract => !TripwireOnlyContracts.Contains(contract))
            .OrderBy(contract => contract, StringComparer.Ordinal)
            .ToList();

        unprovided.ShouldBe(
            ["ISomeOtherStore"],
            "The tripwire exemption must remove ONLY IColdEventStore and still report a genuinely " +
            "required-but-unprovided contract. If this is empty, the exemption neutered the safety arm's " +
            "teeth (rw2ull); if it contains IColdEventStore, the exemption is not applied.");
    }

    /// <summary>
    /// LIVENESS ARM. The contracts that ARE correctly wired must be seen as satisfied.
    /// </summary>
    /// <remarks>
    /// Without this, the safety arm above is satisfied by a gate that requires nothing, or by a scanner
    /// whose "provided" set is everything. This arm fails if the gate stops requiring the contracts that
    /// are genuinely wired today, or if the scanner's provided-set silently swallows the required-set.
    /// </remarks>
    [Fact]
    public void ContractsWithARegisteredProviderMarker_AreRecognisedAsSatisfied()
    {
        var required = ReadRequiredContracts();
        var provided = ReadProvidedContracts();

        var satisfied = required.Where(provided.Contains).ToList();

        satisfied.ShouldNotBeEmpty(
            "No required contract resolves to a provider-registered marker. Either every provider stopped " +
            "registering markers, or the scan drifted — in both cases the safety arm above would pass " +
            "vacuously, because a gate that requires nothing provable can never be caught requiring " +
            "something unprovable.");

        // IEventStore is wired by the SqlServer and Postgres event-sourcing packages. If this contract ever
        // stops being both required and provided, the gate has been narrowed and the guard must be revisited
        // deliberately rather than silently.
        required.ShouldContain(
            "IEventStore",
            "AddMultiTenancy no longer requires tenant-scoping capability for IEventStore. If that is " +
            "intentional, update this guard; if it is not, RowDiscriminator now admits a tenant-unaware " +
            "event store and will silently return cross-tenant rows.");

        provided.ShouldContain(
            "IEventStore",
            "No provider registers ITenantScopingCapability<IEventStore>. The RowDiscriminator strategy is " +
            "unusable with every event-store provider.");
    }

    /// <summary>
    /// NON-VACUITY ARM. The scanners must actually match, and must not match a contract nobody declared.
    /// </summary>
    /// <remarks>
    /// A scanner that matches nothing makes the safety arm pass trivially; a scanner that matches everything
    /// makes the liveness arm pass trivially. This arm pins both directions against a planted negative.
    /// </remarks>
    [Fact]
    public void TheScanners_MatchRealDeclarations_AndNotInventedOnes()
    {
        var required = ReadRequiredContracts();
        var provided = ReadProvidedContracts();

        required.Count.ShouldBeGreaterThanOrEqualTo(
            4,
            $"Expected the gate to require capability for several persistence contracts; found " +
            $"{required.Count}. The scan filter or the gate's shape has drifted and this guard would pass " +
            "vacuously.");

        provided.Count.ShouldBeGreaterThanOrEqualTo(
            2,
            $"Expected several providers to register capability markers; found {provided.Count}. The scan " +
            "filter or the source layout has drifted.");

        required.ShouldNotContain(
            GenericTypeParameter,
            "The gate's own generic type parameter leaked into the required-contract set. The regex is " +
            "matching the helper's declaration as if it were a call site.");

        required.ShouldNotContain(
            "INoSuchContractExists",
            "The required-contract scanner reported a contract that appears nowhere in the source. It is " +
            "matching something other than what it claims to match.");

        provided.ShouldNotContain(
            "INoSuchContractExists",
            "The provided-contract scanner reported a marker registration that does not exist.");
    }

    /// <summary>
    /// MANIFEST-COVERAGE ARM. Every contract declared tenant-owned must be gated by the registration path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ApplyRowDiscriminator</c> already throws when a manifest entry reaches the end of the method
    /// ungated. That assertion is correct and it is the enforcement that matters in production — but it
    /// fires only when a host actually calls <c>AddMultiTenancy</c> with the row-discriminator strategy.
    /// **No CI shard calls it.** A contract added to the manifest and forgotten in the gate therefore ships
    /// green and detonates on the first consumer who wires multi-tenancy.
    /// </para>
    /// <para>
    /// This arm moves that failure from the consumer's startup to our build. It is deliberately a source
    /// scan and not a reflection test: the manifest is <c>internal</c>, and asserting the invariant at the
    /// point where a human writes it — a <c>typeof(...)</c> beside a <c>Require...&lt;T&gt;</c> — is the
    /// thing that a future edit can actually get wrong.
    /// </para>
    /// <para>
    /// It does <b>not</b> close the residual hole: a tenant-owned contract that is never added to the
    /// manifest at all is invisible to both this arm and the runtime assertion. That gap is irreducible
    /// without deriving the manifest from the type system, and the manifest is the declaration of intent.
    /// Saying so here rather than implying coverage we do not have.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryManifestedTenantOwnedContract_IsGatedByTheRegistrationPath()
    {
        var manifested = ReadManifestedContracts();
        var required = ReadRequiredContracts();

        // Liveness first: a manifest scanner that matches nothing would make the coverage check below
        // pass trivially, exactly the vacuity this whole file exists to refuse.
        manifested.Count.ShouldBeGreaterThanOrEqualTo(
            4,
            $"Expected the tenant-owned-contract manifest to declare several contracts; found " +
            $"{manifested.Count}. The manifest moved or its shape changed, and this arm would pass " +
            "vacuously against an empty set.");

        var ungated = manifested
            .Where(contract => !required.Contains(contract))
            .OrderBy(contract => contract, StringComparer.Ordinal)
            .ToList();

        ungated.ShouldBeEmpty(
            "These contracts are declared tenant-owned in TenantOwnedContracts.All but no " +
            "RequireTenantScopingCapability<T> gate block covers them in ApplyRowDiscriminator. The " +
            "runtime assertion at the end of that method would throw — but only on the first host that " +
            "wires RowDiscriminator, because no CI shard calls AddMultiTenancy. A tenant-owned contract " +
            "shipped ungated leaks every tenant's rows. Ungated contract(s): " +
            string.Join(", ", ungated) + ".");
    }

    /// <summary>
    /// SELF-TEST for the manifest-coverage arm. Proves the comparison detects an ungated contract.
    /// </summary>
    /// <remarks>
    /// The arm above is GREEN today, and a GREEN arm proves nothing until you have watched it go RED. The
    /// honest way to watch it is against synthetic text: mutating the real manifest would mean editing a
    /// file another agent is holding uncommitted, and a clobber there is not recoverable from git.
    /// </remarks>
    [Fact]
    public void TheManifestCoverageComparison_GoesRed_WhenAManifestedContractIsUngated()
    {
        const string manifestText = "typeof(IEventStore), typeof(ISagaStore), typeof(IOrphanStore)";
        const string gateText =
            "RequireTenantScopingCapability<IEventStore>(services, nameof(IEventStore));" +
            "RequireTenantScopingCapability<ISagaStore>(services, nameof(ISagaStore));";

        var manifested = ExtractContracts(ManifestContract, manifestText);
        var required = ExtractContracts(RequiredContract, gateText);

        manifested.ShouldContain("IOrphanStore", "The manifest scanner failed to see the planted contract.");
        required.ShouldNotContain("IOrphanStore", "The gate scanner hallucinated a gate that is not there.");

        var ungated = manifested.Where(c => !required.Contains(c)).ToList();

        ungated.ShouldBe(
            ["IOrphanStore"],
            "The coverage comparison must report exactly the manifested-but-ungated contract. If this is " +
            "empty, the real arm above cannot fail and is decoration.");

        // And the converse: a fully covered manifest reports nothing, so the arm is not stuck RED either.
        var covered = ExtractContracts(ManifestContract, "typeof(IEventStore)");
        covered.Where(c => !required.Contains(c)).ShouldBeEmpty(
            "A fully gated manifest must report no ungated contracts, or the arm would fail on correct code.");
    }

    /// <summary>
    /// NON-VACUITY SELF-TEST (wxbwef) for the extended provided-scan. Proves the scan BOTH recognises a marker
    /// registered through every seam form AND still goes RED on a genuinely required-but-unwired contract (the
    /// real rw2ull leak class). Uses synthetic text — mutating the real source would mean editing a file another
    /// agent may hold uncommitted, and a clobber there is not git-recoverable.
    /// </summary>
    [Fact]
    public void TheProvidedScan_RecognisesEverySeamForm_AndStillCatchesAnUnwiredContract()
    {
        // Synthetic provider source exercising ALL four seams the fix must recognise.
        const string providerText =
            "services.AddTenantAwareStore<IInboxStore, PostgresInboxStore>(sp => ...);" +
            "services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, PgProjectionStore<TProjection>, IProjectionStore<object>>(sp => ...);" +
            "builder.AddTenantScopingCapability<IEventStoreErasure>(services);" +
            "services.AddSingleton<ITenantScopingCapability<IEventStore>>(new TenantScopingCapabilityMarker<IEventStore>());" +
            "services.AddTenantAwareStore<IOutboxStore, PostgresOutboxStore>(sp => ...);";

        var provided = ExtractProvidedContracts(providerText);

        // Every seam's concrete contract is recognised — the drift wxbwef fixed (IProjectionStore + IEventStoreErasure).
        provided.ShouldContain("IInboxStore", "store seam: AddTenantAwareStore<C,..> (1st generic).");
        provided.ShouldContain(
            "IOutboxStore",
            "row-partitioned store seam: AddTenantAwareStore<C,..> (1st generic, ADR-348 one-verb collapse). A scan blind to this "
            + "seam would report the outbox as unprovided while three providers register it, and the safety "
            + "arm would then fail on correct code.");
        provided.ShouldContain("IProjectionStore", "projection seam: AddTenantScopedProjectionStore<..,..,CAP> (3rd generic, capability family).");
        provided.ShouldContain("IEventStoreErasure", "capability seam: AddTenantScopingCapability<C> (1st generic).");
        provided.ShouldContain("IEventStore", "marker seam: explicit ITenantScopingCapability<C> / TenantScopingCapabilityMarker<C>.");

        // Generic type parameters from the seams' own declarations must NOT leak in as contracts.
        provided.ShouldNotContain("TProjection", "the projection seam's 1st-generic type parameter must be filtered.");
        provided.ShouldNotContain("TContract", "the seam helpers' own type parameter must be filtered.");

        // RED on a genuinely required-but-unwired contract: a gate requiring INoProviderStore whose marker the
        // provider text never registers MUST be reported unprovided — the safety arm's real teeth (rw2ull).
        var required = ExtractContracts(
            RequiredContract,
            "RequireTenantScopingCapability<INoProviderStore>(services, nameof(INoProviderStore));");
        required
            .Where(contract => !provided.Contains(contract))
            .ShouldBe(
                ["INoProviderStore"],
                "the safety arm must still report a required-but-unprovided contract — dropping a marker "
                + "registration goes RED. If this is empty the guard has been neutered and enforces nothing.");
    }

    /// <summary>
    /// PER-PROVIDER LIVENESS ARM. For the contracts in <see cref="PerProviderContracts"/>, EVERY provider
    /// package that registers a store must emit its OWN capability marker — not merely one provider
    /// somewhere in the tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RED when a package registers one of these contracts and nothing in that package routes the store
    /// through a marker-emitting seam. The consequence is concrete and it is a LIVENESS break, not a leak:
    /// <c>AddMultiTenancy</c> with <c>RowDiscriminator</c> throws at registration for a store that honors the
    /// ambient tenant perfectly, so the gate rejects a correct host. Ten providers registered
    /// <c>ISnapshotStore</c> and one attested; the per-contract arm above saw "provided" and the other nine
    /// were invisible.
    /// </para>
    /// <para>
    /// The unit is the PACKAGE rather than the file, because a provider legitimately splits registration
    /// across files (a builder extension and a service-collection extension), and because the alias shape
    /// varies — a store may be registered through the seam in one file and re-exposed under a provider key in
    /// another. Asking "does this package attest the contract it registers?" is robust to both, and it is the
    /// same unit a consumer chooses when they pick a provider.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryProviderRegisteringAGatedContract_EmitsItsOwnMarker()
    {
        var unattested = ScanProvidersMissingTheirMarker();

        unattested.ShouldBeEmpty(
            "These provider packages register a tenant-owned store contract but emit no capability marker "
            + "for it, so AddMultiTenancy(RowDiscriminator) throws at startup for every host that selects "
            + "them — including hosts whose store scopes by tenant correctly. Fix by registering the store "
            + "through AddTenantAwareStore<TContract, TStore>, which emits the marker inseparably from the "
            + "wiring it attests. Do NOT silence this by adding an entry to UnattestedByDesign unless the "
            + "store genuinely implements no tenancy mechanism — a marker on a store that does not scope "
            + "turns a startup refusal into a silent cross-tenant leak. Unattested provider(s): "
            + string.Join(", ", unattested) + ".");
    }

    /// <summary>
    /// COUPLING ARM for <see cref="UnattestedByDesign"/>. Each exempted provider is exempt only because its
    /// store implements no tenancy mechanism; this arm re-establishes that premise from the source instead of
    /// trusting the comment next to the entry.
    /// </summary>
    /// <remarks>
    /// A standing exemption whose justification has silently expired is worse than no exemption: it reads as
    /// a considered decision while covering a live defect. If one of these stores gains an
    /// <c>ITenantContext</c> — someone implementing the tenancy it is tracked for — this arm goes RED and
    /// forces the exemption to be removed rather than letting the provider stay unattested behind cover
    /// written when the claim was still true.
    /// </remarks>
    [Fact]
    public void TheExemptedProviders_StillImplementNoTenancyMechanism()
    {
        var srcRoot = Path.Combine(TestHelpers.GetRepositoryRoot(), "src");

        foreach (var (package, contract) in UnattestedByDesign)
        {
            var packageDirectory = Directory
                .EnumerateDirectories(srcRoot, package, SearchOption.AllDirectories)
                .FirstOrDefault();

            packageDirectory.ShouldNotBeNull(
                $"Exempted package '{package}' no longer exists under src/. Remove its "
                + "UnattestedByDesign entry — an exemption for a package that is gone is dead cover.");

            var tenantAware = Directory
                .EnumerateFiles(packageDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Any(path => StripCommentsAndStringLiterals(File.ReadAllText(path))
                    .Contains("ITenantContext", StringComparison.Ordinal));

            tenantAware.ShouldBeFalse(
                $"'{package}' is exempted from presenting a {contract} marker on the grounds that its store "
                + "implements NO tenancy mechanism, but its source now references ITenantContext. Either the "
                + "store became tenant-aware — in which case remove the exemption and register it through "
                + "AddTenantAwareStore, since it is now a correct host the gate would wrongly refuse — or the "
                + "reference is incidental and this arm needs a narrower premise. Do not leave the exemption "
                + "standing on a justification that has expired.");
        }
    }

    /// <summary>
    /// NON-VACUITY SELF-TEST for the per-provider scan. A green arm proves nothing until it has been watched
    /// going RED, and the honest way to watch it is against synthetic text rather than by mutating real
    /// provider files another agent may be holding uncommitted.
    /// </summary>
    [Fact]
    public void ThePerProviderScan_CatchesAnUnattestedProvider_AndIgnoresAliasesAndProse()
    {
        // A provider that registers the contract and never attests it — the defect this arm exists to catch.
        ExtractProviderRegistrations(
                "services.TryAddKeyedSingleton<IEventStore>(\"oracle\", (sp, _) => new OracleEventStore());")
            .ShouldBe(
                ["IEventStore"],
                "A keyed provider registration must be seen. If this is empty the arm reports every provider "
                + "as clean and enforces nothing.");

        // The keyed-"default" forwarding alias promises the contract without providing a store, and the
        // runtime sweep skips it for the same reason. Counting it would demand a marker of a store the host
        // never registered — the inverse defect, a gate rejecting a correct host.
        ExtractProviderRegistrations(
                "services.TryAddKeyedSingleton<IEventStore>(\"default\", (sp, _) => sp.GetRequiredKeyedService<IEventStore>(\"oracle\"));")
            .ShouldBeEmpty("the keyed-\"default\" forwarding alias must NOT count as a store registration.");

        // The two-generic keyed-"default" form. This one ESCAPED the first version of the scan: the tail
        // began at " TEventStore>(..." rather than at the parenthesis, so the alias check never fired and the
        // core event-sourcing package was reported as a defective provider. It registers whatever store the
        // consumer supplies; the tenancy belongs to that store's own package, which attests it there.
        ExtractProviderRegistrations(
                "_ = Services.AddKeyedSingleton<IEventStore, TEventStore>(\"default\");")
            .ShouldBeEmpty(
                "the two-generic keyed-\"default\" form must be recognised as a forwarding alias. Matching it "
                + "reports the core package as an unattested provider, and the only way to make that green "
                + "would be to attest a store the package does not implement.");

        // The same form under a PROVIDER key is a real registration and must still be seen — the alias skip
        // must key on "default", not on the shape.
        ExtractProviderRegistrations(
                "services.AddSingleton<ISnapshotStore, DynamoDbSnapshotStore>();")
            .ShouldBe(
                ["ISnapshotStore"],
                "the two-generic non-keyed form is a real store registration and must be seen.");

        // Prose and string literals must not register anything. The dead-letter extension documents the wrong
        // wiring in a comment AND quotes it inside an exception message; a scanner matching either would
        // report Excalibur.Dispatch — which registers no dead-letter store at all — as a defective provider,
        // and the "fix" for that phantom would be to attest a store that does not exist.
        ExtractProviderRegistrations(
                "// (TryAddSingleton<IDeadLetterQueue, NullDeadLetterQueue>), which the container cannot activate")
            .ShouldBeEmpty("a line comment must not count as a registration.");
        ExtractProviderRegistrations(
                "throw new InvalidOperationException(\"services.AddSingleton<IDeadLetterQueue>(x); each discarded \");")
            .ShouldBeEmpty("a string literal must not count as a registration.");

        // A saga registration spelled with its full namespace, which is how the core package writes it to
        // avoid a using that would collide with its own types. The first version of this scan compared the
        // captured spelling as a string, so this read as a different contract and the package that DOES
        // attest was reported as unattested — a false finding aimed at correct code.
        ExtractProviderRegistrations(
                "services.TryAddKeyedSingleton<Excalibur.Dispatch.Messaging.ISagaStore>(\"inmemory\", (sp, _) => sp.GetRequiredService<InMemorySagaStore>());")
            .ShouldBe(
                ["ISagaStore"],
                "a namespace-qualified saga registration must normalise to its simple name and be seen.");

        // The NON-keyed forwarding alias: keyless, so the first-argument anchor above cannot see it, and
        // identifiable only by what its factory resolves. The core outbox package registers exactly this,
        // and only when a provider already backs it. Counting it would demand an IOutboxStore marker of a
        // package that owns no outbox store and has no mechanism to attest.
        ExtractProviderRegistrations(
                "services.TryAddSingleton<Excalibur.Dispatch.IOutboxStore>(sp => sp.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>(\"default\"));")
            .ShouldBeEmpty("the non-keyed forwarding alias must NOT count as a store registration.");

        // ...and the skip must key on the default-key forward, not on the mere presence of a keyed resolve:
        // a provider-keyed alias over the provider's own store is a real registration.
        ExtractProviderRegistrations(
                "services.AddKeyedSingleton<IOutboxStore>(\"elasticsearch\", (sp, _) => sp.GetRequiredService<ElasticsearchOutboxStore>());")
            .ShouldBe(
                ["IOutboxStore"],
                "a provider-keyed outbox registration is a real store registration and must be seen.");

        // A near-miss contract must not be swallowed by the outbox alternation. ICloudNativeOutboxStore is
        // a DIFFERENT contract with its own gate, so matching it as IOutboxStore would attribute a marker
        // to the wrong contract and report a change-feed provider as clean on a gate it never satisfied.
        ExtractProviderRegistrations(
                "builder.Services.TryAddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<CosmosDbOutboxStore>());")
            .ShouldBeEmpty("ICloudNativeOutboxStore must not be read as IOutboxStore.");

        // And the real tree is not vacuously empty: the scan must actually be finding registrations to compare.
        ScanProviderRegistrationsByPackage().Count.ShouldBeGreaterThanOrEqualTo(
            8,
            "Expected many shipped packages to register one of these contracts. If this collapses the scan "
            + "matched nothing and the per-provider arm passes trivially.");
    }

    /// <summary>
    /// Provider packages that register a contract in <see cref="PerProviderContracts"/> without emitting a
    /// marker for it, excluding the justified exemptions. Each entry is rendered "Package (IContract)".
    /// </summary>
    private static List<string> ScanProvidersMissingTheirMarker()
    {
        var registrations = ScanProviderRegistrationsByPackage();
        var markers = ScanMarkerEmissionsByPackage();

        return registrations
            .SelectMany(entry => entry.Value
                .Where(contract => !markers.GetValueOrDefault(entry.Key, []).Contains(contract))
                .Where(contract => !string.Equals(
                    UnattestedByDesign.GetValueOrDefault(entry.Key), contract, StringComparison.Ordinal))
                .Select(contract => $"{entry.Key} ({contract})"))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Maps each provider package to the covered contracts it registers a store for.</summary>
    private static Dictionary<string, HashSet<string>> ScanProviderRegistrationsByPackage() =>
        ScanSourceByPackage(text => ExtractProviderRegistrations(text));

    /// <summary>Maps each provider package to the contracts it emits a capability marker for.</summary>
    private static Dictionary<string, HashSet<string>> ScanMarkerEmissionsByPackage() =>
        ScanSourceByPackage(text => ExtractProvidedContracts(StripCommentsAndStringLiterals(text)));

    /// <summary>
    /// Walks <c>src/</c>, grouping the result of <paramref name="extract"/> by owning package directory.
    /// </summary>
    private static Dictionary<string, HashSet<string>> ScanSourceByPackage(Func<string, HashSet<string>> extract)
    {
        var srcRoot = Path.Combine(TestHelpers.GetRepositoryRoot(), "src");

        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        var byPackage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(path))
            {
                continue;
            }

            var package = OwningPackage(srcRoot, path);
            if (package is null)
            {
                continue;
            }

            var found = extract(File.ReadAllText(path));
            if (found.Count == 0)
            {
                continue;
            }

            if (!byPackage.TryGetValue(package, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                byPackage[package] = set;
            }

            set.UnionWith(found);
        }

        return byPackage;
    }

    /// <summary>
    /// The package directory owning <paramref name="path"/> — the second segment under <c>src/</c>
    /// (<c>src/&lt;area&gt;/&lt;Package&gt;/…</c>), or <see langword="null"/> when the file sits above one.
    /// </summary>
    private static string? OwningPackage(string srcRoot, string path)
    {
        var segments = Path
            .GetRelativePath(srcRoot, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 3 ? segments[1] : null;
    }

    /// <summary>True for a file under an <c>obj/</c> or <c>bin/</c> build-output directory.</summary>
    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return segments.Any(segment =>
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The covered contracts a store is registered for in <paramref name="text"/>, skipping the
    /// keyed-"default" forwarding aliases. Shared by the real scan and its self-test so the non-vacuity proof
    /// exercises the SAME extraction production uses.
    /// </summary>
    private static HashSet<string> ExtractProviderRegistrations(string text)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ProviderStoreRegistration.Matches(StripCommentsAndStringLiterals(text)))
        {
            // Anchored to the FIRST argument: a provider registration may legitimately
            // mention the "default" key later in its factory body (an alias forwarding onto
            // it), and skipping on a bare Contains would drop that real registration.
            var tail = match.Groups["tail"].Value;
            if (KeyedDefaultAliasCall.IsMatch(tail) || NonKeyedDefaultForwardingAlias.IsMatch(tail))
            {
                continue;
            }

            var contract = NormalizeContract(match.Groups["contract"].Value);
            if (PerProviderContracts.Contains(contract, StringComparer.Ordinal))
            {
                _ = found.Add(contract);
            }
        }

        return found;
    }

    /// <summary>
    /// Removes comments, and blanks the string literals that could be mistaken for code, so neither
    /// documentation prose nor a quoted example counts as a registration.
    /// </summary>
    /// <remarks>
    /// The dead-letter extension does both — it describes the wrong wiring in a comment AND quotes it inside
    /// an exception message — and a scan blind to that reports a package which registers no store at all as a
    /// defective provider. Literals that are plain DI keys survive, because blanking every literal would make
    /// a forwarding alias and a keyed provider registration indistinguishable and skip every keyed provider
    /// in the tree: a scanner that sees nothing and reports everything clean.
    /// </remarks>
    private static string StripCommentsAndStringLiterals(string source) =>
        StringLiteral.Replace(
            StripComments(source),
            match => PlainKeyContent.IsMatch(match.Groups["content"].Value) ? match.Value : "\"\"");

    /// <summary>Applies a contract-capturing pattern to arbitrary text. Shared by the scanners and their self-test.</summary>
    private static HashSet<string> ExtractContracts(Regex pattern, string text) =>
        pattern
            .Matches(text)
            .Select(m => NormalizeContract(m.Groups["contract"].Value))
            .Where(contract => !string.Equals(contract, GenericTypeParameter, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Reads the contracts declared tenant-owned in the manifest.</summary>
    private static HashSet<string> ReadManifestedContracts()
    {
        var manifestPath = Path.Combine(TestHelpers.GetRepositoryRoot(), ManifestRelativePath);

        File.Exists(manifestPath).ShouldBeTrue(
            $"Expected the tenant-owned-contract manifest at '{manifestPath}'. If it moved, repoint this " +
            "guard — do not delete it: the manifest is the only declaration of which contracts must be " +
            "tenant-scoped.");

        return ManifestContract
            .Matches(File.ReadAllText(manifestPath))
            .Select(m => NormalizeContract(m.Groups["contract"].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Reads the contracts the gate demands a capability marker for.</summary>
    private static HashSet<string> ReadRequiredContracts()
    {
        var gatePath = Path.Combine(TestHelpers.GetRepositoryRoot(), GateRelativePath);

        File.Exists(gatePath).ShouldBeTrue(
            $"Expected the multi-tenancy gate at '{gatePath}'. If AddMultiTenancy moved, this guard is " +
            "reading a file that no longer constrains anything and must be repointed, not deleted.");

        // Strip comments before scanning: the gate documents itself with prose like
        // "…the RequireTenantScopingCapability<T> call…" (MultiTenancyServiceCollectionExtensions.cs:196,202),
        // and matching that would capture the open generic `T` — a false "unprovided contract" from the gate's
        // OWN documentation. The real RequireTenantScopingCapability<IFooStore>(...) calls are code, not comments,
        // so they survive. `TContract` (the private helper's code-level type parameter) is still filtered below.
        return RequiredContract
            .Matches(StripComments(File.ReadAllText(gatePath)))
            .Select(m => NormalizeContract(m.Groups["contract"].Value))
            .Where(contract => !string.Equals(contract, GenericTypeParameter, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Removes C# block and line comments so a source scan matches code, not documentation prose.</summary>
    private static string StripComments(string source)
    {
        var withoutBlockComments = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlockComments, @"//[^\n]*", string.Empty);
    }

    /// <summary>Reads the contracts for which some shipped provider registers a capability marker.</summary>
    private static HashSet<string> ReadProvidedContracts()
    {
        var repoRoot = TestHelpers.GetRepositoryRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        var gateFullPath = Path.GetFullPath(Path.Combine(repoRoot, GateRelativePath));
        var markerFullPaths = MarkerInterfaceRelativePaths
            .Select(relative => Path.GetFullPath(Path.Combine(repoRoot, relative)))
            .ToList();

        var provided = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory
                     .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
                     // The gate REQUIRES markers; it does not provide them. The interface file DECLARES the
                     // marker; a declaration is not a registration. Counting either would let the gate satisfy
                     // itself.
                     .Where(path => !PathEquals(path, gateFullPath)
                                    && !markerFullPaths.Any(marker => PathEquals(path, marker))))
        {
            provided.UnionWith(ExtractProvidedContracts(File.ReadAllText(path)));
        }

        return provided;
    }

    /// <summary>
    /// Applies every provider-registration seam pattern to arbitrary source text and returns the concrete
    /// contracts a marker is registered for. Shared by <see cref="ReadProvidedContracts"/> and its self-test so
    /// the non-vacuity proof exercises the SAME extraction production uses (S873/S886 — prove the real seam).
    /// </summary>
    private static HashSet<string> ExtractProvidedContracts(string text)
    {
        var provided = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pattern in ProvidedContractPatterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var contract = NormalizeContract(match.Groups["contract"].Value);
                if (!IsGenericTypeParameter(contract))
                {
                    _ = provided.Add(contract);
                }
            }
        }

        return provided;
    }

    /// <summary>
    /// True for a C# generic type-parameter name (<c>TContract</c>, <c>TCapabilityFamily</c>, <c>TService</c>,
    /// <c>TProjection</c>, …) captured from a seam helper's OWN declaration or forwarding call, rather than a
    /// concrete contract. Tenant-owned contracts are interfaces (<c>I</c>-prefixed), so a <c>T</c> followed by
    /// an uppercase letter is unambiguously a generic parameter, never a contract (<c>TenantXxx</c> — second
    /// char lowercase — is not caught).
    /// </summary>
    private static bool IsGenericTypeParameter(string contract) =>
        string.Equals(contract, GenericTypeParameter, StringComparison.Ordinal)
        || (contract.Length >= 2 && contract[0] == 'T' && char.IsUpper(contract[1]));

    /// <summary>
    /// Reduces a captured contract to its simple open-generic name so that
    /// <c>IProjectionStore&lt;object&gt;</c> and <c>IProjectionStore&lt;TProjection&gt;</c> compare equal.
    /// </summary>
    private static string NormalizeContract(string captured)
    {
        var trimmed = captured.Trim();
        var genericMarker = trimmed.IndexOf('<', StringComparison.Ordinal);
        var withoutGenerics = genericMarker >= 0 ? trimmed[..genericMarker] : trimmed;

        // A contract's identity here is its simple name: the manifest declares typeof(IFooStore) and the
        // gate names IFooStore, while a registration site may spell the same type fully qualified to avoid
        // a using that would collide with the package's own types. Comparing the two forms as strings would
        // read the qualified spelling as a different contract, so a package that attests through it would
        // be reported as unattested — a false finding pointing at correct code.
        var lastDot = withoutGenerics.LastIndexOf('.');
        return lastDot >= 0 ? withoutGenerics[(lastDot + 1)..] : withoutGenerics;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), right, StringComparison.OrdinalIgnoreCase);
}
