// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options specific to Azure Storage Queue messaging.
/// </summary>
public sealed class AzureStorageQueueOptions
{
	/// <summary>
	/// Gets or sets the Storage Queue connection string.
	/// </summary>
	/// <remarks> Either this or StorageAccountUri must be set. </remarks>
	/// <value>
	/// The Storage Queue connection string.
	/// </value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the storage account URI for managed identity authentication.
	/// </summary>
	/// <remarks> Format: https://{accountname}.queue.core.windows.net/. </remarks>
	/// <value>
	/// The storage account URI for managed identity authentication.
	/// </value>
	public Uri? StorageAccountUri { get; set; }

	/// <summary>
	/// Gets or sets the default queue name.
	/// </summary>
	/// <value>
	/// The default queue name.
	/// </value>
	public string QueueName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages to process.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent messages to process.
	/// </value>
	public int MaxConcurrentMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets the visibility timeout for messages.
	/// </summary>
	/// <value>
	/// The visibility timeout for messages.
	/// </value>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the polling interval for checking new messages.
	/// </summary>
	/// <value>
	/// The polling interval for checking new messages.
	/// </value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of messages to retrieve per poll.
	/// </summary>
	/// <value>
	/// The maximum number of messages to retrieve per poll.
	/// </value>
	public int MaxMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption for messages.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable encryption for messages.
	/// </value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the encryption provider name.
	/// </summary>
	/// <value>
	/// The encryption provider name.
	/// </value>
	public string? EncryptionProviderName { get; set; }

	/// <summary>
	/// Gets or sets the dead letter queue name.
	/// </summary>
	/// <value>
	/// The dead letter queue name.
	/// </value>
	public string? DeadLetterQueueName { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of times a message can be dequeued before being sent to the dead letter queue.
	/// </summary>
	/// <value>
	/// The maximum number of times a message can be dequeued before being sent to the dead letter queue.
	/// </value>
	public int MaxDequeueCount { get; set; } = 5;

	/// <summary>
	/// Gets the polling interval in milliseconds.
	/// </summary>
	/// <value>
	/// The polling interval in milliseconds.
	/// </value>
	public int PollingIntervalMs => (int)PollingInterval.TotalMilliseconds;

	/// <summary>
	/// Gets or sets the empty queue delay in milliseconds.
	/// </summary>
	/// <value>
	/// The empty queue delay in milliseconds.
	/// </value>
	public int EmptyQueueDelayMs { get; set; } = 1000;

	/// <summary>
	/// Gets the maximum messages per request.
	/// </summary>
	/// <value>
	/// The maximum messages per request.
	/// </value>
	public int MaxMessagesPerRequest => MaxMessages;

	/// <summary>
	/// Gets the visibility timeout in seconds.
	/// </summary>
	/// <value>
	/// The visibility timeout in seconds.
	/// </value>
	public int VisibilityTimeoutSeconds => (int)VisibilityTimeout.TotalSeconds;

	/// <summary>
	/// Gets or sets a value indicating whether to enable verbose logging.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable verbose logging.
	/// </value>
	public bool EnableVerboseLogging { get; set; }

	/// <summary>
	/// Gets custom key-value pairs for additional configuration.
	/// </summary>
	/// <value>
	/// Custom key-value pairs for additional configuration.
	/// </value>
	public Dictionary<string, string> CustomProperties { get; } = [];

	/// <summary>
	/// Validates the options configuration.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when configuration is invalid. </exception>
	public void Validate()
	{
		if (string.IsNullOrEmpty(ConnectionString) && StorageAccountUri == null)
		{
			throw new InvalidOperationException(
				"Either ConnectionString or StorageAccountUri must be configured for Azure Storage Queues");
		}

		if (string.IsNullOrEmpty(QueueName))
		{
			throw new InvalidOperationException("QueueName must be configured");
		}

		if (MaxConcurrentMessages <= 0)
		{
			throw new InvalidOperationException("MaxConcurrentMessages must be greater than 0");
		}

		if (VisibilityTimeout <= TimeSpan.Zero || VisibilityTimeout > TimeSpan.FromDays(7))
		{
			throw new InvalidOperationException("VisibilityTimeout must be between 1 second and 7 days");
		}

		if (PollingInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("PollingInterval must be greater than 0");
		}

		if (MaxMessages is <= 0 or > 32)
		{
			throw new InvalidOperationException("MaxMessages must be between 1 and 32 (Azure Storage Queue limit)");
		}

		if (MaxDequeueCount <= 0)
		{
			throw new InvalidOperationException("MaxDequeueCount must be greater than 0");
		}
	}
}
