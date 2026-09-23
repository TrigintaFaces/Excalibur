// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Classifies a property as containing sensitive business data: it is encrypted at rest and masked in logs and other rendered output.
/// </summary>
/// <remarks>
/// <para>
/// A property marked with this attribute is selected for encryption at rest on every field-encryption path, and is read by
/// <see cref="IDataMasker" /> to redact the value when an object is rendered for logging. The two protections are independent —
/// masking hides a value in output, encryption protects the bytes that are stored.
/// </para>
/// <para>
/// <strong>This attribute states no key purpose</strong>, so it never contradicts an annotation beside it. The key comes from
/// <see cref="EncryptedFieldAttribute" /> when that states a purpose, from the per-subject key when <see cref="PersonalDataAttribute" />
/// is present, and otherwise from the configured default purpose. Applying it alongside either one is therefore safe and encrypts the
/// value exactly once.
/// </para>
/// <para>
/// Only <see cref="string" /> and <see cref="byte" />[] properties can be encrypted. Marking a property of any other type is
/// <strong>reported</strong> rather than ignored: the value cannot be protected, and silently skipping it would store the data in the
/// clear while the annotation suggested otherwise. A type is checked when an encryption path first uses it, so a rarely-written entity
/// can surface the problem on a write hours after startup rather than at boot.
/// </para>
/// <para>
/// Sensitive data differs from personal data in that it may not be subject to privacy regulations but still warrants classification and
/// redaction:
/// - Trade secrets
/// - API keys and credentials
/// - Internal business metrics
/// - Competitive intelligence
/// </para>
/// <para>
/// Use <see cref="PersonalDataAttribute" /> for data subject to GDPR/privacy regulations. Use this attribute to classify and redact
/// sensitive business data that is not personally identifiable information.
/// </para>
/// </remarks>
/// <example>
/// <code>
///public class ApiConfiguration
///{
///public string ServiceName { get; set; }
///
/// // Encrypted at rest under the configured default key purpose, and redacted in logs.
/// // [EncryptedField] is not needed to encrypt it; add that only to state a specific key purpose.
///[Sensitive(Classification = DataClassification.Restricted)]
///public string ApiKey { get; set; }
///
///[Sensitive(Classification = DataClassification.Confidential)]
///public string ConnectionString { get; set; }
///}
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the classification level for this sensitive data. Defaults to <see cref="DataClassification.Confidential" />.
	/// </summary>
	public DataClassification Classification { get; set; } = DataClassification.Confidential;

	/// <summary>
	/// Gets or sets the category of sensitive data for policy selection.
	/// </summary>
	public SensitiveDataCategory Category { get; set; } = SensitiveDataCategory.General;

	/// <summary>
	/// Gets or sets a value indicating whether this data should be masked in logs. Defaults to true.
	/// </summary>
	/// <remarks>
	/// Read by <see cref="IDataMasker" /> when masking an object. It has no effect on output that does not pass through a masker.
	/// </remarks>
	public bool MaskInLogs { get; set; } = true;
}

/// <summary>
/// Categories of sensitive business data.
/// </summary>
public enum SensitiveDataCategory
{
	/// <summary>
	/// General sensitive data.
	/// </summary>
	General = 0,

	/// <summary>
	/// Credentials and authentication data (API keys, passwords, tokens).
	/// </summary>
	Credentials = 1,

	/// <summary>
	/// Cryptographic material (encryption keys, certificates).
	/// </summary>
	CryptographicMaterial = 2,

	/// <summary>
	/// Infrastructure configuration (connection strings, endpoints).
	/// </summary>
	Configuration = 3,

	/// <summary>
	/// Trade secrets and intellectual property.
	/// </summary>
	TradeSecret = 4,

	/// <summary>
	/// Internal business metrics and analytics.
	/// </summary>
	BusinessMetrics = 5
}
