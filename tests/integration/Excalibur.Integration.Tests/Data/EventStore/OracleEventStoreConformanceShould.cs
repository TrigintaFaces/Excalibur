// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="OracleEventStore"/> using the EventStore Conformance Test Kit against
/// a real <c>gvenzl/oracle-free</c> container. Non-skipped: Docker is required (per the provider
/// real-infra lock discipline).
/// </summary>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class OracleEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<OracleEventStoreContainerFixture>
{
	private readonly OracleEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleEventStoreConformanceShould(OracleEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IEventStore> CreateStoreAsync()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		return CreateStore();
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private OracleEventStore CreateStore() =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			payloadSerializer: null,
			schema: _fixture.Schema,
			table: _fixture.TableName);

	/// <summary>
	/// A0 seam #6: appends survive a reload on a FRESH store instance (durability across instances) and
	/// the stream version is the authoritative persisted value, not an in-memory artifact.
	/// </summary>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task Append_Then_ReloadOnFreshInstance_SeesPersistedVersion()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const string aggregateType = "OracleConformanceAggregate";

		var writer = CreateStore();
		var events = new IDomainEvent[]
		{
			new ConformanceEvent(aggregateId, 1),
			new ConformanceEvent(aggregateId, 2),
		};

		var append = await writer.AppendAsync(aggregateId, aggregateType, events, expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		append.Success.ShouldBeTrue(append.ErrorMessage);

		// Fresh instance — nothing shared with the writer but the real database.
		var reader = CreateStore();
		var loaded = await reader.LoadAsync(aggregateId, aggregateType, TestContext.Current.CancellationToken).ConfigureAwait(false);

		loaded.Count.ShouldBe(2);
		// Version is 0-based (store assigns currentVersion then ++): 2 events => versions 0,1 => highest 1.
		loaded[^1].Version.ShouldBe(1);
	}

	/// <summary>
	/// A0 seam #6: a second append at a stale expected version is rejected as a concurrency conflict
	/// (read-current-version-then-compare inside the serializable transaction), never silently applied.
	/// </summary>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task Append_WithStaleExpectedVersion_ReportsConcurrencyConflict()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const string aggregateType = "OracleConformanceAggregate";
		var store = CreateStore();

		var first = await store.AppendAsync(
				aggregateId, aggregateType, new IDomainEvent[] { new ConformanceEvent(aggregateId, 1) },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		first.Success.ShouldBeTrue(first.ErrorMessage);

		// Expected version -1 again is stale (stream is now at 1).
		var conflict = await store.AppendAsync(
				aggregateId, aggregateType, new IDomainEvent[] { new ConformanceEvent(aggregateId, 2) },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		conflict.IsConcurrencyConflict.ShouldBeTrue();
	}

	/// <summary>
	/// A0 seam #5: Oracle folds <c>''</c> to <c>NULL</c>. An event whose metadata is empty must still
	/// round-trip through the BLOB column and the stream version/identity must be intact, proving the
	/// empty-string fold cannot corrupt a not-null identity column.
	/// </summary>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task Append_WithEmptyMetadata_RoundTripsWithoutEmptyStringFold()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const string aggregateType = "OracleConformanceAggregate";
		var store = CreateStore();

		var append = await store.AppendAsync(
				aggregateId, aggregateType, new IDomainEvent[] { new ConformanceEvent(aggregateId, 1) },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		append.Success.ShouldBeTrue(append.ErrorMessage);

		var loaded = await store.LoadAsync(aggregateId, aggregateType, TestContext.Current.CancellationToken).ConfigureAwait(false);
		loaded.Count.ShouldBe(1);
		loaded[0].AggregateId.ShouldBe(aggregateId);
		// Version is 0-based: the single appended event is version 0.
		loaded[0].Version.ShouldBe(0);
	}

	/// <summary>
	/// bd-1m19p6 regression lock: <see cref="OracleEventStore.LoadAsync"/> must MATERIALIZE a loaded stream
	/// and round-trip the event timestamp. Oracle returns <c>EVENTTIMESTAMP</c> (TIMESTAMP WITH TIME ZONE)
	/// as <see cref="DateTimeOffset"/>; if the provider row types it as <see cref="DateTime"/>, Dapper finds
	/// no matching constructor and <c>LoadAsync</c> throws for EVERY loaded stream (data-loss-shaped).
	/// </summary>
	/// <remarks>
	/// <b>verify-against-real-infra-not-mock:</b> real Oracle (TestContainers), non-skipped. Safety+liveness:
	/// the load must not throw (materialization succeeds) AND the persisted UTC timestamp must be returned
	/// intact — a mutant that re-types <c>OracleEventRow.Timestamp</c> back to <see cref="DateTime"/> goes RED
	/// (no matching ctor). Asserts the value, not merely "did not throw", so a silent timestamp corruption is
	/// also caught.
	/// </remarks>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task LoadAsync_MaterializesStream_AndRoundTripsUtcTimestamp()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const string aggregateType = "OracleConformanceAggregate";
		var store = CreateStore();

		var evt = new ConformanceEvent(aggregateId, 1);
		var expectedUtc = evt.OccurredAt.ToUniversalTime();

		var append = await store.AppendAsync(
				aggregateId, aggregateType, new IDomainEvent[] { evt },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		append.Success.ShouldBeTrue(append.ErrorMessage);

		// Fresh instance — forces a real materialization from Oracle, exercising the OracleEventRow ctor.
		var reader = CreateStore();
		var loaded = await reader.LoadAsync(aggregateId, aggregateType, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		// Liveness: LoadAsync materialized the stream (pre-fix it threw — no matching ctor for DateTimeOffset).
		loaded.Count.ShouldBe(1);
		// Version is 0-based: the single appended event is version 0.
		loaded[0].Version.ShouldBe(0);

		// The persisted UTC timestamp round-trips (Oracle TIMESTAMP(7) precision — allow sub-second tolerance).
		loaded[0].Timestamp.Offset.ShouldBe(TimeSpan.Zero, "LoadAsync normalizes the stored offset to UTC");
		(loaded[0].Timestamp - expectedUtc).Duration()
			.ShouldBeLessThan(TimeSpan.FromSeconds(1), "the appended OccurredAt round-trips through Oracle");
	}

	/// <summary>
	/// 1m19p6 SAFETY arm (SA/PM hard gate): the ORA-08177 fix switches the multi-event append from
	/// <c>INSERT ALL</c> to ODP.NET array binding but MUST keep <see cref="System.Data.IsolationLevel.Serializable"/>
	/// — the optimistic-concurrency invariant (read-version → check → INSERT, atomic). Two CONCURRENT
	/// MULTI-event appends at the same <c>expectedVersion</c> on real Oracle: exactly ONE commits, the other
	/// returns a concurrency conflict — never two successes (a silent lost update). Exercises the exact batch
	/// path the fix touches; RED pre-fix (both fail ORA-08177 → 0 successes), GREEN only when the fix persists
	/// AND preserves isolation. Non-skipped.
	/// </summary>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task ConcurrentMultiEventAppend_SameExpectedVersion_YieldsExactlyOneConflict_NeverALostUpdate()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const string aggregateType = "OracleConformanceAggregate";

		// Seed a 2-event stream (multi-event batch path) → stream is now at version 1.
		var seed = await CreateStore().AppendAsync(
				aggregateId, aggregateType,
				new IDomainEvent[] { new ConformanceEvent(aggregateId, 0), new ConformanceEvent(aggregateId, 1) },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		seed.Success.ShouldBeTrue(seed.ErrorMessage);

		// Two concurrent MULTI-event appends, both at expectedVersion 1, on independent connections.
		async Task<AppendResult> AppendTwoAtVersionOne() =>
			await CreateStore().AppendAsync(
					aggregateId, aggregateType,
					new IDomainEvent[] { new ConformanceEvent(aggregateId, 2), new ConformanceEvent(aggregateId, 3) },
					expectedVersion: 1, TestContext.Current.CancellationToken)
				.ConfigureAwait(false);

		var results = await Task.WhenAll(
				Task.Run(AppendTwoAtVersionOne), Task.Run(AppendTwoAtVersionOne))
			.ConfigureAwait(false);

		results.Count(r => r.Success).ShouldBe(1,
			"exactly one concurrent multi-event append may commit at a given version (SERIALIZABLE)");
		results.Count(r => r.IsConcurrencyConflict).ShouldBe(1,
			"the loser MUST be a ConcurrencyConflict — the fix must not lower isolation into a silent lost update");
	}

	private sealed class ConformanceEvent : IDomainEvent
	{
		public ConformanceEvent(string aggregateId, long version)
		{
			AggregateId = aggregateId;
			Version = version;
		}

		public string EventId { get; } = Guid.NewGuid().ToString();

		public string AggregateId { get; }

		public long Version { get; }

		public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

		public string EventType => nameof(ConformanceEvent);

		public IDictionary<string, object>? Metadata => null;
	}
}
