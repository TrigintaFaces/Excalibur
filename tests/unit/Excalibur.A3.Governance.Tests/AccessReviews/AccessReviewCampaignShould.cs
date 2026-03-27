// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Events;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// Unit tests for <see cref="AccessReviewCampaign"/> aggregate: creation, state machine,
/// decision recording, event replay via <see cref="AccessReviewCampaign.FromEvents"/>,
/// and summary projection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AccessReviewCampaignShould : UnitTestBase
{
	private static readonly AccessReviewScope DefaultScope = new(AccessReviewScopeType.AllGrants, null);
	private static readonly DateTimeOffset DefaultStart = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset DefaultExpiry = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

	private static readonly IReadOnlyList<AccessReviewItem> DefaultItems =
	[
		new("user-1", "tenant:Role:Admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null),
		new("user-2", "tenant:Role:Editor", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), null),
	];

	private static AccessReviewCampaign CreateCampaign(
		string id = "campaign-1",
		string name = "Q1 Review",
		AccessReviewScope? scope = null,
		string createdBy = "admin",
		DateTimeOffset? startsAt = null,
		DateTimeOffset? expiresAt = null,
		AccessReviewExpiryPolicy expiryPolicy = AccessReviewExpiryPolicy.NotifyAndExtend,
		IReadOnlyList<AccessReviewItem>? items = null) =>
		new(id, name, scope ?? DefaultScope, createdBy,
			startsAt ?? DefaultStart, expiresAt ?? DefaultExpiry,
			expiryPolicy, items ?? DefaultItems);

	private static AccessReviewDecision MakeDecision(
		string userId = "user-1",
		string grantScope = "tenant:Role:Admin",
		string reviewerId = "reviewer-1",
		AccessReviewOutcome outcome = AccessReviewOutcome.Approved,
		string? justification = "Looks good") =>
		new("campaign-1", userId, grantScope, reviewerId, outcome, justification,
			DateTimeOffset.UtcNow, null);

	#region Creation

	[Fact]
	public void SetProperties_WhenCreated()
	{
		// Act
		var campaign = CreateCampaign();

		// Assert
		campaign.Id.ShouldBe("campaign-1");
		campaign.CampaignName.ShouldBe("Q1 Review");
		campaign.Scope.ShouldBe(DefaultScope);
		campaign.CreatedBy.ShouldBe("admin");
		campaign.StartsAt.ShouldBe(DefaultStart);
		campaign.ExpiresAt.ShouldBe(DefaultExpiry);
		campaign.ExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.NotifyAndExtend);
		campaign.State.ShouldBe(AccessReviewState.Created);
		campaign.Items.Count.ShouldBe(2);
		campaign.Decisions.ShouldBeEmpty();
	}

	[Fact]
	public void RaiseCampaignCreatedEvent()
	{
		// Act
		var campaign = CreateCampaign();

		// Assert
		var events = campaign.GetUncommittedEvents().ToList();
		events.Count.ShouldBe(1);
		var created = events[0].ShouldBeOfType<AccessReviewCampaignCreated>();
		created.CampaignId.ShouldBe("campaign-1");
		created.CampaignName.ShouldBe("Q1 Review");
		created.Scope.ShouldBe(DefaultScope);
		created.CreatedBy.ShouldBe("admin");
		created.Items.Count.ShouldBe(2);
		created.EventType.ShouldBe("AccessReviewCampaignCreated");
	}

	[Fact]
	public void PopulateItems_AsPointInTimeSnapshot()
	{
		// Act
		var campaign = CreateCampaign();

		// Assert
		campaign.Items[0].GrantUserId.ShouldBe("user-1");
		campaign.Items[0].GrantScope.ShouldBe("tenant:Role:Admin");
		campaign.Items[0].CurrentDecision.ShouldBeNull();
		campaign.Items[1].GrantUserId.ShouldBe("user-2");
		campaign.Items[1].CurrentDecision.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnNullOrEmptyCampaignId(string? id)
	{
		Should.Throw<ArgumentException>(() =>
			CreateCampaign(id: id!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnNullOrEmptyCampaignName(string? name)
	{
		Should.Throw<ArgumentException>(() =>
			CreateCampaign(name: name!));
	}

	[Fact]
	public void ThrowOnNullScope()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AccessReviewCampaign("campaign-1", "Q1 Review", null!, "admin",
				DefaultStart, DefaultExpiry, AccessReviewExpiryPolicy.NotifyAndExtend, DefaultItems));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnNullOrEmptyCreatedBy(string? createdBy)
	{
		Should.Throw<ArgumentException>(() =>
			CreateCampaign(createdBy: createdBy!));
	}

	[Fact]
	public void ThrowOnNullItems()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AccessReviewCampaign("campaign-1", "Q1 Review", DefaultScope, "admin",
				DefaultStart, DefaultExpiry, AccessReviewExpiryPolicy.NotifyAndExtend, null!));
	}

	[Fact]
	public void ThrowWhenExpiresAtBeforeStartsAt()
	{
		Should.Throw<ArgumentException>(() =>
			CreateCampaign(startsAt: DefaultExpiry, expiresAt: DefaultStart))
			.Message.ShouldContain("ExpiresAt must be after StartsAt");
	}

	[Fact]
	public void ThrowWhenExpiresAtEqualsStartsAt()
	{
		Should.Throw<ArgumentException>(() =>
			CreateCampaign(startsAt: DefaultStart, expiresAt: DefaultStart));
	}

	[Fact]
	public void AllowEmptyItemsList()
	{
		// Act -- campaign with no items (e.g., scope matched nothing)
		var campaign = CreateCampaign(items: []);

		// Assert
		campaign.Items.ShouldBeEmpty();
	}

	#endregion

	#region Start

	[Fact]
	public void TransitionToInProgress_WhenStarted()
	{
		// Arrange
		var campaign = CreateCampaign();

		// Act
		campaign.Start();

		// Assert
		campaign.State.ShouldBe(AccessReviewState.InProgress);
	}

	[Fact]
	public void RaiseStartedEvent()
	{
		// Arrange
		var campaign = CreateCampaign();

		// Act
		campaign.Start();

		// Assert
		var events = campaign.GetUncommittedEvents().ToList();
		events.Count.ShouldBe(2); // Created + Started
		var started = events[1].ShouldBeOfType<AccessReviewCampaignStarted>();
		started.CampaignId.ShouldBe("campaign-1");
	}

	[Fact]
	public void ThrowOnStart_WhenAlreadyInProgress()
	{
		var campaign = CreateCampaign();
		campaign.Start();

		Should.Throw<InvalidOperationException>(() => campaign.Start())
			.Message.ShouldContain("InProgress");
	}

	[Fact]
	public void ThrowOnStart_WhenCompleted()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		Should.Throw<InvalidOperationException>(() => campaign.Start())
			.Message.ShouldContain("Completed");
	}

	[Fact]
	public void ThrowOnStart_WhenExpired()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.Expire();

		Should.Throw<InvalidOperationException>(() => campaign.Start())
			.Message.ShouldContain("Expired");
	}

	#endregion

	#region RecordDecision

	[Fact]
	public void RecordValidDecision()
	{
		// Arrange
		var campaign = CreateCampaign();
		campaign.Start();

		// Act
		campaign.RecordDecision(MakeDecision());

		// Assert
		campaign.Items[0].CurrentDecision.ShouldBe(AccessReviewOutcome.Approved);
		campaign.Decisions.Count.ShouldBe(1);
		campaign.Decisions[0].ReviewerId.ShouldBe("reviewer-1");
		campaign.Decisions[0].Outcome.ShouldBe(AccessReviewOutcome.Approved);
		campaign.Decisions[0].Justification.ShouldBe("Looks good");
	}

	[Fact]
	public void RaiseDecisionMadeEvent()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision());

		var events = campaign.GetUncommittedEvents().ToList();
		events.Count.ShouldBe(3); // Created + Started + DecisionMade
		var decision = events[2].ShouldBeOfType<AccessReviewDecisionMade>();
		decision.CampaignId.ShouldBe("campaign-1");
		decision.GrantUserId.ShouldBe("user-1");
		decision.GrantScope.ShouldBe("tenant:Role:Admin");
		decision.ReviewerId.ShouldBe("reviewer-1");
		decision.Outcome.ShouldBe(AccessReviewOutcome.Approved);
		decision.Justification.ShouldBe("Looks good");
	}

	[Fact]
	public void ThrowOnDecision_WhenNotInProgress()
	{
		var campaign = CreateCampaign(); // State = Created
		Should.Throw<InvalidOperationException>(() =>
			campaign.RecordDecision(MakeDecision()))
			.Message.ShouldContain("Created");
	}

	[Fact]
	public void ThrowOnDecision_WhenItemNotFound()
	{
		var campaign = CreateCampaign();
		campaign.Start();

		var decision = MakeDecision(userId: "nonexistent-user", grantScope: "missing:scope");
		Should.Throw<InvalidOperationException>(() =>
			campaign.RecordDecision(decision))
			.Message.ShouldContain("Item not found");
	}

	[Fact]
	public void ThrowOnDecision_WhenAlreadyDecided()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision()); // First decision on user-1

		Should.Throw<InvalidOperationException>(() =>
			campaign.RecordDecision(MakeDecision())) // Duplicate
			.Message.ShouldContain("already decided");
	}

	[Fact]
	public void ThrowOnDecision_WhenNull()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		Should.Throw<ArgumentNullException>(() => campaign.RecordDecision(null!));
	}

	[Fact]
	public void SupportRevokeOutcome()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision(outcome: AccessReviewOutcome.Revoked, justification: "No longer needed"));

		campaign.Items[0].CurrentDecision.ShouldBe(AccessReviewOutcome.Revoked);
		campaign.Decisions[0].Justification.ShouldBe("No longer needed");
	}

	[Fact]
	public void SupportDelegatedOutcome()
	{
		var campaign = CreateCampaign();
		campaign.Start();

		var decision = new AccessReviewDecision(
			"campaign-1", "user-1", "tenant:Role:Admin", "reviewer-1",
			AccessReviewOutcome.Delegated, "Need manager review",
			DateTimeOffset.UtcNow, "manager-1");
		campaign.RecordDecision(decision);

		campaign.Decisions[0].Outcome.ShouldBe(AccessReviewOutcome.Delegated);
		campaign.Decisions[0].DelegateToReviewerId.ShouldBe("manager-1");
	}

	[Fact]
	public void RecordMultipleDecisions_ForDifferentItems()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision(userId: "user-1", grantScope: "tenant:Role:Admin"));
		campaign.RecordDecision(MakeDecision(userId: "user-2", grantScope: "tenant:Role:Editor",
			outcome: AccessReviewOutcome.Revoked));

		campaign.Items[0].CurrentDecision.ShouldBe(AccessReviewOutcome.Approved);
		campaign.Items[1].CurrentDecision.ShouldBe(AccessReviewOutcome.Revoked);
		campaign.Decisions.Count.ShouldBe(2);
	}

	#endregion

	#region Complete

	[Fact]
	public void TransitionToCompleted_WhenAllDecided()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		campaign.State.ShouldBe(AccessReviewState.Completed);
	}

	[Fact]
	public void RaiseCompletedEvent()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		var events = campaign.GetUncommittedEvents().ToList();
		var completed = events.Last().ShouldBeOfType<AccessReviewCampaignCompleted>();
		completed.CampaignId.ShouldBe("campaign-1");
	}

	[Fact]
	public void ThrowOnComplete_WhenUndecidedItemsRemain()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision(userId: "user-1", grantScope: "tenant:Role:Admin"));
		// user-2 still undecided

		Should.Throw<InvalidOperationException>(() => campaign.Complete())
			.Message.ShouldContain("undecided items remain");
	}

	[Fact]
	public void ThrowOnComplete_WhenNotInProgress()
	{
		var campaign = CreateCampaign();
		Should.Throw<InvalidOperationException>(() => campaign.Complete())
			.Message.ShouldContain("Created");
	}

	[Fact]
	public void ThrowOnComplete_WhenAlreadyCompleted()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		Should.Throw<InvalidOperationException>(() => campaign.Complete())
			.Message.ShouldContain("Completed");
	}

	[Fact]
	public void CompleteWithEmptyItems()
	{
		// Campaign with no items can be completed immediately (no undecided items)
		var campaign = CreateCampaign(items: []);
		campaign.Start();
		campaign.Complete();

		campaign.State.ShouldBe(AccessReviewState.Completed);
	}

	#endregion

	#region Expire

	[Fact]
	public void TransitionToExpired()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.Expire();

		campaign.State.ShouldBe(AccessReviewState.Expired);
	}

	[Fact]
	public void RaiseExpiredEvent_WithAppliedPolicy()
	{
		var campaign = CreateCampaign(expiryPolicy: AccessReviewExpiryPolicy.RevokeUnreviewed);
		campaign.Start();
		campaign.Expire();

		var events = campaign.GetUncommittedEvents().ToList();
		var expired = events.Last().ShouldBeOfType<AccessReviewCampaignExpired>();
		expired.CampaignId.ShouldBe("campaign-1");
		expired.AppliedPolicy.ShouldBe(AccessReviewExpiryPolicy.RevokeUnreviewed);
	}

	[Fact]
	public void ThrowOnExpire_WhenNotInProgress()
	{
		var campaign = CreateCampaign();
		Should.Throw<InvalidOperationException>(() => campaign.Expire())
			.Message.ShouldContain("Created");
	}

	[Fact]
	public void ThrowOnExpire_WhenAlreadyExpired()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.Expire();

		Should.Throw<InvalidOperationException>(() => campaign.Expire())
			.Message.ShouldContain("Expired");
	}

	[Fact]
	public void ThrowOnExpire_WhenCompleted()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		Should.Throw<InvalidOperationException>(() => campaign.Expire())
			.Message.ShouldContain("Completed");
	}

	#endregion

	#region Event Replay (FromEvents)

	[Fact]
	public void RebuildFromEvents_ViaFromEvents()
	{
		// Arrange
		var original = CreateCampaign();
		original.Start();
		original.RecordDecision(MakeDecision(userId: "user-1", grantScope: "tenant:Role:Admin"));
		original.RecordDecision(MakeDecision(userId: "user-2", grantScope: "tenant:Role:Editor",
			outcome: AccessReviewOutcome.Revoked));
		original.Complete();

		var events = original.GetUncommittedEvents().ToList();

		// Act
		var rebuilt = AccessReviewCampaign.FromEvents("campaign-1", events);

		// Assert
		rebuilt.Id.ShouldBe("campaign-1");
		rebuilt.CampaignName.ShouldBe("Q1 Review");
		rebuilt.Scope.ShouldBe(DefaultScope);
		rebuilt.CreatedBy.ShouldBe("admin");
		rebuilt.StartsAt.ShouldBe(DefaultStart);
		rebuilt.ExpiresAt.ShouldBe(DefaultExpiry);
		rebuilt.State.ShouldBe(AccessReviewState.Completed);
		rebuilt.Items.Count.ShouldBe(2);
		rebuilt.Items[0].CurrentDecision.ShouldBe(AccessReviewOutcome.Approved);
		rebuilt.Items[1].CurrentDecision.ShouldBe(AccessReviewOutcome.Revoked);
		rebuilt.Decisions.Count.ShouldBe(2);
	}

	[Fact]
	public void RebuildExpiredCampaign_ViaFromEvents()
	{
		var original = CreateCampaign(expiryPolicy: AccessReviewExpiryPolicy.DoNothing);
		original.Start();
		original.Expire();

		var rebuilt = AccessReviewCampaign.FromEvents("campaign-1", original.GetUncommittedEvents());
		rebuilt.State.ShouldBe(AccessReviewState.Expired);
		rebuilt.ExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.DoNothing);
	}

	[Fact]
	public void CreateFactory_ReturnsEmptyAggregate()
	{
		var campaign = AccessReviewCampaign.Create("campaign-x");
		campaign.Id.ShouldBe("campaign-x");
		campaign.CampaignName.ShouldBe(string.Empty);
		campaign.State.ShouldBe(default); // 0 = Created
		campaign.Items.ShouldBeEmpty();
		campaign.Decisions.ShouldBeEmpty();
	}

	[Fact]
	public void FromEvents_HasNoUncommittedEvents()
	{
		var original = CreateCampaign();
		original.Start();
		var events = original.GetUncommittedEvents().ToList();

		var rebuilt = AccessReviewCampaign.FromEvents("campaign-1", events);
		rebuilt.GetUncommittedEvents().ShouldBeEmpty();
	}

	#endregion

	#region ToSummary

	[Fact]
	public void ConvertToSummary_AfterCreation()
	{
		var campaign = CreateCampaign();
		var summary = campaign.ToSummary();

		summary.CampaignId.ShouldBe("campaign-1");
		summary.CampaignName.ShouldBe("Q1 Review");
		summary.Scope.ShouldBe(DefaultScope);
		summary.CreatedBy.ShouldBe("admin");
		summary.StartsAt.ShouldBe(DefaultStart);
		summary.ExpiresAt.ShouldBe(DefaultExpiry);
		summary.ExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.NotifyAndExtend);
		summary.State.ShouldBe(AccessReviewState.Created);
		summary.TotalItems.ShouldBe(2);
		summary.DecidedItems.ShouldBe(0);
	}

	[Fact]
	public void ConvertToSummary_WithPartialDecisions()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision());

		var summary = campaign.ToSummary();
		summary.State.ShouldBe(AccessReviewState.InProgress);
		summary.TotalItems.ShouldBe(2);
		summary.DecidedItems.ShouldBe(1);
	}

	[Fact]
	public void ConvertToSummary_WhenCompleted()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		var summary = campaign.ToSummary();
		summary.State.ShouldBe(AccessReviewState.Completed);
		summary.TotalItems.ShouldBe(2);
		summary.DecidedItems.ShouldBe(2);
	}

	#endregion

	#region Cross-State Transitions (invalid)

	[Fact]
	public void ThrowOnDecision_WhenCompleted()
	{
		var campaign = CreateCampaignWithAllDecisions();
		campaign.Complete();

		Should.Throw<InvalidOperationException>(() =>
			campaign.RecordDecision(MakeDecision(userId: "user-1")));
	}

	[Fact]
	public void ThrowOnDecision_WhenExpired()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.Expire();

		Should.Throw<InvalidOperationException>(() =>
			campaign.RecordDecision(MakeDecision()));
	}

	#endregion

	#region Helpers

	private static AccessReviewCampaign CreateCampaignWithAllDecisions()
	{
		var campaign = CreateCampaign();
		campaign.Start();
		campaign.RecordDecision(MakeDecision(userId: "user-1", grantScope: "tenant:Role:Admin"));
		campaign.RecordDecision(MakeDecision(userId: "user-2", grantScope: "tenant:Role:Editor",
			outcome: AccessReviewOutcome.Revoked));
		return campaign;
	}

	#endregion
}
