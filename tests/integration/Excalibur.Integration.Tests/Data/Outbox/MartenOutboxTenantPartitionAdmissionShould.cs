// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.MultiTenancy;
using Excalibur.Outbox.Marten;

using global::Marten;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure lock on the Marten outbox's tenancy mechanism: a host wired through the production
/// registration path is <b>admitted</b> by row-discriminator multi-tenancy, resolves the store undecorated,
/// attests the row-partitioned mechanism and not the ambient-scoped one, and then actually <b>serves a
/// read</b> that carries every tenant back out of one estate-wide drain.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> The store already carried the tenant on the document and handed it back on the
/// drain — the row-partitioned mechanism, correctly implemented — but it declared nothing and its two
/// registration paths emitted no capability marker. Row-discriminator multi-tenancy therefore refused every
/// host that used this provider. That is a gate rejecting a correct host, not a leak: the safety property
/// held perfectly and the liveness property was broken, which is the failure a suite of safety-only arms is
/// structurally incapable of seeing.
/// </para>
/// <para>
/// <b>Why the drain stays estate-wide, and why no tenant predicate belongs on this store.</b> One dispatcher
/// serves every tenant. The processor establishes a per-message scope from the discriminator the row
/// carries, so a store that instead filtered on the ambient tenant would read it as absent at drain time,
/// claim the empty set, and stall delivery for every tenant — while passing any arm that only asserts one
/// tenant cannot see another tenant's rows. The store's remaining statements are addressed by the document
/// identity, where a tenant term could only turn the correct row into no row. Both halves are asserted
/// below, because either alone is satisfiable by a store that does nothing at all.
/// </para>
/// <para>
/// <b>Real infrastructure, not a mock.</b> Runs against a real PostgreSQL via TestContainers, through a real
/// Marten <see cref="IDocumentStore"/> and the production <c>AddExcalibur</c> to <c>AddOutbox</c> to
/// <c>UseMarten</c> path, resolved from a real container with <c>GetRequiredKeyedService</c>. A faked
/// session returns whatever it was told to return and would certify a store that persists no tenant at all.
/// <c>DockerAvailable.ShouldBeTrue(...)</c> makes it NON-SKIPPED: a real-infra arm that passes by being
/// skipped is the gap that ships the bug.
/// </para>
/// <para>
/// <b>What turns this red.</b> Drop <c>ITenantPartitionedStore</c> from <see cref="MartenOutboxStore"/>'s
/// base list, or change either <c>AddTenantAwareStore</c> call site back to <c>TryAddSingleton</c>, and the
/// admission and attestation arms fail because the marker disappears with the declaration or with the seam.
/// Drop the tenant from either half of the round trip — the assignment in <c>FromOutbound</c> or the one in
/// <c>ToOutbound</c> — and the drained tenants no longer match the staged ones. Add a tenant predicate to
/// the drain and the count arm fails.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxTenantPartitionAdmissionShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	/// <summary>
	/// Its own Marten schema, so this arm never shares document storage with the conformance deriver that
	/// runs in the same collection.
	/// </summary>
	private const string SchemaName = "marten_outbox_tenant";

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MartenOutboxTenantPartitionAdmissionShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public MartenOutboxTenantPartitionAdmissionShould(PostgresOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task BeAdmittedUnderRowDiscriminator_AndDrainEveryTenantFromOnePass()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"This is the liveness half of the outbox tenant gate — that a correctly wired host is admitted "
			+ "and its drain still carries every tenant. A real-infra arm that passes by being skipped "
			+ "proves nothing, and the defect it guards is a startup refusal followed by a delivery stall.");

		var services = new ServiceCollection();
		_ = services.AddLogging();

		// A distinct application name, so Npgsql pools this data source separately from the one the
		// conformance deriver shares across the collection. Disposing the provider below then cannot take
		// that pool down underneath a later test.
		var connectionString = _fixture.ConnectionString + ";Application Name=marten_outbox_tenant_lock";

		var documentStore = DocumentStore.For(opts =>
		{
			opts.Connection(connectionString);
			opts.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
			opts.DatabaseSchemaName = SchemaName;
		});

		_ = services.AddSingleton<IDocumentStore>(documentStore);
		_ = services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseMarten()));

		// Reaching past this line is the first assertion. Before this provider was moved onto the
		// partitioned seam this threw, so a consumer on Marten could not turn on row-discriminator
		// multi-tenancy at all.
		Should.NotThrow(
			() => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
			"RowDiscriminator must ADMIT a correctly wired Marten outbox. This provider carries the tenant "
			+ "on the document and re-establishes it on drain, which is exactly the mechanism "
			+ "ITenantPartitionedCapability attests. Rejecting it is the gate refusing a correct host, and "
			+ "that is invisible to every safety-only arm on this contract because they all assert a refusal.");

		await using var provider = services.BuildServiceProvider();

		_ = provider.GetRequiredService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
			"The Marten outbox must present ITenantPartitionedCapability<IOutboxStore>, emitted by "
			+ "AddTenantAwareStore inseparably from the store registration. Without it every host using "
			+ "this provider is refused by RowDiscriminator.");

		provider.GetService<ITenantScopingCapability<IOutboxStore>>().ShouldBeNull(
			"The Marten outbox must NOT present ITenantScopingCapability<IOutboxStore>. That marker attests "
			+ "the store applies the ambient tenant discriminator to every operation, and this store reads "
			+ "no ambient tenant on any path. Presenting it is the lying-marker defect: the gate passes and "
			+ "the documentation then describes a verification that did not happen.");

		var store = provider.GetRequiredKeyedService<IOutboxStore>("marten");

		_ = store.ShouldBeOfType<MartenOutboxStore>(
			"The admitted outbox must resolve as the provider's own store, undecorated. A tenant-scoping "
			+ "wrapper on this contract would read the ambient tenant as absent at drain time, claim the "
			+ "empty set, and stall delivery for every tenant while looking safe.");

		provider.GetRequiredKeyedService<IOutboxStore>("default").ShouldBeSameAs(
			store,
			"The default outbox alias must resolve to the same admitted store instance.");

		// Documents, not the schema: the shared container is reused across the collection, so the estate
		// this drain observes has to be the two messages staged below and nothing left by a prior arm.
		await documentStore.Advanced.Clean.DeleteAllDocumentsAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

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
			+ "returns fewer than the staged set is the stall this seam exists to prevent, and it would "
			+ "look perfectly safe to any arm that only asserts one tenant cannot see another's rows.");

		drained.Select(static m => m.TenantId)
			.OrderBy(static t => t, StringComparer.Ordinal)
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
