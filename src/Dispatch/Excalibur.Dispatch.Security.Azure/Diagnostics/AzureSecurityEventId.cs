// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Azure;

/// <summary>
/// Event IDs for Azure security components (70900-70919).
/// </summary>
/// <remarks>
/// These event IDs are in the Cloud Credential Stores range defined in Excalibur.Dispatch.Security.
/// </remarks>
public static class AzureSecurityEventId
{
	/// <summary>Azure Key Vault credential store created.</summary>
	public const int AzureKeyVaultCredentialStoreCreated = 70900;

	/// <summary>Retrieving credential from Azure Key Vault.</summary>
	public const int AzureKeyVaultRetrieving = 70906;

	/// <summary>Secret not found in Azure Key Vault.</summary>
	public const int AzureKeyVaultSecretNotFound = 70907;

	/// <summary>Credential retrieved from Azure Key Vault.</summary>
	public const int AzureKeyVaultRetrieved = 70908;

	/// <summary>Request failed for Azure Key Vault.</summary>
	public const int AzureKeyVaultRequestFailed = 70909;

	/// <summary>Failed to retrieve from Azure Key Vault.</summary>
	public const int AzureKeyVaultRetrieveFailed = 70910;

	/// <summary>Storing credential in Azure Key Vault.</summary>
	public const int AzureKeyVaultStoring = 70911;

	/// <summary>Credential stored in Azure Key Vault.</summary>
	public const int AzureKeyVaultStored = 70912;
}
