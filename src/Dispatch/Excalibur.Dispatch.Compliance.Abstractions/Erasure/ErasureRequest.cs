// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents a GDPR Article 17 erasure request.
/// </summary>
/// <remarks>
/// The request captures all information needed to process an erasure:
/// - Data subject identification
/// - Legal basis for erasure
/// - Scope and categories
/// - Audit trail information
/// </remarks>
public sealed record ErasureRequest
{
	/// <summary>
	/// Gets the unique identifier for this request.
	/// </summary>
	public Guid RequestId { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the data subject identifier (user ID, email, etc.).
	/// </summary>
	public required string DataSubjectId { get; init; }

	/// <summary>
	/// Gets the type of identifier used to identify the data subject.
	/// </summary>
	public required DataSubjectIdType IdType { get; init; }

	/// <summary>
	/// Gets the tenant context for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the scope of erasure (User, Tenant, or Selective).
	/// </summary>
	public ErasureScope Scope { get; init; } = ErasureScope.User;

	/// <summary>
	/// Gets the legal basis for the request (Article 17(1) grounds).
	/// </summary>
	public required ErasureLegalBasis LegalBasis { get; init; }

	/// <summary>
	/// Gets the external ticket/case reference for audit trail.
	/// </summary>
	public string? ExternalReference { get; init; }

	/// <summary>
	/// Gets the identity of the operator who initiated the request.
	/// </summary>
	public required string RequestedBy { get; init; }

	/// <summary>
	/// Gets an override for the grace period (null = use default).
	/// </summary>
	public TimeSpan? GracePeriodOverride { get; init; }

	/// <summary>
	/// Gets specific data categories to erase (null = all personal data).
	/// </summary>
	public IReadOnlyList<string>? DataCategories { get; init; }

	/// <summary>
	/// Gets the timestamp when the request was created.
	/// </summary>
	public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets additional metadata for the request.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Types of data subject identifiers.
/// </summary>
public enum DataSubjectIdType
{
	/// <summary>
	/// Internal user ID (GUID or similar).
	/// </summary>
	UserId = 0,

	/// <summary>
	/// Email address.
	/// </summary>
	Email = 1,

	/// <summary>
	/// External system identifier.
	/// </summary>
	ExternalId = 2,

	/// <summary>
	/// National ID or government identifier.
	/// </summary>
	NationalId = 3,

	/// <summary>
	/// SHA-256 hash of the original identifier (used for hash-based lookups during erasure execution).
	/// </summary>
	Hash = 4,

	/// <summary>
	/// Custom identifier type.
	/// </summary>
	Custom = 99
}

/// <summary>
/// Scope of the erasure operation.
/// </summary>
public enum ErasureScope
{
	/// <summary>
	/// Erase all data for a specific user.
	/// </summary>
	User = 0,

	/// <summary>
	/// Erase all data for an entire tenant.
	/// </summary>
	Tenant = 1,

	/// <summary>
	/// Erase specific data categories only.
	/// </summary>
	Selective = 2
}

/// <summary>
/// Legal basis for erasure under GDPR Article 17.
/// </summary>
public enum ErasureLegalBasis
{
	/// <summary>
	/// Article 17(1)(a) - Data no longer necessary for purpose.
	/// </summary>
	DataNoLongerNecessary = 0,

	/// <summary>
	/// Article 17(1)(b) - Consent withdrawal.
	/// </summary>
	ConsentWithdrawal = 1,

	/// <summary>
	/// Article 17(1)(c) - Right to object.
	/// </summary>
	RightToObject = 2,

	/// <summary>
	/// Article 17(1)(d) - Unlawful processing.
	/// </summary>
	UnlawfulProcessing = 3,

	/// <summary>
	/// Article 17(1)(e) - Legal obligation to erase.
	/// </summary>
	LegalObligation = 4,

	/// <summary>
	/// Article 17(1)(f) - Child's data in information society services.
	/// </summary>
	ChildData = 5,

	/// <summary>
	/// Data subject direct request (general right to erasure).
	/// </summary>
	DataSubjectRequest = 6
}
