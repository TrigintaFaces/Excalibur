// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.A3.Governance.SeparationOfDuties;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using GrantRecord = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.A3.Governance.Tests.Provisioning;

/// <summary>
/// Integration tests: provisioning -> SoD -> approval -> grant creation.
/// Uses in-memory stores with real <see cref="ProvisioningCompletionService"/>,
/// <see cref="DefaultSoDEvaluator"/>, and <see cref="InMemoryProvisioningStore"/>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
public sealed class ProvisioningIntegrationShould : UnitTestBase
{
	private readonly InMemoryProvisioningStore _provisioningStore = new();
	private readonly IGrantStore _grantStore = A.Fake<IGrantStore>();
	private readonly ISoDPolicyStore _sodPolicyStore = A.Fake<ISoDPolicyStore>();

	private static readonly ApprovalStep PendingStep = new(
		"step-1", "Manager", null, null, null, null);

	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	/// <summary>
	/// Full happy path: create aggregate -> submit -> approve -> complete provisioning -> grant created.
	/// </summary>
	[Fact]
	public async Task CompleteFullProvisioningWorkflow_HappyPath()
	{
		// Arrange -- no SoD policies, grant doesn't exist
		A.CallTo(() => _sodPolicyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(Array.Empty<SoDPolicy>());
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Array.Empty<GrantRecord>());
		A.CallTo(() => _grantStore.GrantExistsAsync("user-1", A<string>._, "Role", "Admin", A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _grantStore.SaveGrantAsync(A<GrantRecord>._, A<CancellationToken>._))
			.Returns(1);

		var evaluator = new DefaultSoDEvaluator(_sodPolicyStore, _grantStore);

		// Step 1: Create and persist aggregate
		var request = new ProvisioningRequest("req-1", "user-1", "Admin", "Role",
			"idem-1", 10, "requester", [PendingStep]);
		request.SubmitForReview();
		request.ApproveCurrentStep("manager@example.com", "Approved");
		await _provisioningStore.SaveRequestAsync(request.ToSummary(), CancellationToken.None);

		// Step 2: Complete provisioning
		var completionService = new ProvisioningCompletionService(
			_provisioningStore, _grantStore, evaluator,
			NullLogger<ProvisioningCompletionService>.Instance);

		var result = await completionService.CompleteProvisioningAsync("req-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => _grantStore.SaveGrantAsync(
			A<GrantRecord>.That.Matches(g => g.UserId == "user-1" && g.Qualifier == "Admin"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		var savedRequest = await _provisioningStore.GetRequestAsync("req-1", CancellationToken.None);
		savedRequest!.Status.ShouldBe(ProvisioningRequestStatus.Provisioned);
	}

	/// <summary>
	/// SoD blocks: request approved but SoD conflict detected at completion -> request Failed.
	/// </summary>
	[Fact]
	public async Task BlockProvisioning_WhenSoDConflictExists()
	{
		// Arrange -- SoD policy blocks Admin+Finance
		A.CallTo(() => _sodPolicyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[]
			{
				new SoDPolicy("p1", "No Admin+Finance", null, SoDSeverity.Violation,
					SoDPolicyScope.Role, ["Admin", "Finance"], null, "admin")
			});
		// User already has Finance role
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[]
			{
				new GrantRecord("user-1", "User", null, GrantType.Role, "Finance", null, "admin", Now)
			});
		A.CallTo(() => _grantStore.GrantExistsAsync("user-1", A<string>._, "Role", "Admin", A<CancellationToken>._))
			.Returns(false);

		var evaluator = new DefaultSoDEvaluator(_sodPolicyStore, _grantStore);

		// Create approved request
		var request = new ProvisioningRequest("req-2", "user-1", "Admin", "Role",
			"idem-2", 50, "requester", [PendingStep]);
		request.SubmitForReview();
		request.ApproveCurrentStep("manager");
		await _provisioningStore.SaveRequestAsync(request.ToSummary(), CancellationToken.None);

		// Complete provisioning
		var completionService = new ProvisioningCompletionService(
			_provisioningStore, _grantStore, evaluator,
			NullLogger<ProvisioningCompletionService>.Instance);

		var result = await completionService.CompleteProvisioningAsync("req-2", CancellationToken.None);

		// Assert -- blocked by SoD
		result.ShouldBeFalse();
		A.CallTo(() => _grantStore.SaveGrantAsync(A<GrantRecord>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		var savedRequest = await _provisioningStore.GetRequestAsync("req-2", CancellationToken.None);
		savedRequest!.Status.ShouldBe(ProvisioningRequestStatus.Failed);
	}

	/// <summary>
	/// Multi-step: two approval steps must both approve before completion.
	/// </summary>
	[Fact]
	public async Task CompleteMultiStepApproval_ThenProvision()
	{
		// Arrange
		A.CallTo(() => _sodPolicyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(Array.Empty<SoDPolicy>());
		A.CallTo(() => _grantStore.GetAllGrantsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Array.Empty<GrantRecord>());
		A.CallTo(() => _grantStore.GrantExistsAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _grantStore.SaveGrantAsync(A<GrantRecord>._, A<CancellationToken>._))
			.Returns(1);

		var step2 = new ApprovalStep("step-2", "SecurityReviewer", null, null, null, null);
		var request = new ProvisioningRequest("req-3", "user-1", "SensitiveScope", "ActivityGroup",
			"idem-3", 80, "requester", [PendingStep, step2]);
		request.SubmitForReview();
		request.ApproveCurrentStep("manager@example.com");
		request.ApproveCurrentStep("security@example.com");
		request.Status.ShouldBe(ProvisioningRequestStatus.Approved);
		await _provisioningStore.SaveRequestAsync(request.ToSummary(), CancellationToken.None);

		var evaluator = new DefaultSoDEvaluator(_sodPolicyStore, _grantStore);
		var completionService = new ProvisioningCompletionService(
			_provisioningStore, _grantStore, evaluator,
			NullLogger<ProvisioningCompletionService>.Instance);

		// Act
		var result = await completionService.CompleteProvisioningAsync("req-3", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => _grantStore.SaveGrantAsync(
			A<GrantRecord>.That.Matches(g => g.Qualifier == "SensitiveScope"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// JIT access: approved grant with RequestedExpiry -> ExpiresOn set on created grant.
	/// </summary>
	[Fact]
	public async Task PropagateJitExpiry_WhenRequestHasExpiry()
	{
		// Arrange
		A.CallTo(() => _sodPolicyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(Array.Empty<SoDPolicy>());
		A.CallTo(() => _grantStore.GetAllGrantsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Array.Empty<GrantRecord>());
		A.CallTo(() => _grantStore.GrantExistsAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _grantStore.SaveGrantAsync(A<GrantRecord>._, A<CancellationToken>._))
			.Returns(1);

		var jitExpiry = DateTimeOffset.UtcNow.AddHours(4);
		var request = new ProvisioningRequest("req-jit", "user-1", "EmergencyAccess", "Role",
			"idem-jit", 5, "requester", [PendingStep], tenantId: "tenant-A", requestedExpiry: jitExpiry);
		request.SubmitForReview();
		request.ApproveCurrentStep("manager");
		await _provisioningStore.SaveRequestAsync(request.ToSummary(), CancellationToken.None);

		var evaluator = new DefaultSoDEvaluator(_sodPolicyStore, _grantStore);
		var completionService = new ProvisioningCompletionService(
			_provisioningStore, _grantStore, evaluator,
			NullLogger<ProvisioningCompletionService>.Instance);

		// Act
		var result = await completionService.CompleteProvisioningAsync("req-jit", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => _grantStore.SaveGrantAsync(
			A<GrantRecord>.That.Matches(g => g.ExpiresOn == jitExpiry && g.TenantId == "tenant-A"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Denial: request denied -> completion service rejects with false.
	/// </summary>
	[Fact]
	public async Task ReturnFalse_WhenRequestWasDenied()
	{
		var request = new ProvisioningRequest("req-denied", "user-1", "Admin", "Role",
			"idem-denied", 10, "requester", [PendingStep]);
		request.SubmitForReview();
		request.DenyCurrentStep("manager", "Not justified");
		await _provisioningStore.SaveRequestAsync(request.ToSummary(), CancellationToken.None);

		var completionService = new ProvisioningCompletionService(
			_provisioningStore, _grantStore, null,
			NullLogger<ProvisioningCompletionService>.Instance);

		var result = await completionService.CompleteProvisioningAsync("req-denied", CancellationToken.None);
		result.ShouldBeFalse();
	}
}
