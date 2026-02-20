// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Event IDs for Azure Key Vault key management (92610-92629).
/// </summary>
public static class AzureKeyVaultEventId
{
	/// <summary>Azure Key Vault provider initialized.</summary>
	public const int ProviderInitialized = 92610;

	/// <summary>Key not found in Azure Key Vault.</summary>
	public const int KeyNotFound = 92611;

	/// <summary>Key version not found in Azure Key Vault.</summary>
	public const int KeyVersionNotFound = 92612;

	/// <summary>Azure Key Vault key rotated.</summary>
	public const int KeyRotated = 92613;

	/// <summary>Azure Key Vault key created.</summary>
	public const int KeyCreated = 92614;

	/// <summary>Azure Key Vault key rotation failed.</summary>
	public const int KeyRotationFailed = 92615;

	/// <summary>Azure Key Vault key rotation unexpected error.</summary>
	public const int KeyRotationUnexpectedError = 92616;

	/// <summary>Azure Key Vault key scheduled for deletion.</summary>
	public const int KeyScheduledForDeletion = 92617;

	/// <summary>Azure Key Vault key not found for deletion.</summary>
	public const int KeyNotFoundForDeletion = 92618;

	/// <summary>Azure Key Vault key suspended.</summary>
	public const int KeySuspended = 92619;

	/// <summary>Azure Key Vault key not found for suspension.</summary>
	public const int KeyNotFoundForSuspension = 92620;

	/// <summary>Azure Key Vault provider disposed.</summary>
	public const int ProviderDisposed = 92621;

	/// <summary>Azure Key Vault key is using standard tier.</summary>
	public const int StandardTierWarning = 92622;
}
