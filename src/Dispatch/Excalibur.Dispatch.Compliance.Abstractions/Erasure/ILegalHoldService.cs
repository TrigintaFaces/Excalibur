// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

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
	Task ReleaseHoldAsync(
		Guid holdId,
		string reason,
		string releasedBy,
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
