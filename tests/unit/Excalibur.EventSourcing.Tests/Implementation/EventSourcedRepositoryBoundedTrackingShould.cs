// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// Author≠impl regression lock for S850 Lane D · <c>1ramx6</c> (unbounded <c>_snapshotTracking</c> growth on
/// <see cref="EventSourcedRepository{TAggregate}"/>).
/// </summary>
/// <remarks>
/// <para>
/// Authored by FrontendDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>) against the frozen GUIDE seam (msg 15508): snapshot-tracking writes
/// route through a bounded <c>TrackSnapshotState</c> that adds a brand-new aggregate only while the map is
/// below <c>MaxTrackedAggregates</c> (1024), but always applies an update to an already-tracked aggregate —
/// the RetryMiddleware cap=1024 / skip-when-full pattern. Beyond the cap a later miss makes <c>SaveAsync</c>
/// re-derive the auto-snapshot decision, so the policy degrades safely rather than leaking memory.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix code wrote <c>_snapshotTracking[id] = state</c>
/// directly with no bound — there is no <c>TrackSnapshotState</c> method at all. This lock fails RED on that
/// surface twice over: the bounding method is absent (the <c>ShouldNotBeNull</c> guard), and driving more
/// than the cap of distinct aggregates would leave the map at the full count rather than 1024. On the fixed
/// surface the map is capped at exactly 1024, an update to an already-tracked aggregate still applies, and a
/// brand-new aggregate past the cap is rejected — GREEN. Deterministic: the invariant is asserted on the real
/// method and field, no timing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryBoundedTrackingShould
{
	// Mirrors EventSourcedRepository.MaxTrackedAggregates (private const). If the impl changes the cap,
	// this lock must change with it — that is the point of pinning the value.
	private const int MaxTrackedAggregates = 1024;

	[Fact]
	public void BoundSnapshotTrackingAtTheCap_ApplyingUpdatesButRejectingNewAggregatesWhenFull()
	{
		// Arrange — a repository with no snapshot manager; we drive the bounded write helper directly so the
		// lock is independent of the (heavy) snapshot-load fixture.
		var repository = new EventSourcedRepository<TrackingTestAggregate>(
			A.Fake<IEventStore>(),
			A.Fake<IEventSerializer>(),
			id => new TrackingTestAggregate(id));

		// The bounded helper + tracking map are declared on the two-type-parameter base.
		var baseType = repository.GetType().BaseType.ShouldNotBeNull();

		var trackingField = baseType
			.GetField("_snapshotTracking", BindingFlags.NonPublic | BindingFlags.Instance)
			.ShouldNotBeNull("_snapshotTracking field not found — seam changed");
		var trackingMap = trackingField.GetValue(repository).ShouldNotBeNull();

		var trackMethod = baseType
			.GetMethod("TrackSnapshotState", BindingFlags.NonPublic | BindingFlags.Instance)
			.ShouldNotBeNull(
				"pre-fix surface has no bounding TrackSnapshotState — writes go straight to _snapshotTracking " +
				"unbounded. The fix introduces this gate (RED here on the pre-fix code).");

		// Construct the private SnapshotTrackingState via the map's value type (the field's 2nd generic arg)
		// to avoid nested-generic GetNestedType friction.
		var stateType = trackingField.FieldType.GetGenericArguments()[1];
		object State(long version) =>
			Activator.CreateInstance(stateType, version, DateTimeOffset.UnixEpoch).ShouldNotBeNull();

		void Track(string aggregateId, long version) =>
			trackMethod.Invoke(repository, [aggregateId, State(version)]);

		int Count() => (int)trackingMap.GetType().GetProperty("Count").ShouldNotBeNull()
			.GetValue(trackingMap).ShouldNotBeNull();

		// Act — record well beyond the cap, each a distinct aggregate.
		for (var i = 0; i < MaxTrackedAggregates + 64; i++)
		{
			Track($"agg-{i}", version: i);
		}

		// Assert — bounded at exactly the cap (pre-fix would be MaxTrackedAggregates + 64).
		Count().ShouldBe(MaxTrackedAggregates);

		// An update to an ALREADY-tracked aggregate still applies and never grows the map.
		Track("agg-0", version: 999);
		Count().ShouldBe(MaxTrackedAggregates);

		// A brand-new aggregate while the map is full is rejected (skip-when-full), not added.
		Track("agg-OVERFLOW", version: 1);
		Count().ShouldBe(MaxTrackedAggregates);
		((IDictionary)trackingMap).Contains("agg-OVERFLOW").ShouldBeFalse();
	}

	private sealed class TrackingTestAggregate : AggregateRoot
	{
		public TrackingTestAggregate()
		{
		}

		public TrackingTestAggregate(string id) : base(id)
		{
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No state transitions needed: this lock never replays events.
		}
	}
}
