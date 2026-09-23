// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Erasure.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// S887 88xrgq (P0) — independent regression lock (author≠impl, TestsDeveloper) proving the GDPR-erasure
/// AFFIRMATIVE-coverage gate through the REAL DI container (the S873 real-container-resolve bar).
/// <para>
/// The bug: <c>AddGdprErasure(...)</c> wires <see cref="IErasureService"/> but does NOT wire an
/// <c>IDataInventoryService</c> discovery source. A completion certificate is a compliance PROOF, so the
/// absence of discovered-uncovered stores is NOT proof of coverage when discovery never ran. A vacuous gate
/// (or a hand-constructed <c>ErasureService</c> with fakes that supply an inventory service) would certify
/// <c>Completed</c> over UNVERIFIED coverage — silently leaving personal data behind.
/// </para>
/// <para>
/// Every service here is resolved from a real <see cref="ServiceProvider"/> built by the production
/// registration path (<c>AddGdprErasure</c> + <c>AddInMemoryErasureStore</c>), never by hand-constructing
/// the service with mocks. The crypto is real, in-process (no Docker); the lock runs unconditionally.
/// </para>
/// <para>
/// Arms follow the safety∧liveness discipline (testing-patterns §3): each safety arm is paired with a
/// liveness arm so a blanket-fail gate cannot pass. Runtime gate (arms 1/2) and startup fail-fast
/// (arms 3/4/5) are the two independent defenses.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GdprErasureCoverageGateShould
{
    private const string TestPepper = "test-pepper-0123456789abcdef0123456789ab";

    // ── Arm 1 (HEADLINE, SAFETY): real AddGdprErasure with NO discovery source and KeyShredOnly=false must
    // NOT certify a Completed erasure — coverage is UNVERIFIED. This is the P0: a Completed certificate over
    // an unverified/empty inventory. RED on the pre-fix code (the UNVERIFIED-coverage error is not appended).
    [Fact]
    public async Task NotCertifyCompletedWhenNoDiscoverySourceIsWiredAndNotKeyShredOnly()
    {
        await using var provider = BuildProvider(o => o.KeyShredOnlyErasure = false);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErasureService>();
        var executor = scope.ServiceProvider.GetRequiredService<IErasureExecutor>();

        var requestId = await ScheduleRequestAsync(service);

        var result = await executor.ExecuteAsync(requestId, CancellationToken.None);

        result.Success.ShouldBeFalse(
            "AddGdprErasure wired no data-inventory discovery source and KeyShredOnly=false, so store-level "
            + "coverage is UNVERIFIED — a Completed erasure certificate must NOT be issued over unverified coverage.");

        var status = await service.GetStatusAsync(requestId, CancellationToken.None);
        status.ShouldNotBeNull();
        status!.Status.ShouldNotBe(
            ErasureRequestStatus.Completed,
            "the request must not reach Completed while coverage is unverified (expected PartiallyCompleted/Failed).");
    }

    // ── Arm 2 (LIVENESS pair): identical wiring but KeyShredOnly=true is a LEGITIMATE completion basis (the
    // per-subject key is still shredded), so the gate must NOT fire — the erasure completes. Proves the gate
    // keys on the unverified-coverage condition, not a blanket failure.
    //
    // CORRECTED: this arm previously claimed it resolved the default annotation source because "no
    // [PersonalData]-annotated types are loaded in this test process". That premise was FALSE — this assembly
    // declares four of them (SubjectFieldCryptorEnvelopeMarkerShould, SubjectFieldCryptorTruncatedEnvelope
    // FailsClosedShould, and two nested types in SensitiveSelectsForEncryptionShould), so the scan returned
    // {General}, nothing covered it, and this arm went red on an input it never set up. BuildProvider now pins
    // the input explicitly rather than depending on which fixtures share the assembly.
    [Fact]
    public async Task CertifyCompletedWhenKeyShredOnlyErasureIsExplicitlyOptedIn()
    {
        await using var provider = BuildProvider(o => o.KeyShredOnlyErasure = true);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErasureService>();
        var executor = scope.ServiceProvider.GetRequiredService<IErasureExecutor>();

        var requestId = await ScheduleRequestAsync(service);

        var result = await executor.ExecuteAsync(requestId, CancellationToken.None);

        result.Success.ShouldBeTrue(
            "KeyShredOnlyErasure=true explicitly accepts key-destruction-only erasure — the coverage gate must "
            + "NOT block completion, otherwise it is a vacuous blanket-fail.");

        var status = await service.GetStatusAsync(requestId, CancellationToken.None);
        status.ShouldNotBeNull();
        status!.Status.ShouldBe(ErasureRequestStatus.Completed);
    }

    // ── Arm 3 (FAIL-FAST, SAFETY): the startup guard rejects a no-discovery + not-KeyShredOnly registration
    // at host start (defense-in-depth with the runtime gate). RED on a validator whose throw is a no-op.
    [Fact]
    public async Task FailStartupWhenNoDiscoverySourceIsWiredAndNotKeyShredOnly()
    {
        await using var provider = BuildProvider(o => o.KeyShredOnlyErasure = false);
        var validator = ResolveDiscoveryValidator(provider);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));
    }

    // ── Arm 4 (FAIL-FAST, LIVENESS): KeyShredOnly=true is a valid registration — startup must NOT fail.
    [Fact]
    public async Task StartCleanlyWhenKeyShredOnlyErasureIsExplicitlyOptedIn()
    {
        await using var provider = BuildProvider(o => o.KeyShredOnlyErasure = true);
        var validator = ResolveDiscoveryValidator(provider);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    // ── Arm 5 (FAIL-FAST, LIVENESS): a discovery source IS wired (AddDataInventoryService) with
    // KeyShredOnly=false — the coverage is verifiable, so startup must NOT fail.
    [Fact]
    public async Task StartCleanlyWhenADiscoverySourceIsWired()
    {
        await using var provider = BuildProvider(o => o.KeyShredOnlyErasure = false, withDiscovery: true);
        var validator = ResolveDiscoveryValidator(provider);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    // ARM (SAFETY): the registry cannot be READ. Distinct from an empty registry and refused for the
    // opposite reason -- an empty registry is a legitimate first boot, a store that throws is never one.
    // Without this arm the refusal is unbound: the suite passes whether the throw is present or absent,
    // because nothing else in it drives an unreadable store. A fail-closed property that holds only by
    // accident of structure is a latent fail-open.
    [Fact]
    public async Task FailStartupWhenTheRegistryCannotBeRead()
    {
        await using var provider = BuildProvider(
            o => o.KeyShredOnlyErasure = false,
            withDiscovery: true,
            registryReadFails: true);
        var validator = ResolveDiscoveryValidator(provider);

        var refusal = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        // The message must not claim the registry is EMPTY -- that is the absence-of-evidence error this
        // guard exists to prevent, and reporting "nothing is registered" for "I could not tell" commits it.
        refusal.Message.ShouldContain("could not be READ");
        refusal.InnerException.ShouldNotBeNull();
    }

    // ARM (LIVENESS): an EMPTY registry must NOT fail startup. Paired with the arm above so the two halves
    // of the ruled matrix are bound separately -- a guard that refused both would satisfy the safety arm
    // and deadlock every first boot, which is the failure the placement ruling exists to prevent.
    [Fact]
    public async Task StartCleanlyWhenTheRegistryIsMerelyEmpty()
    {
        await using var provider = BuildProvider(o => o.KeyShredOnlyErasure = false, withDiscovery: true);
        var validator = ResolveDiscoveryValidator(provider);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    /// <summary>
    /// Builds a real <see cref="ServiceProvider"/> from the production registration path — GDPR erasure plus
    /// the in-memory erasure store — with the required hashing pepper configured (else ValidateOnStart fails).
    /// Optionally wires the data-inventory discovery source.
    /// </summary>
    private static ServiceProvider BuildProvider(
        Action<ErasureOptions> configure,
        bool withDiscovery = false,
        bool registryReadFails = false)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        // Registered BEFORE AddGdprErasure so it wins the TryAdd: a store whose registry read throws.
        if (registryReadFails)
        {
            services.TryAddSingleton<IDataInventoryStore>(new UnreadableRegistryStore());
        }

        // Pin the annotated-coverage input BEFORE AddGdprErasure, whose registration is TryAdd. Left to the
        // production default this resolves the whole-AppDomain assembly scan, which inside a test process
        // reports this assembly's own [PersonalData] fixture properties (all bare, so PersonalDataCategory
        // .General) as annotated domain categories no discovered location covers. These arms are about the
        // UNVERIFIED-coverage and key-shred-only conditions, so the annotated gate is pinned empty and cannot
        // contribute; the annotated gate has its own arms in ErasureCoverageGateShould.
        services.TryAddSingleton(TestAnnotationSource.None);

        // The keyed data-subject hasher fails closed without a pepper — supply one (as a consumer would from
        // a secret manager) so the real store can pseudonymize identifiers.
        _ = services.Configure<DataSubjectHashingOptions>(o => o.Pepper = TestPepper);

        // A completed erasure certificate is HMAC-signed and fails closed without a key — configure a signing
        // key (as a consumer would from a secret manager) so the liveness arms can reach a signed Completed cert.
        _ = services.AddGdprErasure(o =>
        {
            o.Retention.SigningKey = new byte[32];
            configure(o);
        });

        // This host operates no legal holds, and says so rather than leaving anything to infer it.
        // Erasure REQUIRES a legal-hold service -- an absent one made "no holds here" and "nobody wired
        // it" the same observation, and the second silently skipped an irreversible check. This gate is
        // about discovery coverage rather than holds, so it makes the decision explicitly and moves on.
        _ = services.AddNoLegalHolds();
        _ = services.AddInMemoryErasureStore();

        if (withDiscovery)
        {
            _ = services.AddDataInventoryService();
            _ = services.AddInMemoryDataInventoryStore();
        }

        return services.BuildServiceProvider();
    }

    /// <summary>Schedules a real erasure request against the wired store and returns its tracking id.</summary>
    private static async Task<Guid> ScheduleRequestAsync(IErasureService service)
    {
        var request = new ErasureRequest
        {
            DataSubjectId = "user-erasure-coverage-gate",
            IdType = DataSubjectIdType.UserId,
            LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
            RequestedBy = "compliance-admin",
        };

        var scheduled = await service.RequestErasureAsync(request, CancellationToken.None);
        scheduled.Status.ShouldBe(
            ErasureRequestStatus.Scheduled,
            "the request must schedule before execution — ExecuteAsync only runs a Scheduled request.");

        return request.RequestId;
    }

    /// <summary>Resolves the internal startup guard registered as an <see cref="IHostedService"/> by AddGdprErasure.</summary>
    private static ErasureDiscoverySourceValidator ResolveDiscoveryValidator(IServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<ErasureDiscoverySourceValidator>().Single();

    /// <summary>
    /// A data-inventory store whose registry read FAILS. Models an unreachable or misconfigured backing
    /// store -- not an empty one. Every other member throws, because this fixture exists for exactly one
    /// question and a member answering silently would let an arm pass for the wrong reason.
    /// </summary>
    private sealed class UnreadableRegistryStore : IDataInventoryStore
    {
        public Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("registry backing store is unreachable");

        public Task SaveRegistrationAsync(
            DataLocationRegistration registration,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("fixture: registry writes are out of scope for this arm");

        public Task<bool> RemoveRegistrationAsync(
            string tableName,
            string fieldName,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("fixture: registry writes are out of scope for this arm");

        public Task RecordDiscoveredLocationAsync(
            DataLocation location,
            string dataSubjectId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("fixture: discovery writes are out of scope for this arm");

        public object? GetService(Type serviceType) => null;
    }
}
