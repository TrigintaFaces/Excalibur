// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.MultiTenancy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠implementer, real-DI + real-SQL-Server lock for GDPR erasure through the ACTUAL
/// <c>IErasureContributor</c> — resolved from a real <see cref="ServiceProvider"/> exactly as production
/// wires it (<c>AddExcaliburEventSourcing(b =&gt; b.UseEventStoreErasure&lt;…&gt;())</c> + the keyed
/// <c>"default"</c> event store [+ row-discriminator multi-tenancy]). This is the factory-faithful sibling
/// of <c>SqlServerEventStoreErasureMtFailClosedShould</c>: the n8ogfo bug lived in the contributor's
/// singleton FACTORY (<c>"default" as IEventStoreErasure ?? throw</c>), so the faithful RED-target is the
/// real contributor, not a re-implementation of its resolution.
/// </summary>
/// <remarks>
/// <para>
/// <b>RED target (pre-fix):</b> the whole decorator chain stripped <see cref="IEventStoreErasure"/>, so
/// <c>GetRequiredService&lt;IErasureContributor&gt;()</c> threw at construction (the factory <c>?? throw</c>)
/// in EVERY deployment — GDPR erasure broken via real DI. Post-fix (chain-forward) the contributor resolves
/// and these arms hold.
/// </para>
/// <para>
/// <b>Property-based safety+liveness</b> (testing-patterns §3): the contributor CATCHES a per-aggregate
/// erase failure and reports it (Success=false), so the safety property is the OBSERVABLE one — an
/// unscoped MT erase is REJECTED (Success=false) and mutates ZERO rows on any tenant (fail-closed before any
/// UPDATE), paired with liveness (a scoped MT erase tombstones only its own tenant; a non-MT erase
/// tombstones its single partition).
/// </para>
/// <para>
/// The seeded mapping interprets the data-subject hash AS the aggregate id, so the test controls exactly
/// which aggregate the contributor resolves and erases. Real SQL Server (TestContainers); surviving payloads
/// read straight from the engine. NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreErasureContributorShould
{
	private const string AggregateType = "Order";
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreErasureContributorShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	// Maps a data subject to a single aggregate whose id IS the supplied hash — the test seeds an aggregate
	// with id == hash so the contributor resolves and erases exactly that aggregate.
	private sealed class HashIsAggregateIdMapping : IAggregateDataSubjectMapping
	{
		public Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
			string dataSubjectIdHash, string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AggregateReference>>(
				new[] { new AggregateReference(dataSubjectIdHash, AggregateType) });
	}

	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private ServiceProvider BuildProvider(bool multiTenant)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburEventSourcing(b => b.UseEventStoreErasure<HashIsAggregateIdMapping>());
		_ = services.AddSqlServerEventStore(
			() => _fixture.CreateConnection(), _fixture.SchemaName, _fixture.TableName);
		if (multiTenant)
		{
			_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
		}

		return services.BuildServiceProvider();
	}

	private async Task<int> SurvivingPayloadCountAsync(string aggregateId, string tenantId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

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

	private static async Task SeedAsync(IEventStore store, string aggregateId)
	{
		_ = await store.AppendAsync(
			aggregateId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggregateId, 0), new OrderPlaced(aggregateId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);
	}

	private static ErasureContributorContext ContextFor(string aggregateIdAsHash, string? tenantId) => new()
	{
		RequestId = Guid.NewGuid(),
		DataSubjectIdHash = aggregateIdAsHash,
		IdType = default,
		Scope = ErasureScope.User,
		TenantId = tenantId,
	};

	[Fact]
	public async Task FailClosed_MutatingNoRows_WhenMtContributorErasesWithNullTenant()
	{
		// n8ogfo — SAFETY (contributor-faithful). An unscoped MT erase through the real contributor must be
		// REJECTED (Success=false) and tombstone NO rows on any tenant. RED pre-fix = the contributor factory
		// throws at construction (chain strip); post-fix it resolves and fails closed here.
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure under multi-tenancy is a legal obligation — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildProvider(multiTenant: true);
		var contributor = provider.GetRequiredService<IErasureContributor>();

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await SeedAsync(store, aggId).ConfigureAwait(false);
		}
		(await SurvivingPayloadCountAsync(aggId, TenantB).ConfigureAwait(false))
			.ShouldBe(2, "precondition: tenant B's payloads exist before the null-tenant erase");

		// Erase with a null ambient tenant (default User scope) → fail-closed.
		var result = await contributor.EraseAsync(ContextFor(aggId, tenantId: null), CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeFalse(
			"an unscoped multi-tenant erase must be REJECTED (fail-closed), never silently succeed");
		(await SurvivingPayloadCountAsync(aggId, TenantB).ConfigureAwait(false))
			.ShouldBe(2, "a rejected unscoped MT erase must mutate NO rows — tenant B's payloads survive");
	}

	[Fact]
	public async Task TombstoneOnlyTheScopedTenant_WhenMtContributorErasesWithATenant()
	{
		// n8ogfo — LIVENESS (scoped, MT). The real contributor reaches + tombstones ONLY the scoped tenant's
		// rows (proves the chain-forward made erasure non-vacuous under MT). RED pre-fix (factory throws).
		_fixture.DockerAvailable.ShouldBeTrue(
			"MT erasure must work for the correct tenant — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildProvider(multiTenant: true);
		var contributor = provider.GetRequiredService<IErasureContributor>();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var aggA = "agg-" + Guid.NewGuid().ToString("N");
		var aggB = "agg-" + Guid.NewGuid().ToString("N");
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await SeedAsync(store, aggA).ConfigureAwait(false);
		}
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await SeedAsync(store, aggB).ConfigureAwait(false);
		}

		var result = await contributor.EraseAsync(ContextFor(aggB, TenantB), CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeTrue("the scoped erase for tenant B succeeds");
		(await SurvivingPayloadCountAsync(aggB, TenantB).ConfigureAwait(false))
			.ShouldBe(0, "tenant B's own rows are tombstoned by its scoped erase");
		(await SurvivingPayloadCountAsync(aggA, TenantA).ConfigureAwait(false))
			.ShouldBe(2, "tenant A's rows are UNTOUCHED — a scoped erase reaches only its own tenant");
	}

	[Fact]
	public async Task TombstoneItsSinglePartition_WhenNonMtContributorErases()
	{
		// n8ogfo — LIVENESS (non-MT). A genuine non-MT deployment erases its single partition through the real
		// contributor (None path preserved). RED pre-fix (factory throws — the telemetry strip broke non-MT too).
		_fixture.DockerAvailable.ShouldBeTrue(
			"single-tenant erasure must work — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildProvider(multiTenant: false);
		var contributor = provider.GetRequiredService<IErasureContributor>();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		await SeedAsync(store, aggId).ConfigureAwait(false);

		var result = await contributor.EraseAsync(ContextFor(aggId, tenantId: null), CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeTrue("the single-tenant (None) erase path must remain fully usable");
		(await SurvivingPayloadCountByAggregateAsync(aggId).ConfigureAwait(false))
			.ShouldBe(0, "the single-tenant deployment's own payloads are tombstoned");
	}

	[Fact]
	public void FailClosedAtStartup_WhenShardingIsCombinedWithErasure()
	{
		// n8ogfo carve (rjxf95) — SAFETY. The ShardingErasureGuard makes sharding+erasure fail-closed LOUD at
		// startup validation (ValidateOnStart → OptionsValidationException), never a silent GDPR mis-route.
		// Order-independent: keys off the tenant-sharding marker, not registration order. IStartupValidator is
		// the host-free stand-in for host start (IHost.StartAsync runs this same validator) — gate-attributable:
		// RED if the guard/marker is removed (Validate() would no longer throw). BuildServiceProvider's
		// ValidateOnBuild is NOT used here: it runs graph-resolvability only, not ValidateOnStart, and would
		// surface the incidental scoped-router/contributor throw rather than the gate's OptionsValidationException.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburEventSourcing(b =>
		{
			_ = b.UseEventStoreErasure<HashIsAggregateIdMapping>();
			_ = b.EnableTenantSharding(o => o.DefaultShardId = "shard-default");
		});
		_ = services.AddSqlServerEventStore(
			() => _fixture.CreateConnection(), _fixture.SchemaName, _fixture.TableName);

		using var provider = services.BuildServiceProvider();
		var validator = provider.GetRequiredService<IStartupValidator>();
		_ = Should.Throw<OptionsValidationException>(() => validator.Validate());
	}

	[Fact]
	public void BuildCleanly_WhenErasureWithoutSharding()
	{
		// n8ogfo carve (rjxf95) — LIVENESS. Erasure without sharding must NOT trip the guard (it is silent
		// unless sharding is active), so startup validation passes and the contributor resolves.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburEventSourcing(b => b.UseEventStoreErasure<HashIsAggregateIdMapping>());
		_ = services.AddSqlServerEventStore(
			() => _fixture.CreateConnection(), _fixture.SchemaName, _fixture.TableName);

		using var provider = services.BuildServiceProvider();
		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
		provider.GetRequiredService<IErasureContributor>().ShouldNotBeNull();
	}
}
