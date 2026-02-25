// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Result of an erasure request submission.
/// </summary>
public sealed record ErasureResult
{
	/// <summary>
	/// Gets the tracking ID for this erasure request.
	/// </summary>
	public required Guid RequestId { get; init; }

	/// <summary>
	/// Gets the current status of the request.
	/// </summary>
	public required ErasureRequestStatus Status { get; init; }

	/// <summary>
	/// Gets the scheduled execution time (after grace period).
	/// </summary>
	public DateTimeOffset? ScheduledExecutionTime { get; init; }

	/// <summary>
	/// Gets information about any legal hold blocking the request.
	/// </summary>
	public LegalHoldInfo? BlockingHold { get; init; }

	/// <summary>
	/// Gets the data inventory summary discovered for erasure.
	/// </summary>
	public DataInventorySummary? InventorySummary { get; init; }

	/// <summary>
	/// Gets the estimated completion time.
	/// </summary>
	public DateTimeOffset? EstimatedCompletionTime { get; init; }

	/// <summary>
	/// Gets any message associated with the result (e.g., error details).
	/// </summary>
	public string? Message { get; init; }

	/// <summary>
	/// Creates a successful result with scheduled status.
	/// </summary>
	public static ErasureResult Scheduled(
		Guid requestId,
		DateTimeOffset scheduledTime,
		DataInventorySummary? inventory = null) =>
		new()
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			ScheduledExecutionTime = scheduledTime,
			InventorySummary = inventory,
			EstimatedCompletionTime = scheduledTime.AddMinutes(5)
		};

	/// <summary>
	/// Creates a blocked result due to legal hold.
	/// </summary>
	public static ErasureResult Blocked(
		Guid requestId,
		LegalHoldInfo hold) =>
		new()
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.BlockedByLegalHold,
			BlockingHold = hold,
			Message = $"Erasure blocked by legal hold: {hold.CaseReference}"
		};

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	public static ErasureResult Failed(
		Guid requestId,
		string message) =>
		new()
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Failed,
			Message = message
		};
}

/// <summary>
/// Status of an erasure request.
/// </summary>
public enum ErasureRequestStatus
{
	/// <summary>
	/// Request received, validation in progress.
	/// </summary>
	Pending = 0,

	/// <summary>
	/// In grace period, awaiting execution.
	/// </summary>
	Scheduled = 1,

	/// <summary>
	/// Erasure currently executing.
	/// </summary>
	InProgress = 2,

	/// <summary>
	/// Erasure completed successfully.
	/// </summary>
	Completed = 3,

	/// <summary>
	/// Erasure blocked by legal hold (Article 17(3)).
	/// </summary>
	BlockedByLegalHold = 4,

	/// <summary>
	/// Request cancelled during grace period.
	/// </summary>
	Cancelled = 5,

	/// <summary>
	/// Erasure failed (requires investigation).
	/// </summary>
	Failed = 6,

	/// <summary>
	/// Partially completed (some data retained per exception).
	/// </summary>
	PartiallyCompleted = 7
}

/// <summary>
/// Summary of data discovered for erasure.
/// </summary>
public sealed record DataInventorySummary
{
	/// <summary>
	/// Gets the total number of encrypted fields identified.
	/// </summary>
	public int EncryptedFieldCount { get; init; }

	/// <summary>
	/// Gets the number of distinct encryption keys involved.
	/// </summary>
	public int KeyCount { get; init; }

	/// <summary>
	/// Gets the data categories discovered.
	/// </summary>
	public IReadOnlyList<string> DataCategories { get; init; } = [];

	/// <summary>
	/// Gets the tables/collections containing personal data.
	/// </summary>
	public IReadOnlyList<string> AffectedTables { get; init; } = [];

	/// <summary>
	/// Gets the estimated data volume in bytes.
	/// </summary>
	public long EstimatedDataSizeBytes { get; init; }
}

/// <summary>
/// Summary information about a legal hold.
/// </summary>
public sealed record LegalHoldInfo
{
	/// <summary>
	/// Gets the hold identifier.
	/// </summary>
	public required Guid HoldId { get; init; }

	/// <summary>
	/// Gets the legal basis for the hold (Article 17(3) exception).
	/// </summary>
	public required LegalHoldBasis Basis { get; init; }

	/// <summary>
	/// Gets the external case reference.
	/// </summary>
	public required string CaseReference { get; init; }

	/// <summary>
	/// Gets when the hold was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets when the hold expires (null = indefinite).
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// GDPR Article 17(3) exceptions that justify blocking erasure.
/// </summary>
public enum LegalHoldBasis
{
	/// <summary>
	/// Article 17(3)(a) - Freedom of expression and information.
	/// </summary>
	FreedomOfExpression = 0,

	/// <summary>
	/// Article 17(3)(b) - Legal obligation under EU/Member State law.
	/// </summary>
	LegalObligation = 1,

	/// <summary>
	/// Article 17(3)(c) - Public interest (public health).
	/// </summary>
	PublicInterestHealth = 2,

	/// <summary>
	/// Article 17(3)(d) - Archiving in public interest, research, statistics.
	/// </summary>
	ArchivingResearchStatistics = 3,

	/// <summary>
	/// Article 17(3)(e) - Legal claims (defense/exercise).
	/// </summary>
	LegalClaims = 4,

	/// <summary>
	/// Litigation hold (anticipation of legal proceedings).
	/// </summary>
	LitigationHold = 5,

	/// <summary>
	/// Regulatory investigation.
	/// </summary>
	RegulatoryInvestigation = 6
}
