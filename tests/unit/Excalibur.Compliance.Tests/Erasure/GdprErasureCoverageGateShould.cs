// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Erasure.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
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
    // keys on the unverified-coverage condition, not a blanket failure. Resolves the DEFAULT annotation source
    // via real DI (no [PersonalData]-annotated types are loaded in this test process → no annotated-coverage gap).
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

    /// <summary>
    /// Builds a real <see cref="ServiceProvider"/> from the production registration path — GDPR erasure plus
    /// the in-memory erasure store — with the required hashing pepper configured (else ValidateOnStart fails).
    /// Optionally wires the data-inventory discovery source.
    /// </summary>
    private static ServiceProvider BuildProvider(Action<ErasureOptions> configure, bool withDiscovery = false)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

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
}
