// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.A3.Governance.Tests.Provisioning;

/// <summary>
/// Unit tests for <see cref="ProvisioningCompletionService"/>: idempotency, SoD check,
/// grant creation, failure paths, and null SoD evaluator.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ProvisioningCompletionServiceShould : UnitTestBase
{
	private readonly IProvisioningStore _provisioningStore = A.Fake<IProvisioningStore>();
	private readonly IGrantStore _grantStore = A.Fake<IGrantStore>();
	private readonly ISoDEvaluator _sodEvaluator = A.Fake<ISoDEvaluator>();
	private readonly ProvisioningCompletionService _sut;

	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	public ProvisioningCompletionServiceShould()
	{
		_sut = new ProvisioningCompletionService(
			_provisioningStore,
			_grantStore,
			_sodEvaluator,
			NullLogger<ProvisioningCompletionService>.Instance);
	}

	private static ProvisioningRequestSummary MakeApprovedSummary(
		string requestId = "req-1",
		string userId = "user-1",
		string grantScope = "Admin",
		string grantType = "Role",
		string? tenantId = null,
		DateTimeOffset? requestedExpiry = null) =>
		new(requestId, userId, grantScope, grantType,
			ProvisioningRequestStatus.Approved, $"idem-{requestId}", 10, "requester", Now,
			[new ApprovalStep("step-1", "Manager", ApprovalOutcome.Approved, "OK", Now, "manager")],
			tenantId, requestedExpiry);

	#region Happy Path

	[Fact]
	public async Task CreateGrant_WhenRequestIsApproved()
	{
		// Arrange
		var summary = MakeApprovedSummary();
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);
		A.CallTo(() => _grantStore.GrantExistsAsync("user-1", A<string>._, "Role", "Admin", A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _sodEvaluator.EvaluateHypotheticalAsync("user-1", "Admin", "Role", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SoDConflict>>([]));

		// Act
		var result = await _sut.CompleteProvisioningAsync("req-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => _grantStore.SaveGrantAsync(A<Grant>.That.Matches(g =>
			g.UserId == "user-1" && g.Qualifier == "Admin" && g.GrantType == "Role"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkRequestAsProvisioned_AfterGrantCreation()
	{
		var summary = MakeApprovedSummary();
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);
		A.CallTo(() => _grantStore.GrantExistsAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _sodEvaluator.EvaluateHypotheticalAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SoDConflict>>([]));

		await _sut.CompleteProvisioningAsync("req-1", CancellationToken.None);

		A.CallTo(() => _provisioningStore.SaveRequestAsync(
			A<ProvisioningRequestSummary>.That.Matches(s => s.Status == ProvisioningRequestStatus.Provisioned),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Idempotency

	[Fact]
	public async Task ReturnTrue_WhenGrantAlreadyExists()
	{
		var summary = MakeApprovedSummary();
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);
		A.CallTo(() => _grantStore.GrantExistsAsync("user-1", A<string>._, "Role", "Admin", A<CancellationToken>._))
			.Returns(true);

		var result = await _sut.CompleteProvisioningAsync("req-1", CancellationToken.None);

		result.ShouldBeTrue();
		// Should NOT call SaveGrantAsync again
		A.CallTo(() => _grantStore.SaveGrantAsync(A<Grant>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Request Not Found / Wrong Status

	[Fact]
	public async Task ReturnFalse_WhenRequestNotFound()
	{
		A.CallTo(() => _provisioningStore.GetRequestAsync("missing", A<CancellationToken>._))
			.Returns(Task.FromResult<ProvisioningRequestSummary?>(null));

		var result = await _sut.CompleteProvisioningAsync("missing", CancellationToken.None);
		result.ShouldBeFalse();
	}

	[Theory]
	[InlineData(ProvisioningRequestStatus.Pending)]
	[InlineData(ProvisioningRequestStatus.InReview)]
	[InlineData(ProvisioningRequestStatus.Denied)]
	[InlineData(ProvisioningRequestStatus.Failed)]
	[InlineData(ProvisioningRequestStatus.Provisioned)]
	public async Task ReturnFalse_WhenRequestNotInApprovedStatus(ProvisioningRequestStatus status)
	{
		var summary = MakeApprovedSummary() with { Status = status };
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);

		var result = await _sut.CompleteProvisioningAsync("req-1", CancellationToken.None);
		result.ShouldBeFalse();
	}

	#endregion

	#region SoD Conflict

	[Fact]
	public async Task ReturnFalse_WhenSoDConflictDetected()
	{
		var summary = MakeApprovedSummary();
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);
		A.CallTo(() => _grantStore.GrantExistsAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _sodEvaluator.EvaluateHypotheticalAsync("user-1", "Admin", "Role", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SoDConflict>>(
				[new SoDConflict("policy-1", "user-1", "Admin", "Finance", DateTimeOffset.UtcNow, SoDSeverity.Violation)]));

		var result = await _sut.CompleteProvisioningAsync("req-1", CancellationToken.None);

		result.ShouldBeFalse();
		A.CallTo(() => _grantStore.SaveGrantAsync(A<Grant>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		// Should mark as Failed
		A.CallTo(() => _provisioningStore.SaveRequestAsync(
			A<ProvisioningRequestSummary>.That.Matches(s => s.Status == ProvisioningRequestStatus.Failed),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Null SoD Evaluator

	[Fact]
	public async Task SkipSoDCheck_WhenEvaluatorIsNull()
	{
		var sut = new ProvisioningCompletionService(
			_provisioningStore, _grantStore, null,
			NullLogger<ProvisioningCompletionService>.Instance);

		var summary = MakeApprovedSummary();
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);
		A.CallTo(() => _grantStore.GrantExistsAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);

		var result = await sut.CompleteProvisioningAsync("req-1", CancellationToken.None);

		result.ShouldBeTrue();
		A.CallTo(() => _grantStore.SaveGrantAsync(A<Grant>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region TenantId + JIT Expiry Propagation

	[Fact]
	public async Task PropagateExpiryToGrant()
	{
		var expiry = DateTimeOffset.UtcNow.AddHours(4);
		var summary = MakeApprovedSummary(tenantId: "tenant-A", requestedExpiry: expiry);
		A.CallTo(() => _provisioningStore.GetRequestAsync("req-1", A<CancellationToken>._))
			.Returns(summary);
		A.CallTo(() => _grantStore.GrantExistsAsync(A<string>._, "tenant-A", A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _sodEvaluator.EvaluateHypotheticalAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SoDConflict>>([]));

		await _sut.CompleteProvisioningAsync("req-1", CancellationToken.None);

		A.CallTo(() => _grantStore.SaveGrantAsync(
			A<Grant>.That.Matches(g => g.TenantId == "tenant-A" && g.ExpiresOn == expiry),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Validation

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task ThrowOnComplete_WhenRequestIdIsNullOrEmpty(string? requestId)
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CompleteProvisioningAsync(requestId!, CancellationToken.None));
	}

	#endregion
}
