// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Events;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.A3.Governance;

/// <summary>
/// Event-sourced aggregate representing a provisioning request that flows through
/// an approval workflow before a grant is provisioned.
/// </summary>
/// <remarks>
/// <para>
/// State machine: <c>Pending → InReview → Approved|Denied → Provisioned|Failed</c>.
/// Each transition is modeled as a domain event applied via <see cref="ApplyEventInternal"/>.
/// </para>
/// <para>
/// Approval steps are value objects owned by this aggregate. The aggregate orchestrates
/// step transitions via <see cref="ApproveCurrentStep"/> and <see cref="DenyCurrentStep"/>.
/// </para>
/// </remarks>
internal sealed class ProvisioningRequest : AggregateRoot, IAggregateRoot<ProvisioningRequest, string>
{
	private readonly List<ApprovalStep> _approvalSteps = [];
	private int _currentStepIndex;

	private ProvisioningRequest()
	{
	}

	/// <summary>
	/// Creates a new provisioning request.
	/// </summary>
	public ProvisioningRequest(
		string requestId,
		string userId,
		string grantScope,
		string grantType,
		string idempotencyKey,
		int riskScore,
		string requestedBy,
		IReadOnlyList<ApprovalStep> approvalSteps,
		string? tenantId = null,
		DateTimeOffset? requestedExpiry = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(requestId);
		ArgumentException.ThrowIfNullOrEmpty(userId);
		ArgumentException.ThrowIfNullOrEmpty(grantScope);
		ArgumentException.ThrowIfNullOrEmpty(grantType);
		ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
		ArgumentException.ThrowIfNullOrEmpty(requestedBy);
		ArgumentNullException.ThrowIfNull(approvalSteps);

		RaiseEvent(new ProvisioningRequestCreated
		{
			RequestId = requestId,
			UserId = userId,
			GrantScope = grantScope,
			GrantType = grantType,
			IdempotencyKey = idempotencyKey,
			RiskScore = riskScore,
			RequestedBy = requestedBy,
			ApprovalSteps = approvalSteps,
			TenantId = tenantId,
			RequestedExpiry = requestedExpiry,
		});
	}

	/// <summary>Gets the user requesting the grant.</summary>
	public string UserId { get; private set; } = string.Empty;

	/// <summary>Gets the scope of the requested grant.</summary>
	public string GrantScope { get; private set; } = string.Empty;

	/// <summary>Gets the type of the requested grant.</summary>
	public string GrantType { get; private set; } = string.Empty;

	/// <summary>Gets the idempotency key for duplicate prevention.</summary>
	public string IdempotencyKey { get; private set; } = string.Empty;

	/// <summary>Gets the assessed risk score (0-100).</summary>
	public int RiskScore { get; private set; }

	/// <summary>Gets the optional tenant scope.</summary>
	public string? TenantId { get; private set; }

	/// <summary>Gets the optional JIT expiry time for temporary access.</summary>
	public DateTimeOffset? RequestedExpiry { get; private set; }

	/// <summary>Gets the identity that submitted the request.</summary>
	public string RequestedBy { get; private set; } = string.Empty;

	/// <summary>Gets the current lifecycle status.</summary>
	public ProvisioningRequestStatus Status { get; private set; }

	/// <summary>Gets the failure reason, if any.</summary>
	public string? FailureReason { get; private set; }

	/// <summary>Gets the approval steps for this request.</summary>
	public IReadOnlyList<ApprovalStep> ApprovalSteps => _approvalSteps.AsReadOnly();

	/// <summary>Gets the current approval step index (0-based).</summary>
	public int CurrentStepIndex => _currentStepIndex;

	/// <summary>Creates a new instance for event replay.</summary>
	public static ProvisioningRequest Create(string id) => new() { Id = id };

	/// <summary>Rebuilds from a stream of events.</summary>
	public static ProvisioningRequest FromEvents(string id, IEnumerable<IDomainEvent> events)
	{
		var request = new ProvisioningRequest { Id = id };
		request.LoadFromHistory(events);
		return request;
	}

	/// <summary>
	/// Submits the request for review, transitioning from Pending to InReview.
	/// </summary>
	public void SubmitForReview()
	{
		EnsureStatus(ProvisioningRequestStatus.Pending, "submit for review");

		if (_approvalSteps.Count == 0)
		{
			throw new InvalidOperationException(
				$"Cannot submit request '{Id}' for review: no approval steps defined.");
		}

		RaiseEvent(new ProvisioningStepAdvanced
		{
			RequestId = Id,
			CurrentStepIndex = 0,
		});
	}

	/// <summary>
	/// Approves the current approval step.
	/// </summary>
	public void ApproveCurrentStep(string decidedBy, string? justification = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(decidedBy);
		EnsureStatus(ProvisioningRequestStatus.InReview, "approve step");
		EnsureValidStepIndex();

		var step = _approvalSteps[_currentStepIndex];

		RaiseEvent(new ProvisioningStepApproved
		{
			RequestId = Id,
			StepId = step.StepId,
			DecidedBy = decidedBy,
			Justification = justification,
			Outcome = ApprovalOutcome.Approved,
		});

		// Check if there are more steps
		var nextIndex = _currentStepIndex + 1;
		if (nextIndex < _approvalSteps.Count)
		{
			RaiseEvent(new ProvisioningStepAdvanced
			{
				RequestId = Id,
				CurrentStepIndex = nextIndex,
			});
		}
		// If all steps approved, the aggregate transitions to Approved
		// (handled in ApplyStepApproved)
	}

