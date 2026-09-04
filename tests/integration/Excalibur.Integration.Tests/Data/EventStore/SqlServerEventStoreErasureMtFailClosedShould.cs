// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.MultiTenancy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠implementer, real-DI + real-SQL-Server lock for the GDPR-erasure MT fail-closed contract
/// (n8ogfo). The default <c>User</c>/<c>Selective</c> erase scope passes a null ambient tenant to
/// <c>EventStoreErasureContributor</c>, which resolves erasure via
/// <c>GetRequiredKeyedService&lt;IEventStore&gt;("default") as IEventStoreErasure</c>. Under a real
/// row-discriminator multi-tenancy composition the "default" key is decorated with
/// <c>TenantScopedEventStore</c>, so the erase seam must (a) remain reachable as
/// <see cref="IEventStoreErasure"/> and (b) fail closed on a null/empty ambient tenant rather than run a
/// no-predicate erase across every tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why real DI, not direct construction</b> (S873 real-DI-resolve): the MT-capable signal is a
/// <em>registration</em> fact — the <c>TenantScopedEventStore</c> decorator is applied only by
/// <c>AddMultiTenancy(RowDiscriminator)</c>. A lock that <c>new</c>s the store directly would bind a
/// signal that does not exist in production. These fixtures resolve <see cref="IEventStore"/> through the
/// exact production keyed registration the contributor uses (<c>"default"</c>), so the MT vs non-MT arms
/// differ only by the presence of <c>AddMultiTenancy</c>.
/// </para>
/// <para>
/// <b>RED targets (pre-fix, real MT DI):</b> pre-fix the decorator is <see cref="IEventStore"/>-only, so
/// the <c>"default"</c> key <em>strips</em> <see cref="IEventStoreErasure"/> (the cast lands on null) —
/// GDPR erasure is a silent no-op / broken under MT. So BOTH arms fail pre-fix: the SAFETY arm because the
/// resolved store is not <see cref="IEventStoreErasure"/>; the scoped LIVENESS arm because erasure cannot
/// run at all under MT. Post-fix (<c>TenantScopedEventStore : IEventStoreErasure</c> with a
/// <c>RequireTenant()</c> guard before delegating) both pass — non-vacuous.
/// </para>
/// <para>
/// <b>Property-based</b> (testing-patterns §3): SAFETY paired with LIVENESS per axis. The safety property
/// is "an unscoped MT erase is REJECTED — it throws before any row is mutated"; asserted both as the
/// <see cref="TenantRequiredException"/> throw AND (real-infra) zero surviving-row change on any tenant.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real SQL Server (TestContainers); surviving payloads read
/// straight from the engine. NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreErasureMtFailClosedShould
{
	private const string AggregateType = "Order";
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreErasureMtFailClosedShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

[MessageName("Test.SqlServerEventStoreErasureMtFailClosed.OrderPlaced")]
private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Builds a <see cref="ServiceProvider"/> that resolves <see cref="IEventStore"/> the SAME way the
	/// erasure contributor does — the keyed <c>"default"</c> registration — with or without the
	/// row-discriminator multi-tenancy composition. The MT provider's <c>"default"</c> resolves to the
	/// <c>TenantScopedEventStore</c> decorator; the non-MT provider's resolves to the bare store.
	/// </summary>
	private ServiceProvider BuildProvider(bool multiTenant)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerEventStore(
			() => _fixture.CreateConnection(),
			_fixture.SchemaName,
			_fixture.TableName);

		if (multiTenant)
		{
			// Row-discriminator MT is the ONLY thing that applies the TenantScopedEventStore decorator to the
			// "default" key — this is the registration-time MT-capable signal (not a schema probe).
			_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
		}

		return services.BuildServiceProvider();
	}

	private static IEventStoreErasure? ResolveErasure(ServiceProvider provider) =>
		provider.GetRequiredKeyedService<IEventStore>("default") as IEventStoreErasure;

	/// <summary>
	/// Counts the surviving (non-tombstoned) payload rows for an aggregate+tenant, read straight from the
	/// engine so the store's read path cannot mask an erase that reached the wrong partition.
	/// </summary>
	private async Task<int> SurvivingPayloadCountAsync(string aggregateId, string tenantId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: schema/table are the fixture's constant names; aggregate id + tenant id are bound parameters.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"SELECT COUNT(*) FROM [{_fixture.SchemaName}].[{_fixture.TableName}] "
			+ "WHERE AggregateId = @aggId AND TenantId = @tenant AND EventData IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@aggId", aggregateId);
		_ = command.Parameters.AddWithValue("@tenant", tenantId);

		return Convert.ToInt32(
			await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Counts surviving (non-tombstoned) payload rows for an aggregate across ALL tenant partitions, read
	/// straight from the engine. Used for the non-MT arm, where the untenanted partition's discriminator
	/// encoding (NULL vs '') is an implementation detail this lock must not couple to.
	/// </summary>
	private async Task<int> SurvivingPayloadCountByAggregateAsync(string aggregateId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"SELECT COUNT(*) FROM [{_fixture.SchemaName}].[{_fixture.TableName}] "
			+ "WHERE AggregateId = @aggId AND EventData IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@aggId", aggregateId);

		return Convert.ToInt32(
			await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}

	private async Task SeedAsync(IEventStore store, string aggregateId)
	{
		_ = await store.AppendAsync(
			aggregateId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggregateId, 0), new OrderPlaced(aggregateId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task RemainReachableAsErasure_AndFailClosed_WhenMtAndAmbientTenantIsNull()
	{
		// n8ogfo — SAFETY. Pre-fix RED twice over: (1) the MT "default" store strips IEventStoreErasure (the
		// cast is null), so GDPR erasure is silently broken under MT; and even resolving it, (2) a null-tenant
		// erase must be REJECTED (throw before any UPDATE), never run a no-predicate cross-tenant erase.
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure under multi-tenancy is a legal obligation and a cross-tenant erase is a data-protection "
			+ "incident — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildProvider(multiTenant: true);

		// (1) The MT "default" store MUST still be reachable as IEventStoreErasure — pre-fix the decorator
		// strips it (cast → null), which is the silent-GDPR-no-op half of the finding.
		var erasure = ResolveErasure(provider);
		_ = erasure.ShouldNotBeNull(
			"EXPECTED RED until TenantScopedEventStore implements IEventStoreErasure (n8ogfo). Under MT the "
			+ "'default' key is the TenantScopedEventStore decorator; if it does not implement IEventStoreErasure "
			+ "the contributor's cast is null and GDPR erasure is a silent no-op under multi-tenancy");

		// Seed a tenant-B aggregate so an over-erase would have something to (wrongly) destroy.
		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await SeedAsync((IEventStore)erasure!, aggId).ConfigureAwait(false);
		}
		(await SurvivingPayloadCountAsync(aggId, TenantB).ConfigureAwait(false))
			.ShouldBe(2, "precondition: tenant B's two payloads exist before the unscoped erase attempt");

		// (2) An erase with a null/empty ambient tenant MUST fail closed — throw before any UPDATE.
		_ = await Should.ThrowAsync<TenantRequiredException>(async () =>
			await erasure!.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		// SAFETY (real-infra): zero rows mutated — tenant B's payloads survive the rejected erase.
		(await SurvivingPayloadCountAsync(aggId, TenantB).ConfigureAwait(false))
			.ShouldBe(2, "a rejected unscoped MT erase must mutate NO rows — tenant B's payloads must survive");
	}

	[Fact]
	public async Task StillTombstoneOnlyTheScopedTenantsRows_WhenMtAndAmbientTenantIsPresent()
	{
		// n8ogfo — LIVENESS (scoped, MT). Proves the fix does not break MT erasure and does not silently no-op:
		// a scoped erase actually reaches and tombstones ONLY the scoped tenant's rows. Pre-fix RED (the MT
		// store is not IEventStoreErasure, so erasure cannot run under MT at all).
		_fixture.DockerAvailable.ShouldBeTrue(
			"MT erasure must keep working for the correct tenant — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildProvider(multiTenant: true);
		var erasure = ResolveErasure(provider);
		_ = erasure.ShouldNotBeNull("EXPECTED RED until MT erasure is reachable as IEventStoreErasure (n8ogfo)");

		// Seed the SAME aggregate id under two tenants (the events table's unique key is tenant-agnostic per
		// aggregate+version, so use distinct aggregate ids per tenant to model two tenants' streams).
		var aggA = "agg-" + Guid.NewGuid().ToString("N");
		var aggB = "agg-" + Guid.NewGuid().ToString("N");
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await SeedAsync((IEventStore)erasure!, aggA).ConfigureAwait(false);
		}
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await SeedAsync((IEventStore)erasure!, aggB).ConfigureAwait(false);
		}

		// Scoped erase for tenant B only.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			var erased = await erasure!
				.EraseEventsAsync(aggB, AggregateType, Guid.NewGuid(), CancellationToken.None)
				.ConfigureAwait(false);
			erased.ShouldBe(2, "the scoped erase reports tenant B's two events erased (erasure is not a no-op)");
		}

		(await SurvivingPayloadCountAsync(aggB, TenantB).ConfigureAwait(false))
			.ShouldBe(0, "tenant B's own rows are tombstoned by its scoped erase");
		(await SurvivingPayloadCountAsync(aggA, TenantA).ConfigureAwait(false))
			.ShouldBe(2, "tenant A's rows are UNTOUCHED — a scoped erase must reach only its own tenant");
	}

	[Fact]
	public async Task StillTombstoneItsSinglePartition_WhenNonMtAndAmbientTenantIsNull()
	{
		// n8ogfo — LIVENESS (non-MT). MT-optional preserved: a genuine non-MT deployment (no AddMultiTenancy →
		// bare store, no decorator) has no cross-tenant hazard, so a null-tenant erase must still tombstone its
		// single partition. GREEN now and post-fix; fails only if the fix wrongly makes the bare store fail
		// closed too (breaking single-tenant erasure).
		_fixture.DockerAvailable.ShouldBeTrue(
			"non-MT (single-tenant) erasure must keep working — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildProvider(multiTenant: false);
		var erasure = ResolveErasure(provider);
		_ = erasure.ShouldNotBeNull("the bare (non-MT) store implements IEventStoreErasure");

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		await SeedAsync((IEventStore)erasure!, aggId).ConfigureAwait(false);

		var erased = await erasure!
			.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);
		erased.ShouldBe(2, "the single-tenant (None) erase path must remain fully usable");

		// Count across all partitions so the assertion does not couple to the untenanted discriminator
		// encoding (NULL vs ''): the single-tenant deployment has only its own rows, and they are tombstoned.
		(await SurvivingPayloadCountByAggregateAsync(aggId).ConfigureAwait(false))
			.ShouldBe(0, "the single-tenant deployment's own payloads are tombstoned");
	}
}
