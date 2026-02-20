// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the supported key management providers.
/// </summary>
public enum KeyManagementProvider
{
	/// <summary>
	/// Local key storage with OS-level protection.
	/// </summary>
	Local = 0,

	/// <summary>
	/// Azure Key Vault integration.
	/// </summary>
	AzureKeyVault = 1,

	/// <summary>
	/// AWS Key Management Service integration.
	/// </summary>
	AwsKms = 2,

	/// <summary>
	/// Google Cloud Key Management integration.
	/// </summary>
	GoogleCloudKms = 3,

	/// <summary>
	/// HashiCorp Vault integration.
	/// </summary>
	HashiCorpVault = 4,

	/// <summary>
	/// Hardware Security Module (HSM) integration.
	/// </summary>
	Hsm = 5,
}
