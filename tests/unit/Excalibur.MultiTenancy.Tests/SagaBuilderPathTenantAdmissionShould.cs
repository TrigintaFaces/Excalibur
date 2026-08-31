// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Saga.CosmosDb;
using Excalibur.Saga.Oracle.DependencyInjection;
using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.DependencyInjection;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Runtime lock on the saga registration paths that scoped by tenant and declared nothing: a host wired
/// through the production path is <b>admitted</b> by row-discriminator multi-tenancy, presents the scoped
/// capability, and resolves a store that still works afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> Every saga store reads the ambient tenant on every load and save — the tenant term
/// is what confines the read — and six registration paths handed it over through a bare
/// <c>TryAddSingleton</c> that emitted no capability marker. <c>AddMultiTenancy(RowDiscriminator)</c>
/// therefore threw at startup for a store doing exactly what the gate requires. That is a gate rejecting a
/// correct host, not a leak: the safety property held perfectly and the liveness property was broken, which
/// is the failure a suite of safety-only arms is structurally incapable of seeing — every one of them
/// asserts a refusal.
/// </para>
/// <para>
/// <b>Why the builder path hid.</b> Each of these packages has a sibling <c>IServiceCollection</c> entry
/// point that was already folded onto the seam, so a package-level audit reports the package as attesting.
/// The package does call the seam — just not on this path. Fixing one site of a pair looks done and is not,
/// which is why these arms name the ENTRY POINT rather than the provider.
/// </para>
/// <para>
/// <b>Both mechanisms are asserted, not just the presence of one.</b> An ambient-scoped store must present
/// <see cref="ITenantScopingCapability{TContract}"/> and must NOT present
/// <see cref="ITenantPartitionedCapability{TContract}"/>: the two attest different mechanisms, and for a
/// store that reads the ambient tenant only the first is true. Presenting the other would admit it to a gate
/// whose mechanism it does not implement.
/// </para>
/// <para>
/// <b>Real container, production path, no infrastructure.</b> Each arm builds a real
/// <see cref="ServiceProvider"/> through <c>AddExcalibur</c> to <c>AddSagas</c> to the provider verb, and
/// resolves from it. A lock that registered the marker itself would prove only that the gate reads a marker
/// it was handed — precisely how the original lying-marker defect passed a full CI run. No SQL Server is
/// needed: the store captures its connection factory without opening it.
/// </para>
/// <para>
/// <b>What turns this red.</b> Change either path's <c>AddTenantAwareStore</c> back to a bare
/// <c>TryAddSingleton</c> and the admission arm throws, because the marker disappears with the seam that
/// emits it — the attestation cannot be reverted independently of the wiring it describes.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SagaBuilderPathTenantAdmissionShould
{
    /// <summary>Never opened: the store captures its connection factory lazily.</summary>
    private const string UnusedConnectionString =
        "Server=(localdb)\\ExcaliburUnused;Database=sagas_unused;Trusted_Connection=True;";

    /// <summary>Never opened.</summary>
    private const string UnusedOracleConnectionString =
        "User Id=unused;Password=unused;Data Source=localhost:1521/unused;";

    /// <summary>Never contacted.</summary>
    private const string UnusedCosmosConnectionString =
        "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

    /// <summary>Never contacted.</summary>
    private const string UnusedServiceUrl = "http://localhost:8000";

    // ---- LIVENESS: the real gate ADMITS the real provider. This is the bead. --------------------------

    [Fact]
    public void AdmitTheSqlServerSagaStore_WiredThroughTheBuilderPath_UnderRowDiscriminator()
    {
        var services = BuildSqlServerSagaHostViaBuilder();

        // Reaching past this line is the assertion. Before the fix this threw, so a consumer who wired
        // sagas through the documented builder verb could not turn on row-discriminator multi-tenancy at
        // all — for a store whose every statement already carries the ambient tenant term.
        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must ADMIT a correctly wired SQL Server saga store. The builder path hands the "
            + "ambient ITenantContext to the store and the store applies it on every load and save, which is "
            + "exactly the mechanism ITenantScopingCapability attests. Rejecting it is the gate refusing a "
            + "correct host, and that is invisible to every safety-only arm on this contract.");
    }

    [Fact]
    public void AdmitTheInMemorySagaStore_WiredThroughTheProductionPath_UnderRowDiscriminator()
    {
        var services = BuildInMemorySagaHost();

        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must ADMIT the in-memory saga store. It takes an ITenantContext and projects "
            + "the ambient tenant into its partition key, so it implements the mechanism the gate requires. "
            + "This registration was missing from the issue entirely and is shipped like any other.");
    }

    [Fact]
    public void ResolveTheSqlServerSagaStore_WithTheAmbientTenantWired_AfterTheGateAdmitsIt()
    {
        var services = BuildSqlServerSagaHostViaBuilder();
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // Resolved through the real container rather than asserted from a descriptor: a registration that
        // cannot be constructed satisfies a descriptor scan and still fails the consumer at runtime.
        // The gate decorates the keyed saga stores, so what a consumer resolves is the fail-closed wrapper.
        // Asserting that is the liveness half of admission: the host was not merely allowed to start, the
        // isolation it was admitted for was actually wired onto the store it will use.
        _ = provider.GetRequiredKeyedService<ISagaStore>("sqlserver").ShouldBeOfType<TenantScopedSagaStore>(
            "After admission the keyed saga store must resolve as the fail-closed tenant decorator. A host "
            + "that is admitted and then handed an undecorated store has been let past the gate without "
            + "receiving the isolation the gate admitted it for.");

        // ...and the provider's own store, which the seam registers by its concrete type, must hold the
        // tenant field the attestation is about. An attested store built WITHOUT the context is the
        // lying-marker defect: it reads unscoped on every request while the gate reports the host as safe.
        var store = provider.GetRequiredService<SqlServerSagaStore>();
        TenantContextOf(store).ShouldNotBeNull(
            "The admitted saga store must have received the ambient ITenantContext. That field is the sole "
            + "switch that turns row-level tenant isolation on: a null context yields TenantScope.Untenanted "
            + "— no predicate, no column, no parameter — so tenant B's scoped read would see tenant A's saga "
            + "while the capability marker still attested the store as tenant-aware.");
    }

    [Fact]
    public void ResolveTheInMemorySagaStore_WithTheAmbientTenantWired_AfterTheGateAdmitsIt()
    {
        var services = BuildInMemorySagaHost();
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // The concrete in-memory store is internal to its own package, so this arm asserts what a consumer
        // can actually observe: the keyed contract resolves, and it resolves as the fail-closed decorator
        // the gate wires on admission. Widening the store to public so a test could name it would be a
        // production visibility change made for a test's convenience.
        _ = provider.GetRequiredKeyedService<ISagaStore>("inmemory").ShouldBeOfType<TenantScopedSagaStore>(
            "After admission the keyed in-memory saga store must resolve as the fail-closed tenant "
            + "decorator, not as an undecorated store.");
    }

    // ---- The other four builder paths: same defect, same fix, each with its own registration call ----

    /// <summary>
    /// The remaining builder verbs, driven through their real registration path. Each moved from a bare
    /// <c>TryAddSingleton</c> to the seam, which changes how the store is CONSTRUCTED as well as what is
    /// attested — so compiling is not evidence the container can still build it, and admission is not
    /// evidence the store resolves.
    /// </summary>
    [Theory]
    [InlineData(SagaProvider.Oracle)]
    [InlineData(SagaProvider.CosmosDb)]
    [InlineData(SagaProvider.DynamoDb)]
    [InlineData(SagaProvider.Firestore)]
    public void AdmitAndAttestEachRemainingBuilderPath_UnderRowDiscriminator(SagaProvider provider)
    {
        var services = BuildSagaHost(provider);

        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            $"RowDiscriminator must ADMIT the {provider} saga builder path. Its store takes the ambient "
            + "ITenantContext and applies it on every load and save, so refusing it is the gate rejecting a "
            + "correct host — the failure every safety-only arm on this contract is blind to.");

        using var built = services.BuildServiceProvider();

        _ = built.GetRequiredService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull(
            $"The {provider} saga builder path must present ITenantScopingCapability<ISagaStore>, emitted by "
            + "AddTenantAwareStore inseparably from the store registration.");
    }

    /// <summary>
    /// The resolve half, for the providers whose builder path can actually construct a store in a unit test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from the admission arm above because two providers cannot reach construction here, for
    /// reasons that have nothing to do with this attestation and must not be papered over by dropping the
    /// assertion for all four:
    /// </para>
    /// <para>
    /// <b>Cosmos DB</b> — its builder registers a <c>CosmosClient</c> from the supplied connection but never
    /// writes that connection into <c>CosmosDbSagaOptions</c>, and its validator has no builder-connection
    /// escape hatch, so resolving the store throws <c>OptionsValidationException</c>. That is a real,
    /// separate, pre-existing defect in that builder — the failure is in options resolution, upstream of how
    /// the store is registered — and it is tracked on its own rather than worked around here.
    /// </para>
    /// <para>
    /// <b>Firestore</b> — <c>FirestoreDb.Create</c> resolves Google Application Default Credentials, so the
    /// arm would pass or fail on whether the machine running it happens to have cloud credentials. A test
    /// whose result depends on ambient machine state is worse than an absent one.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(SagaProvider.Oracle, "oracle")]
    [InlineData(SagaProvider.DynamoDb, "dynamodb")]
    public void ResolveEachConstructibleBuilderPath_AfterTheGateAdmitsIt(
        SagaProvider provider,
        string providerKey)
    {
        var services = BuildSagaHost(provider);
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var built = services.BuildServiceProvider();

        _ = built.GetRequiredKeyedService<ISagaStore>(providerKey).ShouldBeOfType<TenantScopedSagaStore>(
            $"The {provider} saga store must still RESOLVE after the move onto the seam, and must resolve as "
            + "the fail-closed decorator. A registration that satisfies a descriptor scan and then cannot be "
            + "constructed is the same class of defect as the missing attestation: it passes registration "
            + "and fails the consumer.");
    }

    // ---- The attestation is the RIGHT one: ambient scoping, not row partitioning ----------------------

    [Fact]
    public void AttestAmbientTenantScoping_ForTheSqlServerBuilderPath()
    {
        using var provider = BuildSqlServerSagaHostViaBuilder().BuildServiceProvider();

        _ = provider.GetRequiredService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull(
            "The SQL Server saga builder path must present ITenantScopingCapability<ISagaStore>, emitted by "
            + "AddTenantAwareStore inseparably from the store registration. Without it every host reaching "
            + "the store through this path is refused by RowDiscriminator.");
    }

    [Fact]
    public void NotAttestRowPartitionedTenancy_ForTheSqlServerBuilderPath()
    {
        using var provider = BuildSqlServerSagaHostViaBuilder().BuildServiceProvider();

        provider.GetService<ITenantPartitionedCapability<ISagaStore>>().ShouldBeNull(
            "This store reads the AMBIENT tenant; it does not carry an owning tenant back on the row for a "
            + "caller to re-establish. Presenting the row-partitioned capability would attest a mechanism it "
            + "does not implement and admit it to a gate it cannot satisfy. The seam derives which marker to "
            + "emit from the store's own constructor, so this arm reddens if that derivation is subverted.");
    }

    [Fact]
    public void AttestAmbientTenantScoping_ForTheInMemoryPath()
    {
        using var provider = BuildInMemorySagaHost().BuildServiceProvider();

        _ = provider.GetRequiredService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull(
            "The in-memory saga registration must present ITenantScopingCapability<ISagaStore>.");
    }

    // ---- SAFETY: the gate still refuses a saga store that implements no tenancy mechanism -------------

    [Fact]
    public void StillRefuseASagaStoreThatImplementsNoTenancyMechanism()
    {
        var services = new ServiceCollection();
        _ = services.AddExcalibur(static _ => { });

        // Registered WITHOUT the seam, exactly as the six defective paths did: the contract is present in
        // the collection and nothing attests it.
        _ = services.AddSingleton<ISagaStore>(new TenancyBlindSagaStore());

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "The gate must STILL refuse a saga store that presents no capability. This arm is what keeps the "
            + "admission arms above from being satisfied by a gate that stopped checking: without it, "
            + "deleting the requirement would turn every arm in this file green while removing the "
            + "protection, and a tenancy-blind store would be admitted to serve every tenant's sagas.");
    }

    // ---- Fixtures --------------------------------------------------------------------------------------

    private static ServiceCollection BuildSqlServerSagaHostViaBuilder()
    {
        var services = new ServiceCollection();

        // The store resolves ILogger<T> in its factory, so a host without logging cannot construct it. This
        // is fixture setup, not part of what is under test.
        _ = services.AddLogging();
        _ = services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
            saga.UseSqlServer(sql => sql.ConnectionString(UnusedConnectionString))));
        return services;
    }

    /// <summary>The remaining shipped saga builder verbs, one per provider package.</summary>
    public enum SagaProvider
    {
        /// <summary>Oracle.</summary>
        Oracle,

        /// <summary>Azure Cosmos DB.</summary>
        CosmosDb,

        /// <summary>Amazon DynamoDB.</summary>
        DynamoDb,

        /// <summary>Google Cloud Firestore.</summary>
        Firestore,
    }

    private static ServiceCollection BuildSagaHost(SagaProvider provider)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        // Each verb is configured with an endpoint that is never contacted: these stores capture their
        // client or connection factory without connecting, so the registration path is exercised for real
        // while the arm stays a unit test.
        switch (provider)
        {
            case SagaProvider.Oracle:
                _ = services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
                    saga.UseOracle(oracle => oracle.ConnectionString(UnusedOracleConnectionString))));
                break;
            case SagaProvider.CosmosDb:
                _ = services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
                    saga.UseCosmosDb(cosmos => cosmos.ConnectionString(UnusedCosmosConnectionString))));
                break;
            case SagaProvider.DynamoDb:
                _ = services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
                    saga.UseDynamoDb(dynamo => dynamo.ServiceUrl(UnusedServiceUrl))));
                break;
            case SagaProvider.Firestore:
                _ = services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
                    saga.UseFirestore(firestore => firestore.ProjectId("test-project"))));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unhandled saga provider.");
        }

        return services;
    }

    private static ServiceCollection BuildInMemorySagaHost()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddExcalibur(static _ => { });
        _ = services.AddInMemorySagaStore();
        return services;
    }

    /// <summary>
    /// Reads the store's private ambient-tenant field. Deliberately reflective: the field is private
    /// because it is an implementation detail, and widening it to make this arm easier would be a
    /// production visibility change made for a test.
    /// </summary>
    private static object? TenantContextOf(object store) =>
        store
            .GetType()
            .GetField("_tenantContext", BindingFlags.Instance | BindingFlags.NonPublic)
            .ShouldNotBeNull(
                $"'{store.GetType().Name}' no longer has a '_tenantContext' field, so this arm can no longer "
                + "see whether the ambient tenant was wired. Repoint it at the store's current tenant seam — "
                + "do not delete it: it is the only thing standing between an attestation and a lying one.")
            .GetValue(store);

    /// <summary>A saga store with no tenancy mechanism at all — the shape the gate must keep refusing.</summary>
    private sealed class TenancyBlindSagaStore : ISagaStore
    {
        public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
            where TSagaState : SagaState => Task.FromResult<TSagaState?>(null);

        public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
            where TSagaState : SagaState => Task.CompletedTask;
    }
}
