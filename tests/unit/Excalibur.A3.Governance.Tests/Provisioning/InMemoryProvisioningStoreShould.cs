// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;
using Excalibur.A3.Governance.Stores.InMemory;

namespace Excalibur.A3.Governance.Tests.Provisioning;

/// <summary>
/// Unit tests for <see cref="InMemoryProvisioningStore"/>: CRUD, status filtering,
/// concurrent access, null/empty inputs, and GetService escape hatch.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryProvisioningStoreShould : UnitTestBase
{
	private readonly InMemoryProvisioningStore _store = new();

	private static ProvisioningRequestSummary CreateRequest(
		string requestId = "req-1",
		ProvisioningRequestStatus status = ProvisioningRequestStatus.Pending,
		string userId = "user-1",
		string grantScope = "Admin",
		string grantType = "Role",
		int riskScore = 0) =>
		new(requestId, userId, grantScope, grantType, status,
			$"idempotency-{requestId}", riskScore, "requester",
			new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
			[]);

	#region SaveRequestAsync / GetRequestAsync

	[Fact]
	public async Task SaveAndRetrieveRequest()
	{
		// Arrange
		var request = CreateRequest();

		// Act
		await _store.SaveRequestAsync(request, CancellationToken.None);
		var result = await _store.GetRequestAsync("req-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.RequestId.ShouldBe("req-1");
		result.UserId.ShouldBe("user-1");
		result.GrantScope.ShouldBe("Admin");
		result.Status.ShouldBe(ProvisioningRequestStatus.Pending);
	}

	[Fact]
	public async Task UpsertRequest_WhenSavedTwice()
	{
		// Arrange
		var original = CreateRequest(status: ProvisioningRequestStatus.Pending);
		var updated = CreateRequest(status: ProvisioningRequestStatus.InReview);

		// Act
		await _store.SaveRequestAsync(original, CancellationToken.None);
		await _store.SaveRequestAsync(updated, CancellationToken.None);

		var result = await _store.GetRequestAsync("req-1", CancellationToken.None);

		// Assert
		result!.Status.ShouldBe(ProvisioningRequestStatus.InReview);
	}

	[Fact]
	public async Task ReturnNull_WhenRequestNotFound()
	{
		var result = await _store.GetRequestAsync("nonexistent", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowOnSave_WhenNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_store.SaveRequestAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnGetRequest_WhenIdIsNullOrEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetRequestAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() =>
			_store.GetRequestAsync("", CancellationToken.None));
	}

	#endregion

	#region GetRequestsByStatusAsync

	[Fact]
	public async Task FilterByStatus()
	{
		// Arrange
		await _store.SaveRequestAsync(CreateRequest("r1", ProvisioningRequestStatus.Pending), CancellationToken.None);
		await _store.SaveRequestAsync(CreateRequest("r2", ProvisioningRequestStatus.InReview), CancellationToken.None);
		await _store.SaveRequestAsync(CreateRequest("r3", ProvisioningRequestStatus.InReview), CancellationToken.None);
		await _store.SaveRequestAsync(CreateRequest("r4", ProvisioningRequestStatus.Approved), CancellationToken.None);

		// Act
		var inReview = await _store.GetRequestsByStatusAsync(
			ProvisioningRequestStatus.InReview, CancellationToken.None);

		// Assert
		inReview.Count.ShouldBe(2);
		inReview.ShouldAllBe(r => r.Status == ProvisioningRequestStatus.InReview);
	}

	[Fact]
	public async Task ReturnAllRequests_WhenStatusIsNull()
	{
		await _store.SaveRequestAsync(CreateRequest("r1", ProvisioningRequestStatus.Pending), CancellationToken.None);
		await _store.SaveRequestAsync(CreateRequest("r2", ProvisioningRequestStatus.Approved), CancellationToken.None);

		var all = await _store.GetRequestsByStatusAsync(null, CancellationToken.None);
		all.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnEmptyList_WhenNoMatchingStatus()
	{
		await _store.SaveRequestAsync(CreateRequest("r1", ProvisioningRequestStatus.Pending), CancellationToken.None);

		var denied = await _store.GetRequestsByStatusAsync(
			ProvisioningRequestStatus.Denied, CancellationToken.None);
		denied.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmptyList_WhenStoreIsEmpty()
	{
		var result = await _store.GetRequestsByStatusAsync(null, CancellationToken.None);
		result.ShouldBeEmpty();
	}

	#endregion

	#region DeleteRequestAsync

	[Fact]
	public async Task DeleteExistingRequest()
	{
		await _store.SaveRequestAsync(CreateRequest(), CancellationToken.None);

		var deleted = await _store.DeleteRequestAsync("req-1", CancellationToken.None);
		deleted.ShouldBeTrue();

		var result = await _store.GetRequestAsync("req-1", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnFalse_WhenDeletingNonexistent()
	{
		var deleted = await _store.DeleteRequestAsync("nonexistent", CancellationToken.None);
		deleted.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowOnDelete_WhenIdIsNullOrEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_store.DeleteRequestAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() =>
			_store.DeleteRequestAsync("", CancellationToken.None));
	}

	#endregion

	#region GetService

	[Fact]
	public void ReturnNull_FromGetService()
	{
		_store.GetService(typeof(string)).ShouldBeNull();
		_store.GetService(typeof(IProvisioningStore)).ShouldBeNull();
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
			.Select(i => _store.SaveRequestAsync(
				CreateRequest($"req-{i}"), CancellationToken.None));

		await Task.WhenAll(tasks);

		var all = await _store.GetRequestsByStatusAsync(null, CancellationToken.None);
		all.Count.ShouldBe(50);
	}

	[Fact]
	public async Task HandleConcurrentSaveAndDelete()
	{
		// Pre-populate
		for (int i = 0; i < 20; i++)
		{
			await _store.SaveRequestAsync(CreateRequest($"req-{i}"), CancellationToken.None);
		}

		// Concurrently save new and delete existing
		var saveTasks = Enumerable.Range(20, 20)
			.Select(i => _store.SaveRequestAsync(
				CreateRequest($"req-{i}"), CancellationToken.None));
		var deleteTasks = Enumerable.Range(0, 20)
			.Select(i => _store.DeleteRequestAsync($"req-{i}", CancellationToken.None));

		await Task.WhenAll(saveTasks.Concat(deleteTasks));

		var remaining = await _store.GetRequestsByStatusAsync(null, CancellationToken.None);
		remaining.Count.ShouldBe(20);
	}

	#endregion

	#region Approval Steps Preservation

	[Fact]
	public async Task PreserveApprovalSteps_OnSaveAndRetrieve()
	{
		// Arrange
		var steps = new List<ApprovalStep>
		{
			new("step-1", "Manager", ApprovalOutcome.Approved, "LGTM",
				DateTimeOffset.UtcNow, "manager@example.com"),
			new("step-2", "SecurityReviewer", null, null, null, null)
		};

		var request = new ProvisioningRequestSummary(
			"req-steps", "user-1", "Admin", "Role",
			ProvisioningRequestStatus.InReview,
			"idem-1", 50, "requester",
			DateTimeOffset.UtcNow, steps);

		// Act
		await _store.SaveRequestAsync(request, CancellationToken.None);
		var result = await _store.GetRequestAsync("req-steps", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.ApprovalSteps.Count.ShouldBe(2);
		result.ApprovalSteps[0].StepId.ShouldBe("step-1");
		result.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Approved);
		result.ApprovalSteps[1].Outcome.ShouldBeNull();
	}

	#endregion
}
