// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ integration.
/// </summary>
/// <remarks>
/// <para>
/// This class is used internally by the RabbitMQ transport infrastructure.
/// For configuring RabbitMQ transport, use the fluent builder API via
/// <see cref="Microsoft.Extensions.DependencyInjection.RabbitMQTransportServiceCollectionExtensions.AddRabbitMQTransport(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Action{IRabbitMQTransportBuilder})"/>.
/// </para>
/// </remarks>
public sealed class RabbitMqOptions
{
	/// <summary>
	/// Gets or sets the RabbitMQ connection string.
	/// </summary>
	/// <value>The RabbitMQ connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exchange name.
	/// </summary>
	/// <value>The exchange name.</value>
	public string Exchange { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the routing key for publishing.
	/// </summary>
	/// <value>The routing key for publishing.</value>
	public string RoutingKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the queue name for consuming.
	/// </summary>
	/// <value>The queue name for consuming.</value>
	public string QueueName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether encryption is enabled when sending/receiving messages.
	/// </summary>
	/// <value><see langword="true"/> if encryption is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the prefetch count for message consumption.
	/// </summary>
	/// <value>The prefetch count. Default is 100.</value>
	[Range(1, ushort.MaxValue)]
	public ushort PrefetchCount { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to use global prefetch count.
	/// </summary>
	/// <value><see langword="true"/> for global prefetch; otherwise, <see langword="false"/>.</value>
	public bool PrefetchGlobal { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the queue should be durable.
	/// </summary>
	/// <value><see langword="true"/> if durable; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool QueueDurable { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the queue should be exclusive.
	/// </summary>
	/// <value><see langword="true"/> if exclusive; otherwise, <see langword="false"/>.</value>
	public bool QueueExclusive { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the queue should be auto-deleted when all consumers disconnect.
	/// </summary>
	/// <value><see langword="true"/> for auto-delete; otherwise, <see langword="false"/>.</value>
	public bool QueueAutoDelete { get; set; }

	/// <summary>
	/// Gets additional queue arguments.
	/// </summary>
	/// <value>Additional queue arguments.</value>
	public Dictionary<string, object?> QueueArguments { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to automatically acknowledge messages.
	/// </summary>
	/// <value><see langword="true"/> for auto-ack; otherwise, <see langword="false"/>.</value>
	public bool AutoAck { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of messages to process in a batch.
	/// </summary>
	/// <value>The max batch size. Default is 50.</value>
	[Range(1, 10000)]
	public int MaxBatchSize { get; set; } = 50;

	/// <summary>
	/// Gets or sets the maximum time to wait for a batch to fill in milliseconds.
	/// </summary>
	/// <value>The max batch wait time in milliseconds. Default is 500.</value>
	[Range(1, 60000)]
	public int MaxBatchWaitMs { get; set; } = 500;

	/// <summary>
	/// Gets or sets the consumer tag prefix.
	/// </summary>
	/// <value>The consumer tag. Default is "dispatch-consumer".</value>
	public string ConsumerTag { get; set; } = "dispatch-consumer";

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter exchange for rejected messages.
	/// </summary>
	/// <value><see langword="true"/> to enable DLX; otherwise, <see langword="false"/>.</value>
	public bool EnableDeadLetterExchange { get; set; }

	/// <summary>
	/// Gets or sets the dead letter exchange name.
	/// </summary>
	/// <value>The DLX name.</value>
	public string? DeadLetterExchange { get; set; }

	/// <summary>
	/// Gets or sets the dead letter routing key.
	/// </summary>
	/// <value>The dead letter routing key.</value>
	public string? DeadLetterRoutingKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to requeue messages on rejection.
	/// </summary>
	/// <value><see langword="true"/> to requeue; otherwise, <see langword="false"/>.</value>
	public bool RequeueOnReject { get; set; }

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>The connection timeout. Default is 30 seconds.</value>
	[Range(1, 300)]
	public int ConnectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic connection recovery.
	/// </summary>
	/// <value><see langword="true"/> for automatic recovery; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool AutomaticRecoveryEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the network recovery interval in seconds.
	/// </summary>
	/// <value>The recovery interval. Default is 10 seconds.</value>
	[Range(1, 300)]
	public int NetworkRecoveryIntervalSeconds { get; set; } = 10;
}
