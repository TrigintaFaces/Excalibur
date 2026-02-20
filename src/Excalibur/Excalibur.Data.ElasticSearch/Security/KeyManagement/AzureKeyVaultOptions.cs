// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configuration options for Azure Key Vault integration.
/// </summary>
public sealed class AzureKeyVaultOptions
{
	/// <summary>
	/// Gets the Azure Key Vault URI.
	/// </summary>
	/// <value> The full URI to the Azure Key Vault instance. </value>
	public string VaultUri { get; init; } = string.Empty;

	/// <summary>
	/// Gets the Azure tenant ID for authentication.
	/// </summary>
	/// <value> The Azure Active Directory tenant identifier. </value>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the Azure client ID for managed identity authentication.
	/// </summary>
	/// <value> The client ID for managed identity or service principal authentication. </value>
	public string? ClientId { get; init; }

	/// <summary>
	/// Gets a value indicating whether to use Hardware Security Module (HSM) backed keys.
	/// </summary>
	/// <value> True to use HSM-backed keys, false for software keys. </value>
	public bool UseHsm { get; init; }

	/// <summary>
	/// Gets a value indicating whether to purge secrets immediately after deletion.
	/// </summary>
	/// <value> True to purge deleted secrets, false to keep them in deleted state. </value>
	public bool PurgeOnDelete { get; init; }

	/// <summary>
	/// Gets the maximum number of concurrent operations.
	/// </summary>
	/// <value> The maximum number of concurrent Azure Key Vault operations. </value>
	public int MaxConcurrentOperations { get; init; } = 10;

	/// <summary>
	/// Gets the automatic key rotation interval.
	/// </summary>
	/// <value> The time interval between automatic key rotations. </value>
	public TimeSpan KeyRotationInterval { get; init; } = TimeSpan.FromDays(90);
}
