// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options specific to Azure Event Hubs messaging.
/// </summary>
public sealed class AzureEventHubOptions
{
	/// <summary>
	/// Gets or sets the Event Hub connection string.
	/// </summary>
	/// <remarks> Either this or FullyQualifiedNamespace must be set. </remarks>
	/// <value>
	/// The Event Hub connection string.
	/// </value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified namespace for managed identity authentication.
	/// </summary>
	/// <remarks> Format: {namespace}.servicebus.windows.net. </remarks>
	/// <value>
	/// The fully qualified namespace for managed identity authentication.
	/// </value>
	public string? FullyQualifiedNamespace { get; set; }

	/// <summary>
	/// Gets or sets the Event Hub name.
	/// </summary>
	/// <value>
	/// The Event Hub name.
	/// </value>
	public string EventHubName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the consumer group name.
	/// </summary>
	/// <value>
	/// The consumer group name.
	/// </value>
	public string ConsumerGroup { get; set; } = "$Default";

	/// <summary>
	/// Gets or sets the prefetch count for receivers.
	/// </summary>
	/// <value>
	/// The prefetch count for receivers.
	/// </value>
	public int PrefetchCount { get; set; } = 300;

	/// <summary>
	/// Gets or sets the maximum batch size for batch operations.
	/// </summary>
	/// <value>
	/// The maximum batch size for batch operations.
	/// </value>
	public int MaxBatchSize { get; set; } = 100;

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
	/// Gets or sets the starting position for event processing.
	/// </summary>
	/// <value>
	/// The starting position for event processing.
	/// </value>
	public EventHubStartingPosition StartingPosition { get; set; } = EventHubStartingPosition.Latest;

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
		if (string.IsNullOrEmpty(ConnectionString) && string.IsNullOrEmpty(FullyQualifiedNamespace))
		{
			throw new InvalidOperationException(
				"Either ConnectionString or FullyQualifiedNamespace must be configured for Azure Event Hubs");
		}

		if (string.IsNullOrEmpty(EventHubName))
		{
			throw new InvalidOperationException("EventHubName must be configured");
		}

		if (PrefetchCount < 0)
		{
			throw new InvalidOperationException("PrefetchCount cannot be negative");
		}

		if (MaxBatchSize is <= 0 or > 1000)
		{
			throw new InvalidOperationException("MaxBatchSize must be between 1 and 1000");
		}
	}
}
