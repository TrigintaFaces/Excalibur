// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures encryption and data protection features.
/// </summary>
public sealed class EncryptionOptions
{
	/// <summary>
	/// Gets a value indicating whether field-level encryption is enabled.
	/// </summary>
	/// <value> True to enable field-level encryption for sensitive data, false otherwise. </value>
	public bool FieldLevelEncryption { get; init; }

	/// <summary>
	/// Gets a value indicating whether document-level security is enabled.
	/// </summary>
	/// <value> True to enable document-level access controls, false otherwise. </value>
	public bool DocumentLevelSecurity { get; init; }

	/// <summary>
	/// Gets the encryption algorithm for field-level encryption.
	/// </summary>
	/// <value> The encryption algorithm to use. Defaults to AES-256-GCM. </value>
	public string EncryptionAlgorithm { get; init; } = "AES-256-GCM";

	/// <summary>
	/// Gets the key management provider configuration.
	/// </summary>
	/// <value> Settings for external key management system integration. </value>
	public KeyManagementOptions KeyManagement { get; init; } = new();

	/// <summary>
	/// Gets the data classification and encryption rules.
	/// </summary>
	/// <value> Rules defining which data should be encrypted and how. </value>
	public List<DataClassificationRule> ClassificationRules { get; init; } = [];
}
