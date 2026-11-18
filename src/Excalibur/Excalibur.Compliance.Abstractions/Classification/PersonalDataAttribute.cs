// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

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
/// Classifies a property as containing personal data subject to GDPR and privacy regulations.
/// </summary>
/// <remarks>
/// <para>
/// This attribute classifies a property. On its own it changes no behaviour: applying it does not encrypt,
/// mask, audit or erase anything. It is the selector that the features below consult, and each of those
/// features must itself be registered before it reads the annotation.
/// </para>
/// <para>
/// <b>Encryption at rest</b> is applied by the framework's encrypting store decorators -- the event store,
/// the projection store, the inbox store and the outbox store -- and only when all of the following hold:
/// the declaring type also carries <see cref="DataSubjectIdAttribute"/> on the property that identifies the
/// subject, whose value is what the per-subject key is derived from; crypto-shredding is registered, so that
/// key exists; the record is written through one of those four stores rather than by your own persistence
/// code; and the annotated property is a readable <i>and writable</i> <see cref="string"/> or
/// <see cref="byte"/> array. A property of any other type, or with no setter, is not encrypted and no
/// diagnostic is raised -- for such a property this attribute is classification metadata only. If a type
/// carrying <see cref="DataSubjectIdAttribute"/> resolves no encryptable annotated property at all, the
/// framework refuses to persist it rather than store personal data in the clear.
/// </para>
/// <para>
/// <b>Masking</b> in logs and error messages is applied by a registered masker. <b>Retention and
/// right-to-erasure</b> read the annotation to determine which properties of a declared aggregate type are
/// in scope. None of these registers a data location on your behalf: the annotation says a property holds
/// personal data, not where that data is stored.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Encrypted at rest only when crypto-shredding is registered and this type is persisted
/// // through one of the framework's encrypting stores. Both annotated properties are writable
/// // strings, which is what the encryption path can act on; the subject id names the key.
///public class Customer
///{
///[DataSubjectId]
///public Guid Id { get; set; }
///
///[PersonalData(Category = PersonalDataCategory.ContactInfo)]
///public string Email { get; set; }
///
///[PersonalData(Category = PersonalDataCategory.Identity)]
///public string SocialSecurityNumber { get; set; }
///
/// // Classified, but NOT encrypted: an int is not an encryptable property type.
///[PersonalData(Category = PersonalDataCategory.Identity)]
///public int NationalIdNumber { get; set; }
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
