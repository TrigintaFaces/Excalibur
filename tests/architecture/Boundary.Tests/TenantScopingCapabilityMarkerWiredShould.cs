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

    /// <summary>The open-generic marker interface. Its declaration is not a registration.</summary>
    private const string MarkerInterfaceRelativePath =
        "src/Dispatch/Excalibur.Dispatch.Abstractions/ContextValues/ITenantScopingCapability.cs";

    /// <summary>Matches <c>RequireTenantScopingCapability&lt;IFooStore&gt;</c>, capturing the contract.</summary>
    private static readonly Regex RequiredContract = new(
        @"RequireTenantScopingCapability<\s*(?<contract>[^>,\s]+)",
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
        // Explicit marker type: ITenantScopingCapability<IFooStore> / TenantScopingCapabilityMarker<IFooStore>.
        new(@"(?:TenantScopingCapabilityMarker|ITenantScopingCapability)<\s*(?<contract>[^>,\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Store seam: AddTenantScopedStore<IFooStore, FooStoreImpl> — the marker is the 1st generic (contract).
        new(@"AddTenantScopedStore<\s*(?<contract>[^>,\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Projection seam: AddTenantScopedProjectionStore<IProjectionStore<T>, IProjectionStore<object>> — the
        // marker is the 2nd generic (the capability family). The 1st arg may itself be generic (no comma inside).
        new(@"AddTenantScopedProjectionStore<\s*[^,]+,\s*(?<contract>[^>\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        // Direct capability seam: AddTenantScopingCapability<IFooErasure> — the marker is the 1st generic. (Not
        // ITenantScopingCapability — the "Add" prefix distinguishes the registration call from the marker type.)
        new(@"AddTenantScopingCapability<\s*(?<contract>[^>,\s]+)",
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
            "services.AddTenantScopedStore<IOutboxStore, PostgresOutboxStore>((sp, ctx) => ...);" +
            "services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, IProjectionStore<object>>((sp, ctx) => ...);" +
            "builder.AddTenantScopingCapability<IEventStoreErasure>(services);" +
            "services.AddSingleton<ITenantScopingCapability<IEventStore>>(new TenantScopingCapabilityMarker<IEventStore>());";

        var provided = ExtractProvidedContracts(providerText);

        // Every seam's concrete contract is recognised — the drift wxbwef fixed (IProjectionStore + IEventStoreErasure).
        provided.ShouldContain("IOutboxStore", "store seam: AddTenantScopedStore<C,..> (1st generic).");
        provided.ShouldContain("IProjectionStore", "projection seam: AddTenantScopedProjectionStore<..,CAP> (2nd generic, capability family).");
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
        var markerFullPath = Path.GetFullPath(Path.Combine(repoRoot, MarkerInterfaceRelativePath));

        var provided = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory
                     .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
                     // The gate REQUIRES markers; it does not provide them. The interface file DECLARES the
                     // marker; a declaration is not a registration. Counting either would let the gate satisfy
                     // itself.
                     .Where(path => !PathEquals(path, gateFullPath) && !PathEquals(path, markerFullPath)))
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
        return genericMarker >= 0 ? trimmed[..genericMarker] : trimmed;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), right, StringComparison.OrdinalIgnoreCase);
}
