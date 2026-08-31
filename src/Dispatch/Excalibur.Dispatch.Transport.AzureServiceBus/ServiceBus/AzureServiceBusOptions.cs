// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for the Azure Service Bus transport. This is the single configuration model:
/// the fluent builder writes into the very instance the options system serves, and every component the
/// transport registers reads that same instance.
/// </summary>
public sealed class AzureServiceBusOptions
{
	/// <summary>
	/// Gets or sets the transport name used as the service key for multi-transport routing.
	/// </summary>
	/// <value>The transport name.</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the Service Bus namespace for connections.
	/// </summary>
	/// <value>The fully-qualified namespace, for example <c>my-bus.servicebus.windows.net</c>.</value>
	public string Namespace { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the connection string used to authenticate.
	/// </summary>
	/// <value>The connection string, or <see langword="null"/> to authenticate with a managed identity.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to authenticate with a managed identity against
	/// <see cref="Namespace"/> rather than a connection string.
	/// </summary>
	/// <value><see langword="true"/> to use a managed identity; otherwise, <see langword="false"/>.</value>
	public bool UseManagedIdentity { get; set; }

	/// <summary>
	/// Gets or sets the transport protocol used for the connection.
	/// </summary>
	/// <value>The transport type. Default is <see cref="ServiceBusTransportType.AmqpTcp"/>.</value>
	public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

	/// <summary>
	/// Gets or sets the sending configuration.
	/// </summary>
	/// <value>The sender options.</value>
	public AzureServiceBusSenderOptions Sender { get; set; } = new();

	/// <summary>
	/// Gets or sets the receiving configuration.
	/// </summary>
	/// <value>The processor options.</value>
	public AzureServiceBusProcessorOptions Processor { get; set; } = new();

	/// <summary>
	/// Gets the message-type to entity-name mappings used to route a published message to a specific
	/// queue or topic.
	/// </summary>
	/// <value>A dictionary mapping message types to entity names.</value>
	public Dictionary<Type, string> EntityMappings { get; } = [];
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
	/// Gets or sets a value indicating whether the receiver consumes from <b>session-enabled</b>
	/// entities with per-session ordered (FIFO) delivery. When <see langword="true"/>,
	/// <c>AddAzureServiceBusTransport</c> wires a session-aware receiver that accepts one session at a
	/// time (<c>ServiceBusClient.AcceptNextSessionAsync</c>) so messages sharing a <c>SessionId</c> are
	/// delivered in order. When <see langword="false"/> (default), the non-session receiver is used
	/// (no ordering guarantee, no behavior change for existing consumers). The target queue/subscription
	/// must itself be session-enabled in Azure Service Bus.
	/// </summary>
	/// <value><see langword="true"/> to consume ordered sessions; otherwise, <see langword="false"/>. Default is false.</value>
	public bool RequiresSession { get; set; }

	/// <summary>
	/// Gets or sets the maximum inbound-payload length, in bytes, enforced at ingress before the message
	/// body is materialized or deserialized. An over-limit message is rejected (dead-lettered) rather than
	/// deserialized, guarding against allocation-based denial-of-service. Defaults to 256 KiB, the Azure
	/// Service Bus standard-tier message ceiling. Set to <see langword="null"/> to opt out (no limit).
	/// </summary>
	/// <value>The maximum inbound-payload length in bytes, or <see langword="null"/> to disable the limit. Default is 262144 (256 KiB).</value>
	[Range(1, int.MaxValue)]
	public int? MaxPayloadBytes { get; set; } = 256 * 1024;

	/// <summary>
	/// Gets the additional configuration dictionary.
	/// </summary>
	/// <value>Dictionary of additional configuration key-value pairs.</value>
	public Dictionary<string, string> AdditionalConfig { get; } = new(StringComparer.OrdinalIgnoreCase);
}
