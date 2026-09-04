// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Excalibur.Dispatch;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Runs the <strong>complete</strong> <see cref="EventStoreConformanceTestKit"/> - every arm - against a
/// real Oracle, through the Oracle provider's OWN registration extension, plus the Oracle-specific arms
/// that have no equivalent in the kit.
/// </summary>
/// <remarks>
/// <para>
/// This suite previously derived a conformance base that lives under <c>tests/</c>. That base is not
/// shipped, so the contract it enforced was one no consumer could obtain. Deriving the published kit makes
/// the contract this provider is held to identical to the one a consumer is handed, and adds four tenancy
/// arms this provider had no conformance coverage for at all.
/// </para>
/// <para>
/// The kit resolves <c>IEventStore</c> from a container built by <c>UseOracle</c>, so the kit arms assert
/// against the object the shipped registration actually produces rather than one this file assembled.
/// </para>
/// <para>
/// The five arms below the kit arms are kept, unchanged, and deliberately construct the store by hand.
/// They are not redundant with the kit: they assert Oracle-specific behaviour the kit does not cover -
/// that a SECOND, independently constructed store instance sees the persisted version, that the ODP.NET
/// timestamp round-trips as UTC, that empty metadata does not fold to an empty string, and that a
/// concurrent multi-event append at one version yields exactly one conflict and never a lost update. Two
/// separate instances is the whole point of the first and last of those, and the kit resolves a single
/// cached store per ambient context by design, so these cannot be expressed through it.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreConformanceShould : EventStoreConformanceTestKit,
	IClassFixture<OracleEventStoreContainerFixture>, IAsyncLifetime
{
	private readonly OracleEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleEventStoreConformanceShould(OracleEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Brings the container and its schema up before any arm resolves the provider.
	/// </summary>
	/// <returns>A task that completes when Oracle is reachable and the event table exists.</returns>
	/// <remarks>
	/// The kit's provider seam is synchronous by design - the package targets no test framework and cannot
	/// own an async lifecycle. Awaiting the container here, in the runner's own lifecycle hook, is what lets
	/// a container-backed provider derive the kit at all.
	/// </remarks>
	public async ValueTask InitializeAsync() =>
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	protected override void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver)
		=> _ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseOracle(o =>
		{
			o.ConnectionString = _fixture.ConnectionString;
			o.Schema = _fixture.Schema;
			o.Table = _fixture.TableName;
			o.EventTypeInfoResolver = eventTypeInfoResolver;
		})));

	/// <summary>
	/// An event type the configured resolver does not declare is refused by throwing, and nothing is
	/// written.
	/// </summary>
	[Fact]
	public Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing_Test() =>
		AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing();

	/// <summary>SAFETY: an append above this provider's atomic limit is refused whole rather than committed in pieces.</summary>
	[Fact]
	public Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically_Test() =>
		AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically();


	/// <inheritdoc />
	protected override async Task CleanupAsync() =>
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

	#region Append arms

	/// <summary>Appending to a stream that does not yet exist succeeds and reports the new version.</summary>
	[Fact]
	public Task AppendAsync_ToNewStream_ShouldSucceed_Test() =>
		AppendAsync_ToNewStream_ShouldSucceed();

	/// <summary>An append at the stream's current version succeeds and advances it.</summary>
	[Fact]
	public Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed_Test() =>
		AppendAsync_WithCorrectExpectedVersion_ShouldSucceed();

	/// <summary>SAFETY: an append at a stale version is rejected as a conflict, not silently applied.</summary>
	[Fact]
	public Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() =>
		AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();

	/// <summary>An append of no events is a no-op that leaves the version untouched.</summary>
	[Fact]
	public Task AppendAsync_EmptyEvents_ShouldNotChangeVersion_Test() =>
		AppendAsync_EmptyEvents_ShouldNotChangeVersion();

	/// <summary>SAFETY: under a race at one expected version, exactly one writer wins and the rest conflict.</summary>
	[Fact]
	public Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed_Test() =>
		ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed();

	/// <summary>LIVENESS: independent aggregates do not falsely conflict with one another.</summary>
	[Fact]
	public Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed_Test() =>
		ConcurrentAppend_DifferentAggregates_AllShouldSucceed();

	/// <summary>SAFETY: a null, empty or whitespace aggregate identifier is rejected rather than written to a stream no reader can name.</summary>
	[Fact]
	public Task AppendAsync_UnaddressableAggregateId_ShouldThrow_Test() =>
		AppendAsync_UnaddressableAggregateId_ShouldThrow();

	/// <summary>SAFETY: an append past the stream tail is refused as a conflict rather than leaving a gap in the version sequence.</summary>
	[Fact]
	public Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict_Test() =>
		AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict();

	/// <summary>SAFETY: an append to a stream that does not exist is refused unless it claims the empty stream.</summary>
	[Fact]
	public Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() =>
		AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();

	#endregion

	#region Load arms

	/// <summary>A stream that was never written reads back empty rather than faulting.</summary>
	[Fact]
	public Task LoadAsync_EmptyStream_ShouldReturnEmpty_Test() =>
		LoadAsync_EmptyStream_ShouldReturnEmpty();

	/// <summary>LIVENESS: a written stream reads back its complete history.</summary>
	[Fact]
	public Task LoadAsync_ExistingStream_ShouldReturnAllEvents_Test() =>
		LoadAsync_ExistingStream_ShouldReturnAllEvents();

	/// <summary>Events read back in strictly ascending version order - replay depends on it.</summary>
	[Fact]
	public Task LoadAsync_ShouldReturnEventsInVersionOrder_Test() =>
		LoadAsync_ShouldReturnEventsInVersionOrder();

	/// <summary>A from-version read returns exactly the events after that version.</summary>
	[Fact]
	public Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion_Test() =>
		LoadAsync_FromVersion_ShouldReturnEventsAfterVersion();

	/// <summary>A from-version read past the end of the stream is empty, not an error.</summary>
	[Fact]
	public Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty_Test() =>
		LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty();

	/// <summary>The from-version bound is exclusive: loading from version zero returns every event after the first, and not the first.</summary>
	[Fact]
	public Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst_Test() =>
		LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst();

	/// <summary>LIVENESS: many callers arriving at once on a cold store all succeed rather than racing its initialisation.</summary>
	[Fact]
	public Task ConcurrentFirstUse_ShouldNotFault_Test() =>
		ConcurrentFirstUse_ShouldNotFault();

	#endregion

	#region Isolation arms

	/// <summary>SAFETY: one aggregate identifier under two types addresses two separate streams.</summary>
	[Fact]
	public Task LoadAsync_ShouldIsolateByAggregateType_Test() =>
		LoadAsync_ShouldIsolateByAggregateType();

	/// <summary>SAFETY: two aggregates of one type do not bleed into each other's history.</summary>
	[Fact]
	public Task LoadAsync_ShouldIsolateByAggregateId_Test() =>
		LoadAsync_ShouldIsolateByAggregateId();

	#endregion

	#region Round-trip arms

	/// <summary>An event's identity and version survive the write/read round trip intact.</summary>
	[Fact]
	public Task AppendAndLoad_ShouldPreserveEventData_Test() =>
		AppendAndLoad_ShouldPreserveEventData();

	/// <summary>Event metadata survives the write/read round trip - it is not silently dropped.</summary>
	[Fact]
	public Task AppendAndLoad_ShouldPreserveMetadata_Test() =>
		AppendAndLoad_ShouldPreserveMetadata();

	#endregion

	#region Tenancy arms

	/// <summary>SAFETY: one tenant's events must not be readable by another.</summary>
	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test() =>
		TenantScopedLoad_MustNotSeeAnotherTenantsEvents();

	/// <summary>LIVENESS: a tenant must read back its own complete history.</summary>
	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnEvents_Test() =>
		TenantScopedLoad_MustSeeItsOwnEvents();

	/// <summary>Two tenants sharing an aggregate identifier must version it independently.</summary>
	[Fact]
	public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test() =>
		TenantPartitions_MustVersionTheSameAggregateIndependently();

	/// <summary>LIVENESS: a host that established no tenant still round-trips its own events.</summary>
	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test() =>
		UntenantedPartition_MustRoundTripItsOwnEvents();

	#endregion

	/// <summary>
	/// The harness guard: fails if this suite has left any kit arm unwired, so an arm added to the kit
	/// later cannot silently never run here.
	/// </summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	/// <summary>
	/// Builds an Oracle store by hand for the arms below, which need two independent instances.
	/// </summary>
	/// <returns>A freshly constructed <see cref="OracleEventStore"/> over the fixture's connection.</returns>
	private OracleEventStore CreateOracleStore() =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			tenantContext: SingleTenantTestContext.Instance,
			payloadSerializer: null,
			schema: _fixture.Schema,
			table: _fixture.TableName);

	[Fact]
	[Trait("Category", "Integration")]
	public async Task Append_Then_ReloadOnFreshInstance_SeesPersistedVersion()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const string aggregateType = "OracleConformanceAggregate";

		var writer = CreateOracleStore();
		var events = new IDomainEvent[]
		{
			new ConformanceEvent(aggregateId, 1),
			new ConformanceEvent(aggregateId, 2),
		};

		var append = await writer.AppendAsync(aggregateId, aggregateType, events, expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		append.Success.ShouldBeTrue(append.ErrorMessage);

		// Fresh instance — nothing shared with the writer but the real database.
		var reader = CreateOracleStore();
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
		var store = CreateOracleStore();

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
		var store = CreateOracleStore();

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
		var store = CreateOracleStore();

		var evt = new ConformanceEvent(aggregateId, 1);
		var expectedUtc = evt.OccurredAt.ToUniversalTime();

		var append = await store.AppendAsync(
				aggregateId, aggregateType, new IDomainEvent[] { evt },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		append.Success.ShouldBeTrue(append.ErrorMessage);

		// Fresh instance — forces a real materialization from Oracle, exercising the OracleEventRow ctor.
		var reader = CreateOracleStore();
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
		var seed = await CreateOracleStore().AppendAsync(
				aggregateId, aggregateType,
				new IDomainEvent[] { new ConformanceEvent(aggregateId, 0), new ConformanceEvent(aggregateId, 1) },
				expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		seed.Success.ShouldBeTrue(seed.ErrorMessage);

		// Two concurrent MULTI-event appends, both at expectedVersion 1, on independent connections.
		async Task<AppendResult> AppendTwoAtVersionOne() =>
			await CreateOracleStore().AppendAsync(
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

[MessageName("Test.ConformanceEvent")]
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


		public IDictionary<string, object>? Metadata => null;
	}
}
