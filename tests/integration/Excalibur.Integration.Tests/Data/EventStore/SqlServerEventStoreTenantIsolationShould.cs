// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠impl real-infra lock for the row-discriminator tenancy keystone — the SQL Server
/// <see cref="SqlServerEventStore"/> derives a tenant scope from the ambient <see cref="ITenantContext"/>
/// and applies a row-level <c>TenantId</c> discriminator (<c>AND TenantId = @TenantId</c>) in the same atomic
/// statement on every read/write, so one tenant can NEVER observe another tenant's event streams — and the
/// non-multi-tenant path (no tenant context) round-trips unchanged with no tenant column referenced at all.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real SQL Server (TestContainers) so the
/// <c>WHERE TenantId = @TenantId</c> predicate is evaluated by the real engine — a mock cannot reproduce
/// row-level scoping or the non-MT (predicate-free) round-trip. NON-SKIPPED
/// (<c>DockerAvailable.ShouldBeTrue</c>). Shares the SQL Server container via the collection fixture.
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's scoped read must not see tenant A's rows;
/// LIVENESS — tenant A still reads its own stream, and the non-MT store round-trips its events.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> drop the <c>TenantId</c> predicate from the Load/IsErased requests ⇒ tenant B's
/// <c>LoadAsync</c> returns tenant A's events ⇒ the isolation facts go RED. Emit the predicate on the
/// unscoped path ⇒ the non-MT round-trip throws on the (real) missing column binding.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreTenantIsolationShould
{
	private const string AggregateType = "Order";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreTenantIsolationShould(SqlServerEventStoreContainerFixture fixture) => _fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private SqlServerEventStore StoreFor(string? tenantId) =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerEventStore>.Instance,
			schema: _fixture.SchemaName,
			table: _fixture.TableName,
			tenantContext: tenantId is null ? null : new FixedTenant(tenantId));

	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public async Task ScopeEveryStreamToItsTenant_OneTenantNeverSeesAnother()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant isolation is a security boundary — this real-SQL Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var tenantA = StoreFor("tenant-A");
		var tenantB = StoreFor("tenant-B");

		// Tenant A writes a 2-event stream.
		_ = await tenantA.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		// SAFETY — tenant B cannot LOAD tenant A's events.
		(await tenantB.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty("tenant B must not load tenant A's event stream (row-level TenantId scoping)");

		// SAFETY — tenant B sees no ERASURE state for tenant A's aggregate.
		(await tenantB.IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("tenant B must not observe tenant A's aggregate erasure state");

		// LIVENESS — tenant A still sees exactly its own 2 events (reads scoped to the writing tenant).
		(await tenantA.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "tenant A sees exactly its own 2 events (its own tenant-scoped stream)");
	}

	[Fact]
	public async Task RoundTripUnscoped_WhenNoTenantContext_WithNoTenantPredicate()
	{
		// LIVENESS (non-MT / AC-K1.1) — a store with NO tenant context is the genuine non-multi-tenant path:
		// it must round-trip append→load and not throw. Under the keyed migration the None scope no longer emits an
		// empty predicate: it binds the reserved __untenanted__ sentinel, so this store reaches exactly the
		// the column on INSERT nor the predicate on SELECT).
		_fixture.DockerAvailable.ShouldBeTrue(
			"the non-multi-tenant round-trip is the keystone's fail-open path — this real lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var nonTenant = StoreFor(null);

		_ = await nonTenant.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		(await nonTenant.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "the non-multi-tenant store round-trips its own events via the untenanted partition");
	}

	[Fact]
	public async Task NotDiscloseATenantsEvents_ToAnUnscopedReader()
	{
		// SAFETY (18c3el read-leak). The event-read fixes shipped without a non-vacuous SAFETY lock — the
		// existing arms are scoped-vs-scoped + unscoped-LIVENESS only, neither proving an unscoped read does not
		// DISCLOSE a tenant's events. RED against the pre-fix empty predicate; GREEN once the unscoped branch is
		// bounded to the untenanted partition (the __untenanted__ sentinel). Property-based: asserts the disclosure.
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant read disclosure is a security boundary — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");

		_ = await StoreFor("tenant-B").AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		(await StoreFor(null).LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty(
				"an unscoped reader must not receive a tenant's events — the untenanted partition "
				+ "(the __untenanted__ sentinel, onto which COALESCE folds legacy NULL rows) excludes tenant-scoped rows; the empty-branch predicate disclosed every "
				+ "tenant's events to an unscoped host");
	}
}
