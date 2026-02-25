// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options specific to Azure cloud provider.
/// </summary>
public sealed class AzureProviderOptions : ProviderOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AzureProviderOptions" /> class.
	/// </summary>
	public AzureProviderOptions() => Provider = CloudProviderType.Azure;

	/// <summary>
	/// Gets or sets the Azure subscription ID.
	/// </summary>
	/// <value>
	/// The Azure subscription ID.
	/// </value>
	public string SubscriptionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Azure tenant ID for authentication.
	/// </summary>
	/// <value>
	/// The Azure tenant ID for authentication.
	/// </value>
	public string TenantId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Azure client ID for service principal authentication.
	/// </summary>
	/// <value>
	/// The Azure client ID for service principal authentication.
	/// </value>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Azure client secret for service principal authentication.
	/// </summary>
	/// <value>
	/// The Azure client secret for service principal authentication.
	/// </value>
	public string ClientSecret { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether to use managed identity for authentication.
	/// </summary>
	/// <value>
	/// A value indicating whether to use managed identity for authentication.
	/// </value>
	public bool UseManagedIdentity { get; set; }

	/// <summary>
	/// Gets or sets the resource group name.
	/// </summary>
	/// <value>
	/// The resource group name.
	/// </value>
	public string ResourceGroup { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Azure Key Vault URL for secret management.
	/// </summary>
	/// <value>
	/// The Azure Key Vault URL for secret management.
	/// </value>
	public Uri? KeyVaultUrl { get; set; }

	/// <summary>
	/// Gets or sets the Azure Storage account name.
	/// </summary>
	/// <value>
	/// The Azure Storage account name.
	/// </value>
	public string StorageAccountName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Azure Storage account key.
	/// </summary>
	/// <value>
	/// The Azure Storage account key.
	/// </value>
	public string StorageAccountKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the fully qualified namespace for managed identity authentication.
	/// </summary>
	/// <value>
	/// The fully qualified namespace for managed identity authentication.
	/// </value>
	public string? FullyQualifiedNamespace { get; set; }

	/// <summary>
	/// Gets or sets the storage account URI for managed identity authentication.
	/// </summary>
	/// <value>
	/// The storage account URI for managed identity authentication.
	/// </value>
	public Uri? StorageAccountUri { get; set; }

	/// <summary>
	/// Gets or sets the maximum message size in bytes.
	/// </summary>
	/// <value>
	/// The maximum message size in bytes.
	/// </value>
	public int MaxMessageSizeBytes { get; set; } = 256 * 1024; // 256 KB default

	/// <summary>
	/// Gets or sets a value indicating whether to enable session support.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable session support.
	/// </value>
	public bool EnableSessions { get; set; }

	/// <summary>
	/// Gets or sets the prefetch count for receivers.
	/// </summary>
	/// <value>
	/// The prefetch count for receivers.
	/// </value>
	public int PrefetchCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets the retry options.
	/// </summary>
	/// <value>
	/// The retry options.
	/// </value>
	public AzureRetryOptions RetryOptions { get; set; } = new();
}
