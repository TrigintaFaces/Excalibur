// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

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
	PartiallyCompleted = 7,

	/// <summary>
	/// Executed, and every remaining obligation is a key the key-management provider has irreversibly
	/// scheduled for destruction but not yet destroyed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a correct intermediate state, not a failure. Everything else the erasure required has been
	/// done; what remains is the provider's own deletion window (for example AWS KMS pending deletion, or an
	/// Azure Key Vault soft-delete retention period that cannot be purged early). Until that window ends the
	/// key material is still recoverable, so the request is <b>not</b> <see cref="Completed"/> and no completion
	/// certificate is issued.
	/// </para>
	/// <para>
	/// The request is revisitable: <see cref="IErasureCompletionProcessor"/> asks the provider whether each key
	/// is actually gone and moves the request to <see cref="Completed"/>, with its certificate, only once the
	/// provider confirms it. The provider is the authority on destruction; the scheduled instant it reported
	/// is informational and is never treated as proof. A request in this state cannot be cancelled, because
	/// the key destruction it is waiting on can no longer be withdrawn by the framework.
	/// </para>
	/// </remarks>
	AwaitingKeyDestruction = 8,

	/// <summary>
	/// Everything the erasure was asked to do succeeded, and personal data for the same data subject was
	/// written while it was running — so the erasure did not cover that data.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This state exists because its absence forced the wrong answer.</b> Key destruction runs once, near
	/// the start, and establishes the state of the key handles at that instant and nothing later. A write
	/// that lands afterwards, for the same subject, mints a live key at a handle the erasure had destroyed —
	/// so at the moment the certificate is signed there is personal data of an erased subject encrypted under
	/// a live key. With only <see cref="Completed"/> and <see cref="PartiallyCompleted"/> available, that
	/// outcome had to be reported as one of them: <see cref="Completed"/> attests an erasure that did not
	/// happen, and <see cref="PartiallyCompleted"/> says something failed when nothing did. A reader could
	/// not tell either apart from the genuine article.
	/// </para>
	/// <para>
	/// <b>It is not a failure, and it is not completion.</b> Nothing the erasure attempted went wrong, and
	/// nothing needs retrying for the data it did reach — the data written during the window is simply
	/// outside what was erased. The remedy is another erasure for the same subject, once the writes have
	/// stopped; the certificate this run issues records what was reached and states that the erasure did not
	/// complete, so an auditor reads the true outcome rather than inferring one.
	/// </para>
	/// <para>
	/// <b>Confidentiality is not breached by reaching this state.</b> Data encrypted under the destroyed key
	/// stays unreadable. What survives is data written after that destruction, which was never covered by
	/// it — a coverage gap, stated plainly, rather than an erased value becoming recoverable.
	/// </para>
	/// </remarks>
	CompletedExceptConcurrentWrites = 9
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
	/// No Article 17(3) ground was established: nobody stated one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Zero means "not established" so that a value nobody assigned cannot read as a lawful ground.</b>
	/// <c>required</c> makes omission inexpressible to the C# compiler, but a reflection binder, a
	/// deserializer, a store round trip and an out-of-range cast all reach this enum without the compiler's
	/// involvement — and whatever sits at zero is what they produce. While zero was
	/// <see cref="FreedomOfExpression"/>, an unset configuration value was indistinguishable from a
	/// deliberate Article 17(3)(a) claim, and on a signed erasure certificate it read as the ground under
	/// which a tax record was kept.
	/// </para>
	/// <para>
	/// <b>It is not a lawful basis and must never be attested as one.</b> An exemption carrying it is an
	/// unmet obligation, and presenting an unmet obligation as a ground turns a failure into a defensible
	/// retention. An erasure whose certificate would carry it is recorded as not complete.
	/// </para>
	/// </remarks>
	NotEstablished = 0,

	/// <summary>
	/// Article 17(3)(a) - Freedom of expression and information.
	/// </summary>
	FreedomOfExpression = 1,

	/// <summary>
	/// Article 17(3)(b) - Legal obligation under EU/Member State law.
	/// </summary>
	LegalObligation = 2,

	/// <summary>
	/// Article 17(3)(c) - Public interest (public health).
	/// </summary>
	PublicInterestHealth = 3,

	/// <summary>
	/// Article 17(3)(d) - Archiving in public interest, research, statistics.
	/// </summary>
	ArchivingResearchStatistics = 4,

	/// <summary>
	/// Article 17(3)(e) - Legal claims (defense/exercise).
	/// </summary>
	LegalClaims = 5,

	/// <summary>
	/// Litigation hold (anticipation of legal proceedings).
	/// </summary>
	LitigationHold = 6,

	/// <summary>
	/// Regulatory investigation.
	/// </summary>
	RegulatoryInvestigation = 7
}
