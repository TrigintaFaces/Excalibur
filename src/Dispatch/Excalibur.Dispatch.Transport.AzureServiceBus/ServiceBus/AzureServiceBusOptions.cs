// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for Azure Service Bus message handling and processing.
/// </summary>
public sealed class AzureServiceBusOptions
{
	/// <summary>
	/// Gets or sets the Service Bus namespace for connections.
	/// </summary>
	/// <value>
	/// The Service Bus namespace for connections.
	/// </value>
	[Required]
	public string Namespace { get; set; } = null!;

	/// <summary>
	/// Gets or sets the queue name for message operations.
	/// </summary>
	/// <value>
	/// The queue name for message operations.
	/// </value>
	[Required]
	public string QueueName { get; set; } = null!;

	/// <summary>
	/// Gets or sets the connection string for Azure Service Bus.
	/// </summary>
	/// <value>
	/// The connection string for Azure Service Bus.
	/// </value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the transport type for Service Bus connections.
	/// </summary>
	/// <value>
	/// The transport type for Service Bus connections.
	/// </value>
	public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

	/// <summary>
	/// Gets or sets the maximum number of concurrent message processing calls. Default value is 10.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent message processing calls. Default value is 10.
	/// </value>
	[Range(1, 1000)]
	public int MaxConcurrentCalls { get; set; } = 10;

	/// <summary>
	/// Gets or sets the number of messages to prefetch for improved performance. Default value is 50.
	/// </summary>
	/// <value>
	/// The number of messages to prefetch for improved performance. Default value is 50.
	/// </value>
	[Range(0, 5000)]
	public int PrefetchCount { get; set; } = 50;

	/// <summary>
	/// Gets or sets the CloudEvents mode for message formatting. Default value is Structured.
	/// </summary>
	/// <value>
	/// The CloudEvents mode for message formatting. Default value is Structured.
	/// </value>
	public CloudEventsMode CloudEventsMode { get; set; } = CloudEventsMode.Structured;

	/// <summary>
	/// Gets or sets a value indicating whether enables encryption when sending or receiving messages.
	/// </summary>
	/// <value>
	/// A value indicating whether enables encryption when sending or receiving messages.
	/// </value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether when true, rejected messages are sent to dead letter queue instead of abandoned.
	/// </summary>
	/// <value>
	/// A value indicating whether when true, rejected messages are sent to dead letter queue instead of abandoned.
	/// </value>
	public bool DeadLetterOnRejection { get; set; }
}
