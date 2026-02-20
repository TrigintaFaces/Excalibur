// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Detailed status information for an erasure request.
/// </summary>
public sealed record ErasureStatus
{
	/// <summary>
	/// Gets the unique request identifier.
	/// </summary>
	public required Guid RequestId { get; init; }

	/// <summary>
	/// Gets the SHA-256 hash of the data subject identifier.
	/// </summary>
	/// <remarks>
	/// The actual identifier is not stored for privacy - only the hash for verification.
	/// </remarks>
	public required string DataSubjectIdHash { get; init; }

	/// <summary>
	/// Gets the type of identifier used.
	/// </summary>
	public required DataSubjectIdType IdType { get; init; }

	/// <summary>
	/// Gets the tenant ID if applicable.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the scope of the erasure request.
	/// </summary>
	public required ErasureScope Scope { get; init; }

	/// <summary>
	/// Gets the legal basis for the erasure.
	/// </summary>
	public required ErasureLegalBasis LegalBasis { get; init; }

	/// <summary>
	/// Gets the current request status.
	/// </summary>
	public required ErasureRequestStatus Status { get; init; }

	/// <summary>
	/// Gets the external case/ticket reference.
	/// </summary>
	public string? ExternalReference { get; init; }

	/// <summary>
	/// Gets who requested the erasure.
	/// </summary>
	public required string RequestedBy { get; init; }

	/// <summary>
	/// Gets when the request was submitted.
	/// </summary>
	public required DateTimeOffset RequestedAt { get; init; }

	/// <summary>
	/// Gets when the erasure is/was scheduled to execute.
	/// </summary>
	public DateTimeOffset? ScheduledExecutionAt { get; init; }

	/// <summary>
	/// Gets when the erasure execution started.
	/// </summary>
	public DateTimeOffset? ExecutedAt { get; init; }

	/// <summary>
	/// Gets when the erasure completed.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets when the request was cancelled (if applicable).
	/// </summary>
	public DateTimeOffset? CancelledAt { get; init; }

	/// <summary>
	/// Gets the cancellation reason (if applicable).
	/// </summary>
	public string? CancellationReason { get; init; }

	/// <summary>
	/// Gets who cancelled the request (if applicable).
	/// </summary>
	public string? CancelledBy { get; init; }

	/// <summary>
	/// Gets the number of keys deleted.
	/// </summary>
	public int? KeysDeleted { get; init; }

	/// <summary>
	/// Gets the number of records affected.
	/// </summary>
	public int? RecordsAffected { get; init; }

	/// <summary>
	/// Gets the certificate ID if erasure is complete.
	/// </summary>
	public Guid? CertificateId { get; init; }

	/// <summary>
	/// Gets information about any blocking legal hold.
	/// </summary>
	public LegalHoldInfo? BlockingHold { get; init; }

	/// <summary>
	/// Gets any error message if the request failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets when the status was last updated.
	/// </summary>
	public required DateTimeOffset UpdatedAt { get; init; }

	/// <summary>
	/// Gets whether the request can be cancelled.
	/// </summary>
	public bool CanCancel => Status is ErasureRequestStatus.Pending or ErasureRequestStatus.Scheduled;

	/// <summary>
	/// Gets whether the erasure has been executed (completed or partially completed).
	/// </summary>
	public bool IsExecuted => Status is ErasureRequestStatus.Completed or ErasureRequestStatus.PartiallyCompleted;

	/// <summary>
	/// Gets the days remaining until GDPR deadline (30 days from request).
	/// </summary>
	public int DaysUntilDeadline =>
		Math.Max(0, (int)(RequestedAt.AddDays(30) - DateTimeOffset.UtcNow).TotalDays);
}
