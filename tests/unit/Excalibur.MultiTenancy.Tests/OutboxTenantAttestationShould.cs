// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Outbox.Postgres;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Author-independent lock on the capability an outbox provider attests, and on the capability the
/// multi-tenancy gate demands of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> <see cref="ITenantScopingCapability{TContract}"/> attests that a store applies the
/// <em>ambient</em> tenant discriminator to every operation, and the gate treated it as authoritative. The
/// relational outbox providers registered through the seam that emits it while binding the tenant context
/// they were handed to a discard — every one of them with a comment correctly explaining that the store reads
/// no ambient tenant. So the attestation was false by the provider's own account, the gate passed on it, and
/// the two published documentation pages told the consumer the gate had verified ambient tenant honouring.
/// </para>
/// <para>
/// <b>Why the false attestation could not simply be made true.</b> An outbox store that filtered on the
/// ambient tenant would be <em>worse</em>, not better: one drain pass carries every tenant's messages and the
/// processor scopes each individually, so an ambient filter would find no tenant, claim the empty set, and
/// stall the drain permanently — while passing any test that only asserts one tenant cannot see another's
/// rows. The gate was demanding an attestation no correct outbox can truthfully make.
/// </para>
/// <para>
/// <b>What is asserted here.</b> That the provider now attests
/// <see cref="ITenantPartitionedCapability{TContract}"/> — the tenant travels on the row and the owning tenant
/// is re-established from it — that it does <em>not</em> attest ambient scoping, and that the gate demands the
/// former and refuses the latter. The last of those is the arm that fails on the one-token mutation: change
/// the gate's outbox block back to <c>RequireTenantScopingCapability</c> and
/// <see cref="RejectAnOutbox_RegisteredThroughTheAmbientScopedSeam"/> goes red, because the pre-fix
/// registration would be accepted again.
/// </para>
/// <para>
/// <b>Real container, production path, both shapes.</b> Every arm builds a real
/// <see cref="ServiceProvider"/> through <c>AddExcalibur → AddOutbox → UsePostgres</c> and resolves with
/// <c>GetRequiredService</c>. A lock that hand-registered the marker would prove only that the gate reads the
/// marker it was handed — which is exactly how the original defect passed a full CI run. Both supported
/// Postgres connection shapes are covered, because the two take different branches and each has its own
/// registration call.
/// </para>
/// <para>
/// No Postgres is required: the connection-factory shape is given a faked <see cref="IDb"/> and the store
/// captures it without connecting.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class OutboxTenantAttestationShould
{
    /// <summary>Never opened: the store captures its connection inputs lazily.</summary>
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=outbox_unused;Username=unused;Password=unused";

    // ---- SAFETY: the outbox does not attest a capability it does not have ------------------------------

    [Theory]
    [InlineData(PostgresConnectionShape.ConnectionString)]
    [InlineData(PostgresConnectionShape.ConnectionFactory)]
    public void NotAttestAmbientTenantScoping_ForEitherPostgresConnectionShape(PostgresConnectionShape shape)
    {
        using var provider = BuildPostgresOutboxHost(shape).BuildServiceProvider();

        provider.GetService<ITenantScopingCapability<IOutboxStore>>().ShouldBeNull(
            "The Postgres outbox must not present ITenantScopingCapability<IOutboxStore>. That marker "
            + "attests that the store applies the ambient tenant discriminator to every operation, and this "
            + "store deliberately reads no ambient tenant on any path — its drain is estate-wide by design. "
            + "Presenting it is the lying-marker defect: the gate passes and the published documentation "
            + "then describes a verification that did not happen.");
    }

    // ---- LIVENESS: it attests the capability it does have, and the attestation resolves ----------------

    [Theory]
    [InlineData(PostgresConnectionShape.ConnectionString)]
    [InlineData(PostgresConnectionShape.ConnectionFactory)]
    public void AttestRowPartitionedTenancy_ForEitherPostgresConnectionShape(PostgresConnectionShape shape)
    {
        using var provider = BuildPostgresOutboxHost(shape).BuildServiceProvider();

        // Resolved, not merely present as a descriptor: a marker that cannot be constructed would satisfy a
        // descriptor scan and still fail the gate's consumer at runtime.
        _ = provider.GetRequiredService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
            "The Postgres outbox must present ITenantPartitionedCapability<IOutboxStore>, emitted by "
            + "AddTenantAwareStore inseparably from the store registration. Without it the safety arm "
            + "above is satisfied by a provider that attests nothing at all — and RowDiscriminator would "
            + "then reject every host that uses this outbox, correct ones included.");
    }

    // ---- LIVENESS: the real gate admits the real provider, end to end ----------------------------------

    [Fact]
    public void AdmitTheRealPostgresOutbox_AndResolveItUndecorated_UnderRowDiscriminator()
    {
        var services = BuildPostgresOutboxHost(PostgresConnectionShape.ConnectionFactory);

        // Reaching past this line proves the gate did not throw for a correctly wired host.
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IOutboxStore>("postgres");

        // Undecorated, deliberately: a tenant-scoping decorator here would read the ambient tenant as absent
        // at drain time and claim the empty set. The gate's job for this contract is to admit or reject, not
        // to wrap.
        _ = store.ShouldBeOfType<PostgresOutboxStore>(
            "The outbox must resolve as the provider's own store. A tenant-scoping wrapper on this contract "
            + "would stall the cross-tenant drain permanently while looking safe.");
    }

    // ---- SAFETY: the gate has teeth, and they are pointed at the right capability ----------------------

    [Fact]
    public void RejectAnOutbox_ThatAttestsNothing()
    {
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<IOutboxStore>());

        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must reject an outbox store whose provider proves no tenant capability at all. "
            + "If this does not throw, the gate is inert and every arm above passes vacuously.");

        thrown.Message.ShouldContain(
            nameof(IOutboxStore),
            Case.Sensitive,
            "The rejection must name the contract that failed, or a consumer cannot act on it.");
    }

    [Fact]
    public void RejectAnOutbox_RegisteredThroughTheAmbientScopedSeam()
    {
        var services = new ServiceCollection();

        // The pre-fix bug (a factory binding the handed ITenantContext to a discard) is now structurally
        // inexpressible — AddTenantAwareStore's factory overload has no second parameter to discard. What
        // survives to test is the GATE's own rejection: even a store whose constructor genuinely declares
        // ITenantContext (so the seam correctly derives the scoped marker) must still be refused for the
        // outbox contract specifically, because ambient scoping is never correct for it regardless of how
        // truthfully it was attested.
        _ = services.AddDefaultTenantContext();
        _ = services.AddTenantAwareStore<IOutboxStore, AmbientlyAttestedOutboxStore>(
            static sp => new AmbientlyAttestedOutboxStore(sp.GetRequiredService<ITenantContext>()));

        // The seam registers the CONCRETE store, because that is the instance the marker binds to; every
        // shipped provider then maps the CONTRACT to it. Without this line the outbox gate never evaluates
        // -- it asks whether an IOutboxStore registration is present, and there is none -- and the throw
        // below arrives instead from the "no tenant-owned store is registered" fallback. Measured: this arm
        // passed on that fallback message, so it was asserting the gate had teeth while never reaching it.
        _ = services.AddSingleton<IOutboxStore>(
            static sp => sp.GetRequiredService<AmbientlyAttestedOutboxStore>());

        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "The ambient-scoping marker must NOT satisfy the outbox gate. This is the one-token mutation "
            + "arm: if the gate's outbox block is changed back to RequireTenantScopingCapability, or if it "
            + "is relaxed to accept either marker, this registration is admitted again and the outbox is "
            + "back to attesting ambient tenant honouring it does not perform.");

        // And it must be the OUTBOX gate that refused, not an unrelated guard standing in for it. This is
        // the assertion whose absence let the arm pass vacuously.
        thrown.Message.ShouldContain(
            nameof(IOutboxStore),
            Case.Sensitive,
            "the refusal must name the contract whose attestation was rejected.");
    }

    /// <summary>
    /// A store whose constructor genuinely declares <see cref="ITenantContext"/> — so
    /// <c>AddTenantAwareStore</c> correctly, honestly derives the scoped marker for it — used to prove the
    /// outbox gate rejects the scoped marker on its own terms, not because the marker was falsely obtained.
    /// Its bodies are unreachable in this lock (the gate inspects registrations, never calls the store), so
    /// they throw rather than return plausible values.
    /// </summary>
    private sealed class AmbientlyAttestedOutboxStore(ITenantContext tenantContext) : IOutboxStore
    {
        public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable in this lock.");

        public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable in this lock.");

        public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable in this lock.");

        public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable in this lock.");

        public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable in this lock.");
    }

    /// <summary>The two connection shapes <c>UsePostgres</c> supports; each takes its own registration branch.</summary>
    public enum PostgresConnectionShape
    {
        /// <summary>Configured with a connection string.</summary>
        ConnectionString,

        /// <summary>Configured with an <see cref="IDb"/> factory.</summary>
        ConnectionFactory,
    }

    /// <summary>Wires a host through the production Postgres outbox registration path for the given shape.</summary>
    private static ServiceCollection BuildPostgresOutboxHost(PostgresConnectionShape shape)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(A.Fake<IDb>());
        _ = services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UsePostgres(postgres =>
        {
            _ = shape == PostgresConnectionShape.ConnectionString
                ? postgres.ConnectionString(UnusedConnectionString)
                : postgres.ConnectionFactory(static sp => sp.GetRequiredService<IDb>());
        })));

        return services;
    }
}
