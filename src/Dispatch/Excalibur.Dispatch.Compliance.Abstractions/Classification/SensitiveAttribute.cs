// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Marks a property as containing sensitive business data requiring encryption.
/// </summary>
/// <remarks>
/// <para>
/// Sensitive data differs from personal data in that it may not be subject to privacy regulations but still requires protection:
/// - Trade secrets
/// - API keys and credentials
/// - Internal business metrics
/// - Competitive intelligence
/// </para>
/// <para>
/// Use <see cref="PersonalDataAttribute" /> for data subject to GDPR/privacy regulations. Use this attribute for sensitive business data
/// that requires encryption but is not personally identifiable information.
/// </para>
/// </remarks>
/// <example>
/// <code>
///public class ApiConfiguration
///{
///public string ServiceName { get; set; }
///
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
	public bool MaskInLogs { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether this data should be excluded from error messages and exception details. Defaults to true.
	/// </summary>
	public bool ExcludeFromErrors { get; set; } = true;

	/// <summary>
	/// Gets or sets the encryption key purpose for key isolation. Null uses the default encryption key.
	/// </summary>
	public string? EncryptionKeyPurpose { get; set; }
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
