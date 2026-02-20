// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Categories of personal data for policy application.
/// </summary>
public enum PersonalDataCategory
{
	/// <summary>
	/// General personal data not falling into specific categories.
	/// </summary>
	General = 0,

	/// <summary>
	/// Identity information (name, ID numbers, passport).
	/// </summary>
	Identity = 1,

	/// <summary>
	/// Contact information (email, phone, address).
	/// </summary>
	ContactInfo = 2,

	/// <summary>
	/// Financial information (bank accounts, credit cards, income).
	/// </summary>
	Financial = 3,

	/// <summary>
	/// Health and medical information.
	/// </summary>
	Health = 4,

	/// <summary>
	/// Biometric data (fingerprints, facial recognition).
	/// </summary>
	Biometric = 5,

	/// <summary>
	/// Location and tracking data.
	/// </summary>
	Location = 6,

	/// <summary>
	/// Behavioral and preference data.
	/// </summary>
	Behavioral = 7
}

/// <summary>
/// Marks a property as containing personal data subject to GDPR and privacy regulations.
/// </summary>
/// <remarks>
/// <para>
/// Personal data requires:
/// - Field-level encryption at rest
/// - Audit logging of all access
/// - Support for right-to-erasure (crypto-shredding)
/// - Data minimization in logs and error messages
/// </para>
/// <para> This attribute can be used by source generators to automatically apply encryption and audit policies. </para>
/// </remarks>
/// <example>
/// <code>
///public class Customer
///{
///public string Id { get; set; }
///
///[PersonalData(Category = PersonalDataCategory.ContactInfo)]
///public string Email { get; set; }
///
///[PersonalData(Category = PersonalDataCategory.Identity)]
///public string SocialSecurityNumber { get; set; }
///}
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the category of personal data for policy selection.
	/// </summary>
	public PersonalDataCategory Category { get; set; } = PersonalDataCategory.General;

	/// <summary>
	/// Gets or sets a value indicating whether this data is considered sensitive personal data under GDPR Article 9 (racial origin, health
	/// data, biometrics, etc.).
	/// </summary>
	public bool IsSensitive { get; set; }

	/// <summary>
	/// Gets or sets the purpose for collecting this personal data. Used for GDPR documentation and data subject requests.
	/// </summary>
	public string? Purpose { get; set; }

	/// <summary>
	/// Gets or sets the legal basis for processing this personal data.
	/// </summary>
	public LegalBasis LegalBasis { get; set; } = LegalBasis.Consent;

	/// <summary>
	/// Gets or sets the retention period in days. After this period, the data should be anonymized or deleted. Zero means indefinite
	/// retention (not recommended).
	/// </summary>
	public int RetentionDays { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this data should be masked in logs. Defaults to true.
	/// </summary>
	public bool MaskInLogs { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether this data should be excluded from error messages and exception details. Defaults to true.
	/// </summary>
	public bool ExcludeFromErrors { get; set; } = true;
}

/// <summary>
/// Legal basis for processing personal data under GDPR.
/// </summary>
public enum LegalBasis
{
	/// <summary>
	/// Data subject has given consent.
	/// </summary>
	Consent = 0,

	/// <summary>
	/// Processing is necessary for contract performance.
	/// </summary>
	Contract = 1,

	/// <summary>
	/// Processing is required by law.
	/// </summary>
	LegalObligation = 2,

	/// <summary>
	/// Processing is necessary to protect vital interests.
	/// </summary>
	VitalInterests = 3,

	/// <summary>
	/// Processing is necessary for a task in the public interest.
	/// </summary>
	PublicInterest = 4,

	/// <summary>
	/// Processing is necessary for legitimate interests.
	/// </summary>
	LegitimateInterests = 5
}
