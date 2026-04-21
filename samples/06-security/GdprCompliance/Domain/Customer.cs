// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Compliance;

namespace GdprCompliance.Domain;

/// <summary>
/// Sample customer entity demonstrating the <see cref="PersonalDataAttribute"/>.
/// The framework source-generators + runtime services use these markers to:
///   * Apply field-level encryption at rest
///   * Mask values in logs
///   * Exclude fields from exception messages
///   * Drive automated erasure when a right-to-erasure request is processed
/// </summary>
public sealed class Customer
{
	/// <summary>Gets or sets the non-PII customer identifier.</summary>
	public Guid Id { get; set; }

	/// <summary>Gets or sets the customer's full name (PII: identity).</summary>
	[PersonalData(
		Category = PersonalDataCategory.Identity,
		Purpose = "Order fulfillment and account identification",
		LegalBasis = LegalBasis.Contract,
		RetentionDays = 2555)] // 7 years for tax records
	public string FullName { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer's email (PII: contact info).</summary>
	[PersonalData(
		Category = PersonalDataCategory.ContactInfo,
		Purpose = "Transactional communication and marketing",
		LegalBasis = LegalBasis.Consent,
		RetentionDays = 1095)] // 3 years
	public string Email { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer's phone number (PII: contact info).</summary>
	[PersonalData(
		Category = PersonalDataCategory.ContactInfo,
		Purpose = "Order confirmation and delivery notifications",
		LegalBasis = LegalBasis.Contract,
		RetentionDays = 1095)]
	public string? PhoneNumber { get; set; }

	/// <summary>Gets or sets the customer's national ID (PII: sensitive identity).</summary>
	[PersonalData(
		Category = PersonalDataCategory.Identity,
		IsSensitive = true,
		Purpose = "KYC / regulatory compliance",
		LegalBasis = LegalBasis.LegalObligation,
		RetentionDays = 2555)]
	public string? NationalIdNumber { get; set; }

	/// <summary>Gets or sets when the customer registered (non-PII).</summary>
	public DateTimeOffset RegisteredAt { get; set; }
}
