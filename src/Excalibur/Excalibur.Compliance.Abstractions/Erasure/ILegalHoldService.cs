// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Service for managing legal holds that block erasure.
/// Implements GDPR Article 17(3) exceptions.
/// </summary>
/// <remarks>
/// Legal holds prevent erasure when data must be retained for:
/// - Legal claims defense
/// - Regulatory investigation
/// - Litigation holds
/// - Legal obligations under EU/Member State law
/// </remarks>
public interface ILegalHoldService
{
	/// <summary>
	/// Creates a legal hold for a data subject or tenant.
	/// </summary>
	/// <param name="request">The legal hold request details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The created legal hold.</returns>
	Task<LegalHold> CreateHoldAsync(
		LegalHoldRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Releases a legal hold.
	/// </summary>
	/// <param name="holdId">The hold ID to release.</param>
	/// <param name="reason">Reason for release.</param>
	/// <param name="releasedBy">Who is releasing the hold.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// Releasing is a read-modify-write, and it refuses to write over a hold that changed underneath it.
	/// If the hold was modified between this call reading it and writing the release — most importantly,
	/// if its expiry was extended — the release is <b>not</b> applied and
	/// <see cref="LegalHoldConcurrencyException"/> is raised. Re-read the hold and decide again; a hold
	/// that is no longer due for release should not be released.
	/// </remarks>
	/// <exception cref="KeyNotFoundException">No hold with that identifier exists, or it was deleted
	/// while this call was releasing it.</exception>
	/// <exception cref="InvalidOperationException">The hold has already been released.</exception>
	/// <exception cref="LegalHoldConcurrencyException">The hold was modified concurrently. Nothing was
	/// written.</exception>
	Task ReleaseHoldAsync(
		Guid holdId,
		string reason,
		string releasedBy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks for active holds using an ALREADY-HASHED data-subject identifier.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Use this whenever the only identifier you hold is a hash.</b> <see cref="CheckHoldsAsync"/>
	/// hashes its argument unconditionally, so passing it a hash queries for a DOUBLE hash against a store
	/// keyed on a single one. The hash is HMAC-SHA256 and is not idempotent, so the two can never match and
	/// the mismatch is SILENT in the unsafe direction: a subject-specific hold reports as absent and an
	/// irreversible erasure proceeds over it.
	/// </para>
	/// <para>
	/// This is not hypothetical. An erasure retains only the subject HASH once a request is recorded -- the
	/// raw identifier is deliberately not kept, because keeping it would defeat the erasure -- so every
	/// execute-time hold check has a hash and nothing else. Two members exist so that constraint is visible
	/// at the call site instead of being carried in a comment nobody reads.
	/// </para>
	/// <para>
	/// Both members share one query path, so a hold found by one is found by the other.
	/// </para>
	/// </remarks>
	/// <param name="dataSubjectIdHash">The hashed data-subject identifier, exactly as stored.</param>
	/// <param name="tenantId">The tenant, or <see langword="null"/> to consult every tenant-wide hold.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The holds active for that subject, plus any tenant-wide holds.</returns>
	Task<LegalHoldCheckResult> CheckHoldsByHashAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a data subject has any active legal holds.
	/// </summary>
	/// <param name="dataSubjectId">The data subject identifier.</param>
	/// <param name="idType">Type of the identifier.</param>
	/// <param name="tenantId">Optional tenant ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Check result including active holds.</returns>
	Task<LegalHoldCheckResult> CheckHoldsAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a specific legal hold.
	/// </summary>
	/// <param name="holdId">The hold ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The legal hold, or null if not found.</returns>
	Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all active legal holds.
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active legal holds.</returns>
	Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Request to create a legal hold.
/// </summary>
public sealed record LegalHoldRequest
{
	/// <summary>
	/// Gets the data subject identifier (or null for tenant-wide hold).
	/// </summary>
	public string? DataSubjectId { get; init; }

	/// <summary>
	/// Gets the type of identifier.
	/// </summary>
	public DataSubjectIdType? IdType { get; init; }

	/// <summary>
	/// Gets the tenant ID (required for tenant-specific holds).
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the legal basis for the hold (Article 17(3) exception).
	/// </summary>
	public required LegalHoldBasis Basis { get; init; }

	/// <summary>
	/// Gets the external case/matter reference.
	/// </summary>
	public required string CaseReference { get; init; }

	/// <summary>
	/// Gets the description of the legal matter.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets the hold expiration (null = indefinite until released).
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets who created the hold.
	/// </summary>
	public required string CreatedBy { get; init; }
}

/// <summary>
/// Represents an active legal hold.
/// </summary>
public sealed record LegalHold
{
	/// <summary>
	/// Gets the hold identifier.
	/// </summary>
	public required Guid HoldId { get; init; }

	/// <summary>
	/// Gets the SHA-256 hash of the data subject identifier.
	/// </summary>
	public string? DataSubjectIdHash { get; init; }

	/// <summary>
	/// Gets the type of identifier.
	/// </summary>
	public DataSubjectIdType? IdType { get; init; }

	/// <summary>
	/// Gets the tenant ID.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the legal basis for the hold.
	/// </summary>
	public required LegalHoldBasis Basis { get; init; }

	/// <summary>
	/// Gets the external case reference.
	/// </summary>
	public required string CaseReference { get; init; }

	/// <summary>
	/// Gets the description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets whether the hold is currently active.
	/// </summary>
	public required bool IsActive { get; init; }

	/// <summary>
	/// Gets when the hold expires.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets who created the hold.
	/// </summary>
	public required string CreatedBy { get; init; }

	/// <summary>
	/// Gets when the hold was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets who released the hold.
	/// </summary>
	public string? ReleasedBy { get; init; }

	/// <summary>
	/// Gets when the hold was released.
	/// </summary>
	public DateTimeOffset? ReleasedAt { get; init; }

	/// <summary>
	/// Gets the release reason.
	/// </summary>
	public string? ReleaseReason { get; init; }

	/// <summary>
	/// Gets the concurrency token for this hold: the number of times the stored record has been updated.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The store owns this value; a caller only carries it.</b> A newly created hold is stored at
	/// <c>0</c>, <see cref="ILegalHoldStore.GetHoldAsync"/> and the query store return whatever is stored,
	/// and each successful <see cref="ILegalHoldStore.UpdateHoldAsync"/> increments it by one. Setting it by
	/// hand does not move the stored record; it only changes which stored version the next update will
	/// accept.
	/// </para>
	/// <para>
	/// <b>Round-trip it unchanged.</b> Updating a hold is a read-modify-write, and the version is what makes
	/// that sequence safe: the store applies the write only if the record still carries the version the
	/// caller read, and raises <see cref="LegalHoldConcurrencyException"/> if it does not. Building the
	/// record to write with a <c>with</c> expression over the one that was read carries the version across
	/// for free, which is why no call site needs to mention it. Constructing a fresh
	/// <see cref="LegalHold"/> from parts and writing that is the way to lose the protection — the version
	/// defaults to <c>0</c>, which no updated record still carries, so the write is refused rather than
	/// silently applied.
	/// </para>
	/// <para>
	/// It is a plain counter rather than a provider-native row version or ETag so that every store — the
	/// relational providers and the in-memory one — compares the same value and a hold read from one shape
	/// of store means the same thing as a hold read from another.
	/// </para>
	/// </remarks>
	public int Version { get; init; }
}

/// <summary>
/// Result of checking legal holds for a data subject.
/// </summary>
public sealed record LegalHoldCheckResult
{
	/// <summary>
	/// Gets whether any active holds exist.
	/// </summary>
	public bool HasActiveHolds { get; init; }

	/// <summary>
	/// Gets active holds affecting this data subject.
	/// </summary>
	public IReadOnlyList<LegalHoldInfo> ActiveHolds { get; init; } = [];

	/// <summary>
	/// Gets whether erasure is completely blocked (all categories covered by holds).
	/// When <see cref="ExemptCategories"/> is non-empty, only those categories are blocked
	/// and erasure may proceed for non-exempt categories (see <see cref="IsPartiallyBlocked"/>).
	/// </summary>
	public bool ErasureBlocked => HasActiveHolds && ExemptCategories.Count == 0;

	/// <summary>
	/// Gets whether erasure is partially blocked (some data categories are exempt from erasure
	/// due to holds, but other categories may still be erased).
	/// </summary>
	public bool IsPartiallyBlocked => HasActiveHolds && ExemptCategories.Count > 0;

	/// <summary>
	/// Gets data categories exempt from erasure due to holds.
	/// When non-empty, only these categories are protected; other categories may be erased.
	/// When empty and <see cref="HasActiveHolds"/> is true, all data is blocked from erasure.
	/// </summary>
	public IReadOnlyList<string> ExemptCategories { get; init; } = [];

	/// <summary>
	/// Checks whether a specific data category is blocked from erasure.
	/// </summary>
	/// <param name="category">The data category to check.</param>
	/// <returns><see langword="true"/> if the category is blocked; otherwise <see langword="false"/>.</returns>
	public bool IsCategoryBlocked(string category)
	{
		if (!HasActiveHolds)
		{
			return false;
		}

		// No exempt categories means all categories are blocked
		if (ExemptCategories.Count == 0)
		{
			return true;
		}

		return ExemptCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Creates a result indicating no holds.
	/// </summary>
	public static LegalHoldCheckResult NoHolds => new() { HasActiveHolds = false };

	/// <summary>
	/// Creates a result indicating holds exist that block all data categories.
	/// </summary>
	public static LegalHoldCheckResult WithHolds(IEnumerable<LegalHoldInfo> holds) =>
		new()
		{
			HasActiveHolds = true,
			ActiveHolds = holds.ToList()
		};

	/// <summary>
	/// Creates a result indicating holds exist that block specific data categories only.
	/// </summary>
	public static LegalHoldCheckResult WithPartialHolds(
		IEnumerable<LegalHoldInfo> holds,
		IEnumerable<string> exemptCategories) =>
		new()
		{
			HasActiveHolds = true,
			ActiveHolds = holds.ToList(),
			ExemptCategories = exemptCategories.ToList()
		};
}
