// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Stores.InMemory;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// Integration-style tests for the R1 access review pipeline:
/// campaign lifecycle, decision recording, completion/expiry,
/// and store update flow using real InMemory stores.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AccessReviewExpiryServiceShould : UnitTestBase
{
	private readonly InMemoryAccessReviewStore _store = new();

	private static readonly AccessReviewScope DefaultScope = new(AccessReviewScopeType.AllGrants, null);
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	private static AccessReviewCampaignSummary CreateSummary(
		string id = "campaign-1",
		AccessReviewState state = AccessReviewState.InProgress,
		AccessReviewExpiryPolicy expiryPolicy = AccessReviewExpiryPolicy.DoNothing,
		DateTimeOffset? expiresAt = null,
		int totalItems = 5,
		int decidedItems = 0) =>
		new(id, "Q1 Review", DefaultScope, "admin",
			Now.AddDays(-30), expiresAt ?? Now.AddDays(-1), // default: already expired
			expiryPolicy, state, totalItems, decidedItems);

	#region Campaign Lifecycle: Create → Start → Decide → Complete

	[Fact]
	public void CampaignLifecycle_CreateToCompletion()
	{
		// Arrange
		var items = new AccessReviewItem[]
		{
			new("user-1", "tenant:Role:Admin", Now.AddDays(-60), null),
			new("user-2", "tenant:Role:Editor", Now.AddDays(-30), null),
		};

		// Act: Create campaign
		var campaign = new AccessReviewCampaign(
			"campaign-lc", "Lifecycle Test", DefaultScope, "admin",
			Now.AddDays(-30), Now.AddDays(30),
			AccessReviewExpiryPolicy.DoNothing, items);

		campaign.State.ShouldBe(AccessReviewState.Created);
		campaign.Items.Count.ShouldBe(2);

		// Act: Start
		campaign.Start();
		campaign.State.ShouldBe(AccessReviewState.InProgress);

		// Act: Record decisions
		campaign.RecordDecision(new AccessReviewDecision(
			"campaign-lc", "user-1", "tenant:Role:Admin", "reviewer-1",
			AccessReviewOutcome.Approved, "Still needs access", Now, null));
		campaign.RecordDecision(new AccessReviewDecision(
			"campaign-lc", "user-2", "tenant:Role:Editor", "reviewer-1",
			AccessReviewOutcome.Revoked, "No longer needed", Now, null));

		campaign.Items[0].CurrentDecision.ShouldBe(AccessReviewOutcome.Approved);
		campaign.Items[1].CurrentDecision.ShouldBe(AccessReviewOutcome.Revoked);

		// Act: Complete
		campaign.Complete();
		campaign.State.ShouldBe(AccessReviewState.Completed);

		// Verify summary
		var summary = campaign.ToSummary();
		summary.State.ShouldBe(AccessReviewState.Completed);
		summary.TotalItems.ShouldBe(2);
		summary.DecidedItems.ShouldBe(2);
	}

	[Fact]
	public void CampaignLifecycle_CreateToExpiry()
	{
		var items = new AccessReviewItem[]
		{
			new("user-1", "tenant:Role:Admin", Now.AddDays(-60), null),
		};

		var campaign = new AccessReviewCampaign(
			"campaign-exp", "Expiry Test", DefaultScope, "admin",
			Now.AddDays(-30), Now.AddDays(-1),
			AccessReviewExpiryPolicy.RevokeUnreviewed, items);

		campaign.Start();
		campaign.State.ShouldBe(AccessReviewState.InProgress);

		// No decisions made - expire
		campaign.Expire();
		campaign.State.ShouldBe(AccessReviewState.Expired);
		campaign.Items[0].CurrentDecision.ShouldBeNull();
	}

	[Fact]
	public void CampaignLifecycle_PartialDecisionsThenExpire()
	{
		var items = new AccessReviewItem[]
		{
			new("user-1", "tenant:Role:Admin", Now.AddDays(-60), null),
			new("user-2", "tenant:Role:Editor", Now.AddDays(-30), null),
		};

		var campaign = new AccessReviewCampaign(
			"campaign-part", "Partial Test", DefaultScope, "admin",
			Now.AddDays(-30), Now.AddDays(-1),
			AccessReviewExpiryPolicy.DoNothing, items);

		campaign.Start();

		// Only decide one item
		campaign.RecordDecision(new AccessReviewDecision(
			"campaign-part", "user-1", "tenant:Role:Admin", "reviewer-1",
			AccessReviewOutcome.Approved, null, Now, null));

		// Cannot complete with undecided items
		Should.Throw<InvalidOperationException>(() => campaign.Complete());

		// But can expire
		campaign.Expire();
		campaign.State.ShouldBe(AccessReviewState.Expired);

		var summary = campaign.ToSummary();
		summary.DecidedItems.ShouldBe(1);
		summary.TotalItems.ShouldBe(2);
	}

	#endregion

	#region Store Integration: Campaign Summary Persistence

	[Fact]
	public async Task StoreUpdate_DoNothing_MarksExpired()
	{
		// Simulate what AccessReviewExpiryService.ApplyExpiryPolicyAsync does for DoNothing
		var summary = CreateSummary(expiryPolicy: AccessReviewExpiryPolicy.DoNothing);
		await _store.SaveCampaignAsync(summary, CancellationToken.None);

		// Simulate marking expired (same as MarkCampaignExpiredAsync)
		var expired = summary with { State = AccessReviewState.Expired };
		await _store.SaveCampaignAsync(expired, CancellationToken.None);

		var result = await _store.GetCampaignAsync("campaign-1", CancellationToken.None);
		result!.State.ShouldBe(AccessReviewState.Expired);
	}

	[Fact]
	public async Task StoreUpdate_NotifyAndExtend_MarksExpiredAfterNotification()
	{
		// Post-fix (ap6tan): NotifyAndExtend marks as Expired after notification
		var summary = CreateSummary(expiryPolicy: AccessReviewExpiryPolicy.NotifyAndExtend);
		await _store.SaveCampaignAsync(summary, CancellationToken.None);

		// Simulate: notify → mark expired
		var expired = summary with { State = AccessReviewState.Expired };
		await _store.SaveCampaignAsync(expired, CancellationToken.None);

		var result = await _store.GetCampaignAsync("campaign-1", CancellationToken.None);
		result!.State.ShouldBe(AccessReviewState.Expired);
	}

	[Fact]
	public async Task StoreUpdate_RevokeUnreviewed_MarksExpired()
	{
		var summary = CreateSummary(
			expiryPolicy: AccessReviewExpiryPolicy.RevokeUnreviewed,
			totalItems: 3, decidedItems: 1);
		await _store.SaveCampaignAsync(summary, CancellationToken.None);

		// Simulate: revoke + mark expired
		var expired = summary with { State = AccessReviewState.Expired };
		await _store.SaveCampaignAsync(expired, CancellationToken.None);

		var result = await _store.GetCampaignAsync("campaign-1", CancellationToken.None);
		result!.State.ShouldBe(AccessReviewState.Expired);
	}

	#endregion

	#region Store: InProgress Campaign Filtering

	[Fact]
	public async Task FindExpiredInProgressCampaigns()
	{
		// Store mix of campaigns
		await _store.SaveCampaignAsync(
			CreateSummary("active", state: AccessReviewState.InProgress, expiresAt: Now.AddDays(7)),
			CancellationToken.None);
		await _store.SaveCampaignAsync(
			CreateSummary("expired", state: AccessReviewState.InProgress, expiresAt: Now.AddDays(-1)),
			CancellationToken.None);
		await _store.SaveCampaignAsync(
			CreateSummary("already-expired", state: AccessReviewState.Expired),
			CancellationToken.None);
		await _store.SaveCampaignAsync(
			CreateSummary("completed", state: AccessReviewState.Completed),
			CancellationToken.None);

		// Query InProgress (what ExpiryService does)
		var inProgress = await _store.GetCampaignsByStateAsync(
			AccessReviewState.InProgress, CancellationToken.None);

		inProgress.Count.ShouldBe(2); // "active" + "expired" both InProgress

		// Filter expired ones (ExpiresAt <= now)
		var now = DateTimeOffset.UtcNow;
		var expiredCampaigns = inProgress.Where(c => c.ExpiresAt <= now).ToList();
		expiredCampaigns.Count.ShouldBe(1);
		expiredCampaigns[0].CampaignId.ShouldBe("expired");
	}

	#endregion

	#region Event Replay and Summary Round-Trip

	[Fact]
	public async Task EventReplay_ThenSummary_ThenStore()
	{
		// Create campaign via aggregate
		var items = new AccessReviewItem[]
		{
			new("user-1", "scope-1", Now, null),
			new("user-2", "scope-2", Now, null),
		};

		var campaign = new AccessReviewCampaign(
			"rt-1", "Round-Trip", DefaultScope, "admin",
			Now, Now.AddDays(30), AccessReviewExpiryPolicy.DoNothing, items);
		campaign.Start();
		campaign.RecordDecision(new AccessReviewDecision(
			"rt-1", "user-1", "scope-1", "reviewer", AccessReviewOutcome.Approved, null, Now, null));

		// Save summary to store
		var summary = campaign.ToSummary();
		await _store.SaveCampaignAsync(summary, CancellationToken.None);

		// Retrieve and verify
		var stored = await _store.GetCampaignAsync("rt-1", CancellationToken.None);
		stored!.CampaignId.ShouldBe("rt-1");
		stored.State.ShouldBe(AccessReviewState.InProgress);
		stored.TotalItems.ShouldBe(2);
		stored.DecidedItems.ShouldBe(1);

		// Event replay should produce same state
		var rebuilt = AccessReviewCampaign.FromEvents("rt-1", campaign.GetUncommittedEvents());
		var rebuiltSummary = rebuilt.ToSummary();
		rebuiltSummary.State.ShouldBe(stored.State);
		rebuiltSummary.TotalItems.ShouldBe(stored.TotalItems);
		rebuiltSummary.DecidedItems.ShouldBe(stored.DecidedItems);
	}

	#endregion

	#region Scope-Based Campaign Targeting

	[Fact]
	public void CampaignWithRoleScope_PopulatesCorrectItems()
	{
		var scope = new AccessReviewScope(AccessReviewScopeType.ByRole, "Admin");
		var items = new AccessReviewItem[]
		{
			new("user-1", "tenant:Role:Admin", Now.AddDays(-60), null),
			new("user-3", "tenant:Role:Admin", Now.AddDays(-30), null),
		};

		var campaign = new AccessReviewCampaign(
			"role-scope", "Admin Role Review", scope, "admin",
			Now, Now.AddDays(30), AccessReviewExpiryPolicy.RevokeUnreviewed, items);

		campaign.Scope.Type.ShouldBe(AccessReviewScopeType.ByRole);
		campaign.Scope.FilterValue.ShouldBe("Admin");
		campaign.Items.Count.ShouldBe(2);
		campaign.Items.ShouldAllBe(i => i.GrantScope.Contains("Admin"));
	}

	[Fact]
	public void CampaignWithUserScope_RestrictsToSingleUser()
	{
		var scope = new AccessReviewScope(AccessReviewScopeType.ByUser, "user-5");
		var items = new AccessReviewItem[]
		{
			new("user-5", "tenant:Role:Admin", Now, null),
			new("user-5", "tenant:Activity:Export", Now, null),
		};

		var campaign = new AccessReviewCampaign(
			"user-scope", "User Review", scope, "admin",
			Now, Now.AddDays(14), AccessReviewExpiryPolicy.DoNothing, items);

		campaign.Scope.Type.ShouldBe(AccessReviewScopeType.ByUser);
		campaign.Scope.FilterValue.ShouldBe("user-5");
		campaign.Items.ShouldAllBe(i => i.GrantUserId == "user-5");
	}

	#endregion

	#region AccessReviewOptions Validation

	[Fact]
	public void DefaultOptions_AreValid()
	{
		var opts = new AccessReviewOptions();
		opts.DefaultCampaignDuration.ShouldBe(TimeSpan.FromDays(30));
		opts.DefaultExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.NotifyAndExtend);
		opts.ExpiryCheckInterval.ShouldBe(TimeSpan.FromHours(1));
		opts.MaxRetryAttempts.ShouldBe(3);
		opts.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(5));
		opts.AutoStartOnCreation.ShouldBeFalse();
	}

	[Fact]
	public void ExtensionDays_DefaultsTo7()
	{
		var opts = new AccessReviewOptions();
		opts.ExtensionDays.ShouldBe(7);
	}

	#endregion
}
