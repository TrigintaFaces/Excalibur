// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.ElasticSearch;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Runtime lock on the Elasticsearch outbox, which carries the tenant on every document and declared
/// nothing: a host wired through either registration path is <b>admitted</b> by row-discriminator
/// multi-tenancy, presents the row-partitioned capability, and resolves the provider's own store undecorated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which mechanism this store implements, and why it matters that it is the partitioned one.</b> Staging
/// records the tenant of the message on the document, and every drained message hands that value back, so
/// the owning tenant is re-established from the document rather than read from ambient state. The store
/// takes no <see cref="ITenantContext"/> anywhere. Attesting ambient scoping instead would be worse than the
/// gap it replaced: the drain is deliberately estate-wide — one dispatcher serves every tenant — so a store
/// admitted as ambient-scoped and then wrapped in a scoping decorator would read the tenant as absent, claim
/// the empty set, and stall delivery for every tenant while every safety-only arm still passed.
/// </para>
/// <para>
/// <b>Two registration paths, both asserted.</b> The provider ships a standalone
/// <c>IServiceCollection</c> verb and a fluent builder verb, each with its own registration call. Fixing one
/// leaves the other refused while looking done, and a package-level audit reports the package as attesting
/// either way.
/// </para>
/// <para>
/// <b>What this does NOT prove.</b> That the writes actually populate the discriminator. That is observable
/// only against real infrastructure and belongs to the conformance round-trip, not to a registration-time
/// marker. What is asserted here is that the gate admits the host and that the store it admits is the
/// provider's own, undecorated.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ElasticsearchOutboxTenantAttestationShould
{
    /// <summary>Never contacted: the store captures its client without connecting.</summary>
    private static readonly Uri UnusedNode = new("http://localhost:9200");

    // ---- LIVENESS: the real gate ADMITS the real provider. This is the bead. --------------------------

    [Theory]
    [InlineData(ElasticsearchRegistrationPath.ServiceCollection)]
    [InlineData(ElasticsearchRegistrationPath.Builder)]
    public void AdmitTheElasticsearchOutbox_UnderRowDiscriminator_ForEitherRegistrationPath(
        ElasticsearchRegistrationPath path)
    {
        var services = BuildElasticsearchOutboxHost(path);

        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must ADMIT a correctly wired Elasticsearch outbox. This provider records the "
            + "tenant on the document and re-establishes it on drain, which is exactly the mechanism "
            + "ITenantPartitionedCapability attests. Rejecting it is the gate refusing a correct host, and "
            + "that is invisible to every safety-only arm on this contract because they all assert a refusal.");
    }

    [Theory]
    [InlineData(ElasticsearchRegistrationPath.ServiceCollection)]
    [InlineData(ElasticsearchRegistrationPath.Builder)]
    public async Task ResolveTheElasticsearchOutboxUndecorated_AfterTheGateAdmitsIt(
        ElasticsearchRegistrationPath path)
    {
        var services = BuildElasticsearchOutboxHost(path);
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        // Disposed asynchronously: this arm constructs the store, which is IAsyncDisposable, so a
        // synchronous container dispose throws.
        await using var provider = services.BuildServiceProvider();

        // Resolved through the real container rather than asserted from a descriptor: a registration that
        // cannot be constructed satisfies a descriptor scan and still fails the consumer at runtime.
        var store = provider.GetRequiredKeyedService<IOutboxStore>("elasticsearch");

        _ = store.ShouldBeOfType<ElasticsearchOutboxStore>(
            "The outbox must resolve as the provider's own store. A tenant-scoping wrapper on this contract "
            + "would read the ambient tenant as absent at drain time, claim the empty set, and stall "
            + "delivery for every tenant while looking safe.");

        // The keyed default alias is what a host without an explicit key resolves, so it must reach the
        // same admitted store rather than a second, unattested registration.
        provider.GetRequiredKeyedService<IOutboxStore>("default").ShouldBeSameAs(
            store,
            "The default outbox alias must resolve to the same admitted store instance.");
    }

    // ---- The attestation is the RIGHT one: row partitioning, not ambient scoping ----------------------

    [Theory]
    [InlineData(ElasticsearchRegistrationPath.ServiceCollection)]
    [InlineData(ElasticsearchRegistrationPath.Builder)]
    public void AttestRowPartitionedTenancy_ForEitherRegistrationPath(ElasticsearchRegistrationPath path)
    {
        using var provider = BuildElasticsearchOutboxHost(path).BuildServiceProvider();

        _ = provider.GetRequiredService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
            "The Elasticsearch outbox must present ITenantPartitionedCapability<IOutboxStore>, emitted by "
            + "AddTenantAwareStore inseparably from the store registration. Without it every host using "
            + "this provider is refused by RowDiscriminator.");
    }

    [Theory]
    [InlineData(ElasticsearchRegistrationPath.ServiceCollection)]
    [InlineData(ElasticsearchRegistrationPath.Builder)]
    public void NotAttestAmbientTenantScoping_ForEitherRegistrationPath(ElasticsearchRegistrationPath path)
    {
        using var provider = BuildElasticsearchOutboxHost(path).BuildServiceProvider();

        provider.GetService<ITenantScopingCapability<IOutboxStore>>().ShouldBeNull(
            "This store carries the tenant on the document and reads no ambient tenant on any path. "
            + "Attesting ambient scoping would claim a mechanism it does not implement, and on THIS "
            + "contract that claim is what would stall the estate-wide drain for every tenant. The seam "
            + "derives the marker from the store's own declaration, so this arm reddens if that is subverted.");
    }

    // ---- Fixtures --------------------------------------------------------------------------------------

    /// <summary>The two shipped registration entry points, each with its own registration call.</summary>
    public enum ElasticsearchRegistrationPath
    {
        /// <summary>The standalone <c>AddElasticsearchOutboxStore</c> service-collection verb.</summary>
        ServiceCollection,

        /// <summary>The fluent <c>AddOutbox(o =&gt; o.UseElasticSearch(...))</c> builder verb.</summary>
        Builder,
    }

    private static ServiceCollection BuildElasticsearchOutboxHost(ElasticsearchRegistrationPath path)
    {
        var services = new ServiceCollection();

        // The store resolves ILogger<T> during construction, so a host without logging cannot build it.
        // Fixture setup, not part of what is under test.
        _ = services.AddLogging();

        if (path == ElasticsearchRegistrationPath.Builder)
        {
            _ = services.AddExcalibur(x => x.AddOutbox(outbox =>
                outbox.UseElasticSearch(es => es.NodeUri(UnusedNode).IndexName("outbox-unused"))));
        }
        else
        {
            _ = services.AddExcalibur(static _ => { });
            _ = services.AddSingleton(new Elastic.Clients.Elasticsearch.ElasticsearchClient(UnusedNode));
            _ = services.AddElasticsearchOutboxStore(o => o.IndexName = "outbox-unused");
        }

        return services;
    }
}
