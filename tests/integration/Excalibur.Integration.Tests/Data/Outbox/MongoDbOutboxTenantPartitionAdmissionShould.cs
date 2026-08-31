// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.MultiTenancy;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure liveness lock: a MongoDB outbox wired through the production registration path is
/// <b>admitted</b> by row-discriminator multi-tenancy, resolves, and then actually <b>serves a read</b> that
/// carries every tenant back out of one estate-wide drain.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists separately from the registration lock.</b> The unit-level attestation arms prove the
/// capability marker is present and the gate admits the host. They cannot prove the mechanism the marker
/// attests is real — that the tenant is genuinely persisted on the document and genuinely handed back on
/// the drain. A registration-time marker is a claim about behaviour that only infrastructure can witness,
/// and this project has already shipped a marker that passed a full CI run while attesting a guarantee the
/// store did not implement. So the claim is settled here, against a real server.
/// </para>
/// <para>
/// <b>The liveness property, stated so it is falsifiable.</b> Two messages staged under two different
/// tenants must both come back from a <em>single</em> drain pass, each carrying its own tenant. That is one
/// assertion doing two jobs. It fails if the tenant is dropped on write or on read — the partitioned
/// mechanism not being real. And it fails if anything scopes this contract to the ambient tenant, because
/// the drain would then read the tenant as absent, claim the empty set, and stall delivery for every tenant
/// while passing any arm that only checks one tenant cannot see another tenant rows. The second failure is
/// the one no safety-only suite can see, and it is why the outbox is on the partitioned seam rather than
/// the ambient-scoped one.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock.</b> Runs against a real MongoDB via TestContainers, through the
/// production <c>AddExcalibur</c> to <c>AddOutbox</c> to <c>UseMongoDB</c> path and a real
/// <see cref="ServiceProvider"/>, resolved with <c>GetRequiredKeyedService</c>. A faked
/// <c>IMongoCollection</c> returns whatever it was told to return and would certify a store that persists
/// no tenant at all. <c>DockerAvailable.ShouldBeTrue(...)</c> makes it NON-SKIPPED: a real-infra arm that
/// passes by being skipped is the gap that ships the bug.
/// </para>
/// <para>
/// <b>RED-on-mutant.</b> Drop the tenant from either half of the round trip — the assignment in
/// <c>MongoDbOutboxDocument.FromMessage</c> or the one in <c>ToOutboundMessage</c> — and the drained
/// tenants no longer match the staged ones. Change the provider registration off
/// <c>AddTenantAwareStore</c> and the host is refused before it ever reaches the drain.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxTenantPartitionAdmissionShould : IClassFixture<MongoDbOutboxStoreContainerFixture>
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private readonly MongoDbOutboxStoreContainerFixture _fixture;

    public MongoDbOutboxTenantPartitionAdmissionShould(MongoDbOutboxStoreContainerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task BeAdmittedUnderRowDiscriminator_AndDrainEveryTenantFromOnePass()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "This is the liveness half of the outbox tenant gate — that a correctly wired host is admitted "
            + "and its drain still carries every tenant. A real-infra arm that passes by being skipped "
            + "proves nothing, and the defect it guards is a delivery stall.");

        await _fixture.CleanupAsync().ConfigureAwait(false);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseMongoDB(mongo => mongo
            .ConnectionString(_fixture.ConnectionString)
            .DatabaseName(_fixture.DatabaseName))));

        // Reaching past this line is the first assertion: before the provider was moved onto the partitioned
        // seam this threw, and no MongoDB host could turn on row-discriminator multi-tenancy at all.
        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must admit a correctly wired MongoDB outbox.");

        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IOutboxStore>("mongodb");

        _ = store.ShouldBeOfType<MongoDbOutboxStore>(
            "The admitted outbox must resolve as the provider own store, undecorated. A tenant-scoping "
            + "wrapper on this contract would read the ambient tenant as absent at drain time and claim the "
            + "empty set.");

        await store.StageMessageAsync(
            new OutboundMessage("test.message", [1], "dest") { TenantId = TenantA },
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        await store.StageMessageAsync(
            new OutboundMessage("test.message", [2], "dest") { TenantId = TenantB },
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        var drained = (await store.GetUnsentMessagesAsync(10, TestContext.Current.CancellationToken)
            .ConfigureAwait(false)).ToList();

        drained.Count.ShouldBe(
            2,
            "One drain pass must carry BOTH tenants. The outbox drain is deliberately estate-wide: the "
            + "processor establishes a per-message scope from the tenant the row carries. A drain that "
            + "returns fewer than the staged set is the stall this seam exists to prevent, and it would look "
            + "perfectly safe to any arm that only asserts one tenant cannot see another.");

        drained.Select(m => m.TenantId)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray()
            .ShouldBe(
                [TenantA, TenantB],
                customMessage: "Each drained message must carry back its OWN tenant. This is the mechanism "
                + "ITenantPartitionedCapability attests — the tenant is persisted on the document and handed "
                + "back on read, so the owning partition is re-established from the row rather than inferred "
                + "from ambient state. If these do not match, the marker attests a guarantee the store does "
                + "not implement.");
    }
}
