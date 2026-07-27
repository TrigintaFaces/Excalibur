// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.A3.Governance.Tests;

/// <summary>
/// Replay-version coverage for the event-sourced A3 Governance aggregates
/// (<see cref="AccessReviewCampaign"/> and <see cref="ProvisioningRequest"/>).
/// </summary>
/// <remarks>
/// <para>
/// These locks bind the <em>property</em> that a multi-event aggregate rehydrates from its stream
/// with the correct applied-event <see cref="AggregateRoot{TKey}.Version"/> — not the mechanism that
/// supplies it. Both governance aggregates' creation events implement <c>IDomainEvent</c> without
/// deriving <c>DomainEvent</c>; version is authoritative on the <see cref="HistoricEvent"/> envelope
/// (the durable stream position the store assigned at append), never on the event payload, so these
/// streams replay contiguously with a correct final version.
/// </para>
/// <para>
/// The safety/property arm raises two or more events, replays through the real
/// <see cref="AggregateRoot{TKey}.LoadFromHistory"/> path, and asserts the aggregate returns with the
/// correct state and <c>Version == event count</c>. The liveness arm proves the single-event stream
/// still rehydrates.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GovernanceAggregateReplayVersionShould : UnitTestBase
{
	private static readonly AccessReviewScope DefaultScope = new(AccessReviewScopeType.AllGrants, null);
	private static readonly DateTimeOffset DefaultStart = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset DefaultExpiry = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

	private static readonly IReadOnlyList<AccessReviewItem> DefaultItems =
	[
		new("user-1", "tenant:Role:Admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null),
	];

	private static readonly ApprovalStep Step1 = new("step-1", "Manager", null, null, null, null);

	#region AccessReviewCampaign

	[Fact]
	public void Campaign_RehydratesWithCorrectStateAndVersion_AfterMultiEventReplay()
	{
		// Arrange -- Created + Started == two events.
		var original = new AccessReviewCampaign(
			"campaign-1", "Q1 Review", DefaultScope, "admin",
			DefaultStart, DefaultExpiry, AccessReviewExpiryPolicy.NotifyAndExtend, DefaultItems);
		original.Start();

		var uncommitted = original.GetUncommittedEvents();
		uncommitted.Count.ShouldBe(2);

		// Act
		var rebuilt = AccessReviewCampaign.FromEvents("campaign-1", ToHistory(uncommitted));

		// Assert -- state rehydrated AND Version reflects the two applied events.
		rebuilt.CampaignName.ShouldBe("Q1 Review");
		rebuilt.State.ShouldBe(AccessReviewState.InProgress);
		rebuilt.Items.Count.ShouldBe(1);
		rebuilt.Version.ShouldBe(2);
	}

	[Fact]
	public void Campaign_RehydratesSingleEventStream_WithVersionOne()
	{
		// Arrange -- a single Created event.
		var original = new AccessReviewCampaign(
			"campaign-1", "Q1 Review", DefaultScope, "admin",
			DefaultStart, DefaultExpiry, AccessReviewExpiryPolicy.NotifyAndExtend, DefaultItems);

		var uncommitted = original.GetUncommittedEvents();
		uncommitted.Count.ShouldBe(1);

		// Act
		var rebuilt = AccessReviewCampaign.FromEvents("campaign-1", ToHistory(uncommitted));

		// Assert -- liveness arm.
		rebuilt.State.ShouldBe(AccessReviewState.Created);
		rebuilt.Version.ShouldBe(1);
	}

	#endregion

	#region ProvisioningRequest

	[Fact]
	public void Request_RehydratesWithCorrectStateAndVersion_AfterMultiEventReplay()
	{
		// Arrange -- Created + StepAdvanced (submit) == two events.
		var original = new ProvisioningRequest(
			"req-1", "user-1", "Admin", "Role", "idem-1", 25, "requester", [Step1]);
		original.SubmitForReview();

		var uncommitted = original.GetUncommittedEvents();
		uncommitted.Count.ShouldBe(2);

		// Act
		var rebuilt = ProvisioningRequest.FromEvents("req-1", ToHistory(uncommitted));

		// Assert -- state rehydrated AND Version reflects the two applied events.
		rebuilt.UserId.ShouldBe("user-1");
		rebuilt.Status.ShouldBe(ProvisioningRequestStatus.InReview);
		rebuilt.Version.ShouldBe(2);
	}

	[Fact]
	public void Request_RehydratesSingleEventStream_WithVersionOne()
	{
		// Arrange -- a single Created event (status Pending).
		var original = new ProvisioningRequest(
			"req-1", "user-1", "Admin", "Role", "idem-1", 25, "requester", [Step1]);

		var uncommitted = original.GetUncommittedEvents();
		uncommitted.Count.ShouldBe(1);

		// Act
		var rebuilt = ProvisioningRequest.FromEvents("req-1", ToHistory(uncommitted));

		// Assert -- liveness arm.
		rebuilt.Status.ShouldBe(ProvisioningRequestStatus.Pending);
		rebuilt.Version.ShouldBe(1);
	}

	#endregion

	private static HistoricEvent[] ToHistory(IReadOnlyList<IDomainEvent> events)
	{
		// Assign the contiguous, zero-based stream positions a real event store stamps at append time.
		var history = new HistoricEvent[events.Count];
		for (var i = 0; i < events.Count; i++)
		{
			history[i] = new HistoricEvent(events[i], i);
		}

		return history;
	}
}
