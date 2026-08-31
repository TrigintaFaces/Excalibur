// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.InMemory;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Runs the <strong>complete</strong> <see cref="EventStoreConformanceTestKit"/> - every arm - against the
/// in-memory event store, through that provider's OWN registration extension.
/// </summary>
/// <remarks>
/// <para>
/// This is the event store a consumer receives when they configure no provider at all, so it is the
/// implementation most consumers meet first and the one whose unverified state was hardest to justify: the
/// published contract had never been run against the default path. It is also the store every consumer
/// test host uses, so a defect here is invisible in exactly the place people look for defects.
/// </para>
/// <para>
/// The store is resolved from <c>UseInMemory</c> rather than constructed: <c>InMemoryEventStore</c> is
/// <c>internal sealed</c>, so hand-construction is not available from outside its assembly anyway - and
/// that is the correct constraint rather than an obstacle, since the registration is the only surface a
/// consumer has.
/// </para>
/// <para>
/// No container and no I/O, so these arms are inherently non-skipped.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
public sealed class InMemoryEventStoreConformanceTests : EventStoreConformanceTestKit
{
	/// <inheritdoc />
	/// <remarks>
	/// The provider's own shipped registration and nothing else. The kit supplies the ambient
	/// <c>ITenantContext</c> before this runs and <c>AddInMemoryEventStore</c> reaches its default through
	/// <c>TryAdd</c>, so the kit's context survives and the tenancy arms address partitions it controls.
	/// </remarks>
	protected override void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver)
	{
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseInMemory(null)));

		// The in-memory builder exposes no resolver method, so the consumer surface is the options type
		// the store reads. Configure runs after the registration above and never touches this property
		// itself, so setting it here is order-independent.
		_ = services.Configure<InMemoryEventStoreOptions>(
			options => options.EventTypeInfoResolver = eventTypeInfoResolver);
	}

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
}
