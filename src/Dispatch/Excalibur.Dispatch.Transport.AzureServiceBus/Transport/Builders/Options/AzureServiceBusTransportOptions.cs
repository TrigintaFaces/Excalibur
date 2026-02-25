// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Central configuration options for Azure Service Bus transport.
/// </summary>
/// <remarks>
/// <para>
/// This class aggregates all configuration settings for Azure Service Bus transport
/// following the single entry point pattern.
/// </para>
/// </remarks>
public sealed class AzureServiceBusTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	/// <value>The transport name identifier.</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the connection string for Azure Service Bus.
	/// </summary>
	/// <value>The Azure Service Bus connection string.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified namespace for Azure Service Bus.
	/// </summary>
	/// <value>The fully qualified namespace (e.g., "mynamespace.servicebus.windows.net").</value>
	public string? FullyQualifiedNamespace { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use managed identity for authentication.
	/// </summary>
	/// <value><see langword="true"/> to use managed identity; otherwise, <see langword="false"/>.</value>
	public bool UseManagedIdentity { get; set; }

	/// <summary>
	/// Gets or sets the transport type for Service Bus connections.
	/// </summary>
	/// <value>The transport type. Default is <see cref="ServiceBusTransportType.AmqpTcp"/>.</value>
	public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

	/// <summary>
	/// Gets or sets the sender options for message publishing.
	/// </summary>
	/// <value>The sender configuration options.</value>
	public AzureServiceBusSenderOptions Sender { get; set; } = new();

	/// <summary>
	/// Gets or sets the processor options for message consumption.
	/// </summary>
	/// <value>The processor configuration options.</value>
	public AzureServiceBusProcessorOptions Processor { get; set; } = new();

	/// <summary>
	/// Gets or sets the CloudEvents options.
	/// </summary>
	/// <value>The CloudEvents configuration options.</value>
	public AzureServiceBusCloudEventOptions CloudEvents { get; set; } = new();

	/// <summary>
	/// Gets the message-to-queue/topic mappings.
	/// </summary>
	/// <value>Dictionary mapping message types to queue/topic names.</value>
	public Dictionary<Type, string> EntityMappings { get; } = [];

	/// <summary>
	/// Gets or sets the entity name prefix.
	/// </summary>
	/// <value>The prefix to apply to entity names.</value>
	public string? EntityPrefix { get; set; }
}

/// <summary>
/// Configuration options for Azure Service Bus sender (producer).
/// </summary>
public sealed class AzureServiceBusSenderOptions
{
	/// <summary>
	/// Gets or sets the default queue or topic name for sending.
	/// </summary>
	/// <value>The default entity name.</value>
	public string? DefaultEntityName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable batching.
	/// </summary>
	/// <value><see langword="true"/> to enable batching; otherwise, <see langword="false"/>.</value>
	public bool EnableBatching { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum batch size in bytes.
	/// </summary>
	/// <value>The maximum batch size in bytes. Default is 256KB.</value>
	public long MaxBatchSizeBytes { get; set; } = 256 * 1024;

	/// <summary>
	/// Gets or sets the maximum number of messages in a batch.
	/// </summary>
	/// <value>The maximum number of messages per batch. Default is 100.</value>
	public int MaxBatchCount { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch window duration.
	/// </summary>
	/// <value>The time to wait before sending a partial batch. Default is 100ms.</value>
	public TimeSpan BatchWindow { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets the additional configuration dictionary.
	/// </summary>
	/// <value>Dictionary of additional configuration key-value pairs.</value>
	public Dictionary<string, string> AdditionalConfig { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configuration options for Azure Service Bus processor (consumer).
/// </summary>
public sealed class AzureServiceBusProcessorOptions
{
	/// <summary>
	/// Gets or sets the default queue or subscription name for receiving.
	/// </summary>
	/// <value>The default entity name.</value>
	public string? DefaultEntityName { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of concurrent calls to the message handler.
	/// </summary>
	/// <value>The maximum concurrent calls. Default is 10.</value>
	public int MaxConcurrentCalls { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically complete messages.
	/// </summary>
	/// <value><see langword="true"/> to auto-complete; otherwise, <see langword="false"/>. Default is true.</value>
	public bool AutoCompleteMessages { get; set; } = true;

	/// <summary>
	/// Gets or sets the prefetch count for improved performance.
	/// </summary>
	/// <value>The number of messages to prefetch. Default is 50.</value>
	public int PrefetchCount { get; set; } = 50;

	/// <summary>
	/// Gets or sets the maximum lock renewal duration.
	/// </summary>
	/// <value>The maximum duration to renew the message lock.</value>
	public TimeSpan? MaxAutoLockRenewalDuration { get; set; }

	/// <summary>
	/// Gets or sets the receive mode.
	/// </summary>
	/// <value>The receive mode. Default is <see cref="ServiceBusReceiveMode.PeekLock"/>.</value>
	public ServiceBusReceiveMode ReceiveMode { get; set; } = ServiceBusReceiveMode.PeekLock;

	/// <summary>
	/// Gets the additional configuration dictionary.
	/// </summary>
	/// <value>Dictionary of additional configuration key-value pairs.</value>
	public Dictionary<string, string> AdditionalConfig { get; } = new(StringComparer.OrdinalIgnoreCase);
}