	/// <summary>
	/// Denies the current approval step, which denies the entire request.
	/// </summary>
	public void DenyCurrentStep(string decidedBy, string? justification = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(decidedBy);
		EnsureStatus(ProvisioningRequestStatus.InReview, "deny step");
		EnsureValidStepIndex();

		var step = _approvalSteps[_currentStepIndex];

		RaiseEvent(new ProvisioningStepDenied
		{
			RequestId = Id,
			StepId = step.StepId,
			DecidedBy = decidedBy,
			Justification = justification,
			Outcome = ApprovalOutcome.Denied,
		});
	}

	/// <summary>
	/// Marks the approved request as successfully provisioned.
	/// </summary>
	public void MarkProvisioned()
	{
		EnsureStatus(ProvisioningRequestStatus.Approved, "mark provisioned");

		RaiseEvent(new ProvisioningRequestProvisioned
		{
			RequestId = Id,
		});
	}

	/// <summary>
	/// Marks the approved request as failed during provisioning.
	/// </summary>
	public void MarkFailed(string reason)
	{
		ArgumentException.ThrowIfNullOrEmpty(reason);
		EnsureStatus(ProvisioningRequestStatus.Approved, "mark failed");

		RaiseEvent(new ProvisioningRequestFailed
		{
			RequestId = Id,
			Reason = reason,
		});
	}

	/// <summary>Converts to a read-model summary.</summary>
	internal ProvisioningRequestSummary ToSummary() => new(
		RequestId: Id,
		UserId: UserId,
		GrantScope: GrantScope,
		GrantType: GrantType,
		Status: Status,
		IdempotencyKey: IdempotencyKey,
		RiskScore: RiskScore,
		RequestedBy: RequestedBy,
		CreatedAt: CreatedAt,
		ApprovalSteps: _approvalSteps.AsReadOnly(),
		TenantId: TenantId,
		RequestedExpiry: RequestedExpiry);

	/// <summary>Gets when the request was created.</summary>
	internal DateTimeOffset CreatedAt { get; private set; }

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case ProvisioningRequestCreated e: Apply(e); break;
			case ProvisioningStepAdvanced e: ApplyStepAdvanced(e); break;
			case ProvisioningStepApproved e: ApplyStepApproved(e); break;
			case ProvisioningStepDenied e: ApplyStepDenied(e); break;
			case ProvisioningRequestProvisioned: Status = ProvisioningRequestStatus.Provisioned; break;
			case ProvisioningRequestFailed e: ApplyFailed(e); break;
		}
	}

	private void Apply(ProvisioningRequestCreated e)
	{
		Id = e.RequestId;
		UserId = e.UserId;
		GrantScope = e.GrantScope;
		GrantType = e.GrantType;
		IdempotencyKey = e.IdempotencyKey;
		RiskScore = e.RiskScore;
		RequestedBy = e.RequestedBy;
		TenantId = e.TenantId;
		RequestedExpiry = e.RequestedExpiry;
		CreatedAt = e.OccurredAt;
		Status = ProvisioningRequestStatus.Pending;
		_approvalSteps.Clear();
		_approvalSteps.AddRange(e.ApprovalSteps);
	}

	private void ApplyStepAdvanced(ProvisioningStepAdvanced e)
	{
		_currentStepIndex = e.CurrentStepIndex;
		Status = ProvisioningRequestStatus.InReview;
	}

	private void ApplyStepApproved(ProvisioningStepApproved e)
	{
		var index = _approvalSteps.FindIndex(s =>
			string.Equals(s.StepId, e.StepId, StringComparison.Ordinal));

		if (index >= 0)
		{
			_approvalSteps[index] = _approvalSteps[index] with
			{
				Outcome = e.Outcome,
				DecidedBy = e.DecidedBy,
				Justification = e.Justification,
				DecidedAt = e.OccurredAt,
			};
		}

		// Check if all steps are now approved -> transition to Approved
		if (_approvalSteps.TrueForAll(s => s.Outcome == ApprovalOutcome.Approved))
		{
			Status = ProvisioningRequestStatus.Approved;
		}
	}

	private void ApplyStepDenied(ProvisioningStepDenied e)
	{
		var index = _approvalSteps.FindIndex(s =>
			string.Equals(s.StepId, e.StepId, StringComparison.Ordinal));

		if (index >= 0)
		{
			_approvalSteps[index] = _approvalSteps[index] with
			{
				Outcome = e.Outcome,
				DecidedBy = e.DecidedBy,
				Justification = e.Justification,
				DecidedAt = e.OccurredAt,
			};
		}

		Status = ProvisioningRequestStatus.Denied;
	}

	private void ApplyFailed(ProvisioningRequestFailed e)
	{
		Status = ProvisioningRequestStatus.Failed;
		FailureReason = e.Reason;
	}

	private void EnsureStatus(ProvisioningRequestStatus expected, string action)
	{
		if (Status != expected)
		{
			throw new InvalidOperationException(
				$"Cannot {action} on request '{Id}': current status is {Status}, expected {expected}.");
		}
	}

	private void EnsureValidStepIndex()
	{
		if (_currentStepIndex < 0 || _currentStepIndex >= _approvalSteps.Count)
		{
			throw new InvalidOperationException(
				$"Invalid step index {_currentStepIndex} on request '{Id}': {_approvalSteps.Count} steps available.");
		}
	}
}
