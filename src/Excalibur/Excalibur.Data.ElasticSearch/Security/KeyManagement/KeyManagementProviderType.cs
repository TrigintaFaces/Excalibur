// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the types of key management providers.
/// </summary>
public enum KeyManagementProviderType
{
	/// <summary>
	/// Local operating system-based key storage.
	/// </summary>
	Local = 0,

	/// <summary>
	/// Azure Key Vault cloud service.
	/// </summary>
	AzureKeyVault = 1,

	/// <summary>
	/// AWS Key Management Service (KMS).
	/// </summary>
	AwsKms = 2,

	/// <summary>
	/// Google Cloud Key Management Service.
	/// </summary>
	GoogleCloudKms = 3,

	/// <summary>
	/// HashiCorp Vault secret management.
	/// </summary>
	HashiCorpVault = 4,

	/// <summary>
	/// Hardware Security Module (HSM) integration.
	/// </summary>
	HardwareSecurityModule = 5,
}
