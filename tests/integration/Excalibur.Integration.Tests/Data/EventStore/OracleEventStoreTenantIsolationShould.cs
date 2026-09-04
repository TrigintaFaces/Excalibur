// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠impl tenant-isolation regression lock for rdur0y — the Oracle <see cref="OracleEventStore"/>
/// scopes every read/write to the ambient <see cref="ITenantContext"/>'s <c>TenantId</c> (a row-level
/// <c>TENANTID</c> discriminator applied in the same statement), so one tenant can NEVER observe another
/// tenant's event streams. Cross-tenant data disclosure is a security boundary — this proves the isolation.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real Oracle (TestContainers) so the
/// <c>WHERE TENANTID = :tenant</c> predicate is evaluated by the real engine — a mock cannot reproduce
/// row-level scoping. NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>). Shares the Oracle container via the
/// collection fixture (Oracle is slow to start).
/// <para>
/// <b>RED-on-mutant:</b> drop the <c>TENANTID</c> predicate from the Load/IsErased/Insert requests ⇒ tenant
/// B's <c>LoadAsync</c> returns tenant A's events (and B's fresh-stream append would conflict) ⇒ the
/// isolation facts go RED.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreTenantIsolationShould
{
	private const string AggregateType = "Order";

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleEventStoreTenantIsolationShould(OracleEventStoreContainerFixture fixture) => _fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private OracleEventStore StoreFor(string? tenantId) =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			schema: _fixture.Schema,
			table: _fixture.TableName,
			tenantContext: tenantId is null ? UntenantedTestTenantContext.Instance : (ITenantContext)new FixedTenant(tenantId));

[MessageName("Test.OracleEventStoreTenantIsolation.OrderPlaced")]
private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public async Task ScopeEveryStreamToItsTenant_OneTenantNeverSeesAnother()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"rdur0y cross-tenant isolation is a security boundary — this real-Oracle lock must never be skipped");
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

		// Isolation — tenant B cannot LOAD tenant A's events.
		(await tenantB.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty("tenant B must not load tenant A's event stream (row-level TENANTID scoping)");

		// Isolation — tenant B sees no ERASURE state for tenant A's aggregate.
		(await tenantB.IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("tenant B must not observe tenant A's aggregate erasure state");

		// Tenant A still sees exactly its own 2 events — reads are scoped to the writing tenant.
		(await tenantA.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "tenant A sees exactly its own 2 events (its own tenant-scoped stream)");
	}

	[Fact]
	public async Task NotDiscloseATenantsEvents_ToAnUnscopedReader()
	{
		// SAFETY (18c3el read-leak). The event-read fixes shipped without a non-vacuous SAFETY lock — the
		// existing arms are scoped-vs-scoped only, never proving an unscoped read does not DISCLOSE a tenant's
		// events. RED against the pre-fix empty predicate; GREEN once the unscoped branch is bounded to the
		// untenanted partition (the __untenanted__ sentinel). Property-based: asserts the disclosure.
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant read disclosure is a security boundary — this real-Oracle lock must never be skipped");
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
