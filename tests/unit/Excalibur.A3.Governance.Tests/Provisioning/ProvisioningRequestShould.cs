// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;

namespace Excalibur.A3.Governance.Tests.Provisioning;

/// <summary>
/// Unit tests for <see cref="ProvisioningRequest"/> aggregate: state machine transitions
/// (Pending→InReview→Approved/Denied→Provisioned/Failed), domain events, validation,
/// multi-step approval, and event replay via LoadFromHistory.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ProvisioningRequestShould : UnitTestBase
{
	private static readonly ApprovalStep PendingStep1 = new(
		"step-1", "Manager", null, null, null, null);

	private static readonly ApprovalStep PendingStep2 = new(
		"step-2", "SecurityReviewer", null, null, null, null);

	private static ProvisioningRequest CreateRequest(
		IReadOnlyList<ApprovalStep>? steps = null) =>
		new("req-1", "user-1", "Admin", "Role", "idem-1", 25, "requester",
			steps ?? [PendingStep1]);

	#region Constructor / Creation

	[Fact]
	public void CreateWithCorrectInitialState()
	{
		// Act
		var sut = CreateRequest();

		// Assert
		sut.UserId.ShouldBe("user-1");
		sut.GrantScope.ShouldBe("Admin");
		sut.GrantType.ShouldBe("Role");
		sut.IdempotencyKey.ShouldBe("idem-1");
		sut.RiskScore.ShouldBe(25);
		sut.RequestedBy.ShouldBe("requester");
		sut.Status.ShouldBe(ProvisioningRequestStatus.Pending);
		sut.ApprovalSteps.Count.ShouldBe(1);
		sut.HasUncommittedEvents.ShouldBeTrue();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnCreate_WhenRequestIdIsNullOrEmpty(string? requestId)
	{
		Should.Throw<ArgumentException>(() =>
			new ProvisioningRequest(requestId!, "user", "scope", "type", "key", 0, "requester",
				[PendingStep1]));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnCreate_WhenUserIdIsNullOrEmpty(string? userId)
	{
		Should.Throw<ArgumentException>(() =>
			new ProvisioningRequest("req", userId!, "scope", "type", "key", 0, "requester",
				[PendingStep1]));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnCreate_WhenGrantScopeIsNullOrEmpty(string? grantScope)
	{
		Should.Throw<ArgumentException>(() =>
			new ProvisioningRequest("req", "user", grantScope!, "type", "key", 0, "requester",
				[PendingStep1]));
	}

	[Fact]
	public void ThrowOnCreate_WhenApprovalStepsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ProvisioningRequest("req", "user", "scope", "type", "key", 0, "requester",
				null!));
	}

	#endregion

	#region SubmitForReview

	[Fact]
	public void TransitionToInReview_WhenSubmitted()
	{
		// Arrange
		var sut = CreateRequest();

		// Act
		sut.SubmitForReview();

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.InReview);
		sut.CurrentStepIndex.ShouldBe(0);
	}

	[Fact]
	public void ThrowOnSubmit_WhenNoApprovalSteps()
	{
		// Arrange -- create with empty steps list
		var sut = new ProvisioningRequest("req-1", "user-1", "Admin", "Role",
			"idem-1", 0, "requester", []);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => sut.SubmitForReview());
	}

	[Fact]
	public void ThrowOnSubmit_WhenNotInPendingState()
	{
		// Arrange
		var sut = CreateRequest();
		sut.SubmitForReview(); // Now InReview

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => sut.SubmitForReview());
	}

	#endregion

	#region ApproveCurrentStep -- Single Step

	[Fact]
	public void TransitionToApproved_WhenSingleStepApproved()
	{
		// Arrange
		var sut = CreateRequest();
		sut.SubmitForReview();

		// Act
		sut.ApproveCurrentStep("manager@example.com", "LGTM");

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.Approved);
		sut.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Approved);
		sut.ApprovalSteps[0].DecidedBy.ShouldBe("manager@example.com");
		sut.ApprovalSteps[0].Justification.ShouldBe("LGTM");
		sut.ApprovalSteps[0].DecidedAt.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnApprove_WhenDecidedByIsNullOrEmpty(string? decidedBy)
	{
		var sut = CreateRequest();
		sut.SubmitForReview();

		Should.Throw<ArgumentException>(() => sut.ApproveCurrentStep(decidedBy!));
	}

	[Fact]
	public void ThrowOnApprove_WhenNotInReview()
	{
		var sut = CreateRequest();
		// Still Pending, not submitted

		Should.Throw<InvalidOperationException>(() => sut.ApproveCurrentStep("manager"));
	}

	#endregion

	#region ApproveCurrentStep -- Multi-Step

	[Fact]
	public void AdvanceToNextStep_WhenMultiStepApprovalInProgress()
	{
		// Arrange
		var sut = CreateRequest([PendingStep1, PendingStep2]);
		sut.SubmitForReview();

		// Act -- approve first step
		sut.ApproveCurrentStep("manager@example.com");

		// Assert -- should advance to step 2, still InReview
		sut.Status.ShouldBe(ProvisioningRequestStatus.InReview);
		sut.CurrentStepIndex.ShouldBe(1);
		sut.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Approved);
		sut.ApprovalSteps[1].Outcome.ShouldBeNull(); // Not yet decided
	}

	[Fact]
	public void TransitionToApproved_WhenAllStepsApproved()
	{
		// Arrange
		var sut = CreateRequest([PendingStep1, PendingStep2]);
		sut.SubmitForReview();

		// Act
		sut.ApproveCurrentStep("manager@example.com");
		sut.ApproveCurrentStep("security@example.com", "Verified");

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.Approved);
		sut.ApprovalSteps.ShouldAllBe(s => s.Outcome == ApprovalOutcome.Approved);
	}

	#endregion

	#region DenyCurrentStep

	[Fact]
	public void TransitionToDenied_WhenStepDenied()
	{
		// Arrange
		var sut = CreateRequest();
		sut.SubmitForReview();

		// Act
		sut.DenyCurrentStep("manager@example.com", "Risk too high");

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.Denied);
		sut.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Denied);
		sut.ApprovalSteps[0].Justification.ShouldBe("Risk too high");
	}

	[Fact]
	public void DenyEntireRequest_WhenAnyStepDenied()
	{
		// Arrange -- multi-step, deny at step 2
		var sut = CreateRequest([PendingStep1, PendingStep2]);
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager@example.com");

		// Act -- deny step 2
		sut.DenyCurrentStep("security@example.com", "Compliance violation");

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.Denied);
		sut.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Approved);
		sut.ApprovalSteps[1].Outcome.ShouldBe(ApprovalOutcome.Denied);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnDeny_WhenDecidedByIsNullOrEmpty(string? decidedBy)
	{
		var sut = CreateRequest();
		sut.SubmitForReview();

		Should.Throw<ArgumentException>(() => sut.DenyCurrentStep(decidedBy!));
	}

	[Fact]
	public void ThrowOnDeny_WhenNotInReview()
	{
		var sut = CreateRequest();
		Should.Throw<InvalidOperationException>(() => sut.DenyCurrentStep("manager"));
	}

	#endregion

	#region MarkProvisioned

	[Fact]
	public void TransitionToProvisioned_WhenApproved()
	{
		// Arrange
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");

		// Act
		sut.MarkProvisioned();

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.Provisioned);
	}

	[Fact]
	public void ThrowOnMarkProvisioned_WhenNotApproved()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();

		// Still InReview, not Approved
		Should.Throw<InvalidOperationException>(() => sut.MarkProvisioned());
	}

	#endregion

	#region MarkFailed

	[Fact]
	public void TransitionToFailed_WhenApprovedButProvisioningFails()
	{
		// Arrange
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");

		// Act
		sut.MarkFailed("Target system unavailable");

		// Assert
		sut.Status.ShouldBe(ProvisioningRequestStatus.Failed);
		sut.FailureReason.ShouldBe("Target system unavailable");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ThrowOnMarkFailed_WhenReasonIsNullOrEmpty(string? reason)
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");

		Should.Throw<ArgumentException>(() => sut.MarkFailed(reason!));
	}

	[Fact]
	public void ThrowOnMarkFailed_WhenNotApproved()
	{
		var sut = CreateRequest();
		Should.Throw<InvalidOperationException>(() => sut.MarkFailed("reason"));
	}

	#endregion

	#region Full Lifecycle (Happy Path)

	[Fact]
	public void SupportFullHappyPath_Pending_InReview_Approved_Provisioned()
	{
		// Arrange & Act
		var sut = CreateRequest();
		sut.Status.ShouldBe(ProvisioningRequestStatus.Pending);

		sut.SubmitForReview();
		sut.Status.ShouldBe(ProvisioningRequestStatus.InReview);

		sut.ApproveCurrentStep("manager");
		sut.Status.ShouldBe(ProvisioningRequestStatus.Approved);

		sut.MarkProvisioned();
		sut.Status.ShouldBe(ProvisioningRequestStatus.Provisioned);
	}

	[Fact]
	public void SupportDenialPath_Pending_InReview_Denied()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.DenyCurrentStep("manager", "No justification");
		sut.Status.ShouldBe(ProvisioningRequestStatus.Denied);
	}

	[Fact]
	public void SupportFailurePath_Pending_InReview_Approved_Failed()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");
		sut.MarkFailed("System error");
		sut.Status.ShouldBe(ProvisioningRequestStatus.Failed);
		sut.FailureReason.ShouldBe("System error");
	}

	#endregion

	#region Event Replay (FromEvents)

	[Fact]
	public void ReplayFromEvents_FullLifecycle()
	{
		// Arrange -- build a request and capture events
		var original = CreateRequest();
		original.SubmitForReview();
		original.ApproveCurrentStep("manager", "Approved");
		original.MarkProvisioned();

		var events = original.GetUncommittedEvents();

		// Act -- replay from events
		var replayed = ProvisioningRequest.FromEvents("req-1", events);

		// Assert
		replayed.Status.ShouldBe(ProvisioningRequestStatus.Provisioned);
		replayed.UserId.ShouldBe("user-1");
		replayed.GrantScope.ShouldBe("Admin");
		replayed.ApprovalSteps.Count.ShouldBe(1);
		replayed.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Approved);
	}

	[Fact]
	public void ReplayFromEvents_DeniedState()
	{
		var original = CreateRequest([PendingStep1, PendingStep2]);
		original.SubmitForReview();
		original.ApproveCurrentStep("manager");
		original.DenyCurrentStep("security", "Blocked");

		var replayed = ProvisioningRequest.FromEvents("req-1", original.GetUncommittedEvents());

		replayed.Status.ShouldBe(ProvisioningRequestStatus.Denied);
		replayed.ApprovalSteps[0].Outcome.ShouldBe(ApprovalOutcome.Approved);
		replayed.ApprovalSteps[1].Outcome.ShouldBe(ApprovalOutcome.Denied);
	}

	#endregion

	#region ToSummary

	[Fact]
	public void ProduceCorrectSummary()
	{
		// Arrange
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");

		// Act
		var summary = sut.ToSummary();

		// Assert
		summary.RequestId.ShouldBe("req-1");
		summary.UserId.ShouldBe("user-1");
		summary.GrantScope.ShouldBe("Admin");
		summary.GrantType.ShouldBe("Role");
		summary.Status.ShouldBe(ProvisioningRequestStatus.Approved);
		summary.IdempotencyKey.ShouldBe("idem-1");
		summary.RiskScore.ShouldBe(25);
		summary.RequestedBy.ShouldBe("requester");
		summary.ApprovalSteps.Count.ShouldBe(1);
	}

	#endregion

	#region Create factory

	[Fact]
	public void CreateEmptyInstanceViaFactory()
	{
		var sut = ProvisioningRequest.Create("req-factory");
		sut.ShouldNotBeNull();
	}

	#endregion

	#region TenantId + JIT Expiry (Sprint 712)

	[Fact]
	public void PreserveTenantIdAndRequestedExpiry()
	{
		// Arrange
		var expiry = DateTimeOffset.UtcNow.AddHours(4);
		var sut = new ProvisioningRequest("req-jit", "user-1", "Admin", "Role",
			"idem-jit", 10, "requester", [PendingStep1], tenantId: "tenant-A", requestedExpiry: expiry);

		// Assert
		sut.TenantId.ShouldBe("tenant-A");
		sut.RequestedExpiry.ShouldBe(expiry);
	}

	[Fact]
	public void IncludeTenantAndExpiryInSummary()
	{
		var expiry = DateTimeOffset.UtcNow.AddHours(4);
		var sut = new ProvisioningRequest("req-jit", "user-1", "Admin", "Role",
			"idem-jit", 10, "requester", [PendingStep1], tenantId: "tenant-B", requestedExpiry: expiry);

		var summary = sut.ToSummary();
		summary.TenantId.ShouldBe("tenant-B");
		summary.RequestedExpiry.ShouldBe(expiry);
	}

	[Fact]
	public void DefaultTenantAndExpiryToNull()
	{
		var sut = CreateRequest();
		sut.TenantId.ShouldBeNull();
		sut.RequestedExpiry.ShouldBeNull();
	}

	[Fact]
	public void ReplayFromEvents_PreservesTenantAndExpiry()
	{
		var expiry = DateTimeOffset.UtcNow.AddHours(8);
		var original = new ProvisioningRequest("req-jit", "user-1", "Admin", "Role",
			"idem-jit", 10, "requester", [PendingStep1], tenantId: "tenant-C", requestedExpiry: expiry);
		original.SubmitForReview();
		original.ApproveCurrentStep("manager");

		var replayed = ProvisioningRequest.FromEvents("req-jit", original.GetUncommittedEvents());

		replayed.TenantId.ShouldBe("tenant-C");
		replayed.RequestedExpiry.ShouldBe(expiry);
		replayed.Status.ShouldBe(ProvisioningRequestStatus.Approved);
	}

	#endregion

	#region Invalid State Transitions

	[Fact]
	public void ThrowOnApprove_WhenAlreadyApproved()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");
		// Now Approved, not InReview
		Should.Throw<InvalidOperationException>(() => sut.ApproveCurrentStep("another-manager"));
	}

	[Fact]
	public void ThrowOnDeny_WhenAlreadyDenied()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.DenyCurrentStep("manager");
		// Now Denied, not InReview
		Should.Throw<InvalidOperationException>(() => sut.DenyCurrentStep("another-manager"));
	}

	[Fact]
	public void ThrowOnMarkProvisioned_WhenAlreadyProvisioned()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");
		sut.MarkProvisioned();
		// Now Provisioned, not Approved
		Should.Throw<InvalidOperationException>(() => sut.MarkProvisioned());
	}

	[Fact]
	public void ThrowOnMarkFailed_WhenAlreadyFailed()
	{
		var sut = CreateRequest();
		sut.SubmitForReview();
		sut.ApproveCurrentStep("manager");
		sut.MarkFailed("Error");
		// Now Failed, not Approved
		Should.Throw<InvalidOperationException>(() => sut.MarkFailed("Another error"));
	}

	#endregion
}
