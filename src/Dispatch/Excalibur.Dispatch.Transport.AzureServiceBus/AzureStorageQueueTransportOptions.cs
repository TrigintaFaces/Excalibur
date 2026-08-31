// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for Azure Storage Queue transport.
/// </summary>
public sealed class AzureStorageQueueTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the queue name.
	/// </summary>
	public string? QueueName { get; set; }
	/// <summary>
	/// Gets the connection and authentication options for the Storage Queue.
	/// </summary>
	public AzureStorageQueueConnectionOptions Connection { get; } = new();

}

/// <summary>
/// Connection and authentication options for Azure Storage Queue transport.
/// </summary>
public sealed class AzureStorageQueueConnectionOptions
{
	/// <summary>
	/// Gets or sets the Azure Storage connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the storage account URI for managed identity authentication.
	/// </summary>
	public Uri? StorageAccountUri { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use managed identity.
	/// </summary>
	public bool UseManagedIdentity { get; set; }
}
