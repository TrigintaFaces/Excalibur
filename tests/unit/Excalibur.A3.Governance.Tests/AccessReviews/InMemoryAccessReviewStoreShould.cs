// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Stores.InMemory;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// Unit tests for <see cref="InMemoryAccessReviewStore"/>: CRUD, state filtering,
/// concurrent access, and GetService escape hatch.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryAccessReviewStoreShould : UnitTestBase
{
	private readonly InMemoryAccessReviewStore _store = new();

	private static AccessReviewCampaignSummary CreateSummary(
		string id = "campaign-1",
		string name = "Q1 Review",
		AccessReviewState state = AccessReviewState.Created,
		int totalItems = 5,
		int decidedItems = 0) =>
		new(id, name,
			new AccessReviewScope(AccessReviewScopeType.AllGrants, null),
			"admin",
			new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
			AccessReviewExpiryPolicy.NotifyAndExtend,
			state, totalItems, decidedItems);

	#region SaveCampaignAsync

	[Fact]
	public async Task SaveAndRetrieveCampaign()
	{
		// Arrange
		var summary = CreateSummary();

		// Act
		await _store.SaveCampaignAsync(summary, CancellationToken.None);
		var result = await _store.GetCampaignAsync("campaign-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.CampaignId.ShouldBe("campaign-1");
		result.CampaignName.ShouldBe("Q1 Review");
		result.TotalItems.ShouldBe(5);
	}

	[Fact]
	public async Task UpsertCampaign_WhenSavedTwice()
	{
		var original = CreateSummary(name: "Original");
		var updated = CreateSummary(name: "Updated", state: AccessReviewState.InProgress);

		await _store.SaveCampaignAsync(original, CancellationToken.None);
		await _store.SaveCampaignAsync(updated, CancellationToken.None);

		var result = await _store.GetCampaignAsync("campaign-1", CancellationToken.None);
		result!.CampaignName.ShouldBe("Updated");
		result.State.ShouldBe(AccessReviewState.InProgress);
	}

	[Fact]
	public async Task ThrowOnSave_WhenNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_store.SaveCampaignAsync(null!, CancellationToken.None));
	}

	#endregion

	#region GetCampaignAsync

	[Fact]
	public async Task ReturnNull_WhenCampaignNotFound()
	{
		var result = await _store.GetCampaignAsync("nonexistent", CancellationToken.None);
		result.ShouldBeNull();
	}

	#endregion

	#region GetCampaignsByStateAsync

	[Fact]
	public async Task FilterByState()
	{
		await _store.SaveCampaignAsync(CreateSummary("c1", state: AccessReviewState.Created), CancellationToken.None);
		await _store.SaveCampaignAsync(CreateSummary("c2", state: AccessReviewState.InProgress), CancellationToken.None);
		await _store.SaveCampaignAsync(CreateSummary("c3", state: AccessReviewState.InProgress), CancellationToken.None);
		await _store.SaveCampaignAsync(CreateSummary("c4", state: AccessReviewState.Completed), CancellationToken.None);

		var inProgress = await _store.GetCampaignsByStateAsync(AccessReviewState.InProgress, CancellationToken.None);
		inProgress.Count.ShouldBe(2);
		inProgress.ShouldAllBe(c => c.State == AccessReviewState.InProgress);
	}

	[Fact]
	public async Task ReturnAllCampaigns_WhenStateIsNull()
	{
		await _store.SaveCampaignAsync(CreateSummary("c1", state: AccessReviewState.Created), CancellationToken.None);
		await _store.SaveCampaignAsync(CreateSummary("c2", state: AccessReviewState.InProgress), CancellationToken.None);

		var all = await _store.GetCampaignsByStateAsync(null, CancellationToken.None);
		all.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnEmptyList_WhenNoMatchingState()
	{
		await _store.SaveCampaignAsync(CreateSummary("c1", state: AccessReviewState.Created), CancellationToken.None);

		var expired = await _store.GetCampaignsByStateAsync(AccessReviewState.Expired, CancellationToken.None);
		expired.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmptyList_WhenStoreIsEmpty()
	{
		var result = await _store.GetCampaignsByStateAsync(null, CancellationToken.None);
		result.ShouldBeEmpty();
	}

	#endregion

	#region DeleteCampaignAsync

	[Fact]
	public async Task DeleteExistingCampaign()
	{
		await _store.SaveCampaignAsync(CreateSummary(), CancellationToken.None);

		var deleted = await _store.DeleteCampaignAsync("campaign-1", CancellationToken.None);
		deleted.ShouldBeTrue();

		var result = await _store.GetCampaignAsync("campaign-1", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnFalse_WhenDeletingNonexistent()
	{
		var deleted = await _store.DeleteCampaignAsync("nonexistent", CancellationToken.None);
		deleted.ShouldBeFalse();
	}

	#endregion

	#region GetService

	[Fact]
	public void ReturnNull_FromGetService()
	{
		_store.GetService(typeof(string)).ShouldBeNull();
		_store.GetService(typeof(IAccessReviewStore)).ShouldBeNull();
	}

	[Fact]
	public void ThrowOnGetService_WhenNull()
	{
		Should.Throw<ArgumentNullException>(() => _store.GetService(null!));
	}

	#endregion

	#region Concurrent Access

	[Fact]
	public async Task HandleConcurrentSaves()
	{
		var tasks = Enumerable.Range(0, 50)
			.Select(i => _store.SaveCampaignAsync(
				CreateSummary($"campaign-{i}"), CancellationToken.None));

		await Task.WhenAll(tasks);

		var all = await _store.GetCampaignsByStateAsync(null, CancellationToken.None);
		all.Count.ShouldBe(50);
	}

	[Fact]
	public async Task HandleConcurrentSaveAndDelete()
	{
		// Pre-populate
		for (int i = 0; i < 20; i++)
		{
			await _store.SaveCampaignAsync(CreateSummary($"c-{i}"), CancellationToken.None);
		}

		// Concurrently save new and delete existing
		var saveTasks = Enumerable.Range(20, 20)
			.Select(i => _store.SaveCampaignAsync(
				CreateSummary($"c-{i}"), CancellationToken.None));
		var deleteTasks = Enumerable.Range(0, 20)
			.Select(i => _store.DeleteCampaignAsync($"c-{i}", CancellationToken.None));

		await Task.WhenAll(saveTasks.Concat(deleteTasks));

		// Only the newly saved ones should remain
		var remaining = await _store.GetCampaignsByStateAsync(null, CancellationToken.None);
		remaining.Count.ShouldBe(20);
	}

	#endregion
}
