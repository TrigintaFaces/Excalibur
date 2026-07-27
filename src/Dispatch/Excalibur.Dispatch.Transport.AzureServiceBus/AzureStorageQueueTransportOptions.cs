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
	/// Gets or sets the maximum number of concurrent messages to process. Default is 10.
	/// </summary>
	public int MaxConcurrentMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets the visibility timeout for messages. Default is 5 minutes.
	/// </summary>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the polling interval for checking new messages. Default is 1 second.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of messages to retrieve per poll. Default is 10.
	/// </summary>
	public int MaxMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption.
	/// </summary>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets the connection and authentication options for the Storage Queue.
	/// </summary>
	public AzureStorageQueueConnectionOptions Connection { get; } = new();

	/// <summary>
	/// Gets the dead letter queue options for the Storage Queue.
	/// </summary>
	public AzureStorageQueueDeadLetterOptions DeadLetter { get; } = new();
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

/// <summary>
/// Dead letter queue options for Azure Storage Queue transport.
/// </summary>
public sealed class AzureStorageQueueDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the dead letter queue name.
	/// </summary>
	public string? QueueName { get; set; }

	/// <summary>
	/// Gets or sets the maximum dequeue count before sending to DLQ. Default is 5.
	/// </summary>
	public int MaxDequeueCount { get; set; } = 5;
}
