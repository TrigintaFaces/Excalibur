// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;

namespace Excalibur.Compliance;

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
	/// Gets the tenant identity this request was filed as — the OWNER of the data it erases.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the exact ambient term the write path encrypted under, and it is what key handles are
	/// derived from. It is NOT a partition and must not be compared against one: a single-tenant
	/// deployment operates as <see cref="TenantDefaults.DefaultTenantId"/>, which is a real tenant
	/// identity rather than a reserved marker. Use <see cref="TenantPartition"/> for anything that joins
	/// against a declaration.
	/// </para>
	/// </remarks>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the tenant partition this request belongs to — the thing a retention declaration names.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the second of two facts, and they are separate because they answer different
	/// questions.</b> <see cref="TenantId"/> answers <em>whose data is this, and therefore which key
	/// decrypts it</em>; this answers <em>which partition's legal obligations apply</em>. They were one
	/// field, and the consequence was not a missed lookup: a single-tenant deployment filed a request as
	/// <see cref="TenantDefaults.DefaultTenantId"/> while declaring its retention against
	/// <see cref="KeyedTenantPartition.Untenanted"/>, the join missed, and the key protecting the legally
	/// retained record was destroyed while the signed certificate reported the record as kept.
	/// </para>
	/// <para>
	/// It is decided when the request is FILED, from whether multi-tenancy is configured — not from the
	/// shape of the tenant string. A host with multi-tenancy active yields a scoped partition whatever its
	/// tenants are named, including one named like the single-tenant default; a host without it yields
	/// <see cref="KeyedTenantPartition.Untenanted"/>. It is persisted so a certificate stays reproducible
	/// if the deployment is reconfigured between filing and execution.
	/// </para>
	/// </remarks>
	public required KeyedTenantPartition TenantPartition { get; init; }

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
	/// Gets the key handles this request has destroyed, across every pass it has made.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A count cannot answer the question this does.</b> <see cref="KeysDeleted"/> says how many keys the
	/// LAST pass destroyed; this says WHICH keys the request has destroyed in total. A retry needs the second,
	/// because a key an earlier pass destroyed now reports as absent — indistinguishable from one that never
	/// existed — and without this record the retry attests less coverage than the request has actually
	/// achieved, so an interrupted erasure could never be reported complete.
	/// </para>
	/// <para>
	/// Empty when the request has destroyed nothing yet, never <see langword="null"/>. Order is not
	/// meaningful and duplicates do not appear.
	/// </para>
	/// </remarks>
	public IReadOnlyCollection<string> DestroyedKeyHandles { get; init; } = [];

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
	/// Gets whether the erasure has been executed (completed, partially completed, or executed and awaiting the
	/// key-management provider's destruction of a scheduled key).
	/// </summary>
	public bool IsExecuted => Status is ErasureRequestStatus.Completed
		or ErasureRequestStatus.PartiallyCompleted
		or ErasureRequestStatus.AwaitingKeyDestruction;

	/// <summary>
	/// Gets the days remaining until the GDPR deadline (30 days from request), as of <paramref name="asOf"/>.
	/// </summary>
	/// <param name="asOf">The point in time to measure the remaining days from.</param>
	/// <returns>The whole number of days remaining, floored at zero once the deadline has passed.</returns>
	/// <remarks>
	/// A method rather than a property: a DTO cannot carry an injected clock, so a property here could only
	/// read ambient wall-clock time, making this value untestable at a boundary and non-deterministic on
	/// every read. Callers pass the current time explicitly -- typically <c>TimeProvider.GetUtcNow()</c>.
	/// </remarks>
	public int DaysUntilDeadline(DateTimeOffset asOf) =>
		Math.Max(0, (int)(RequestedAt.AddDays(30) - asOf).TotalDays);
}
