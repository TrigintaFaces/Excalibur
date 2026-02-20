// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ-specific CloudEvent configuration options.
/// </summary>
public sealed class RabbitMqCloudEventOptions
{
	/// <summary>
	/// Gets the consumer options for message consumption behavior.
	/// </summary>
	/// <value>The consumer options.</value>
	public RabbitMqConsumerOptions Consumer { get; } = new();

	/// <summary>
	/// Gets the publisher options for message publishing behavior.
	/// </summary>
	/// <value>The publisher options.</value>
	public RabbitMqPublisherOptions Publisher { get; } = new();

	/// <summary>
	/// Gets or sets the default exchange name for publishing CloudEvents.
	/// </summary>
	/// <value>
	/// The default exchange name for publishing CloudEvents.
	/// </value>
	public string DefaultExchange { get; set; } = "cloudevents";

	/// <summary>
	/// Gets or sets the exchange type for CloudEvent publishing.
	/// </summary>
	/// <value>
	/// The exchange type for CloudEvent publishing.
	/// </value>
	public RabbitMqExchangeType ExchangeType { get; set; } = RabbitMqExchangeType.Topic;

	/// <summary>
	/// Gets or sets the routing key strategy for CloudEvents.
	/// </summary>
	/// <value>
	/// The routing key strategy for CloudEvents.
	/// </value>
	public RabbitMqRoutingStrategy RoutingStrategy { get; set; } = RabbitMqRoutingStrategy.EventType;

	/// <summary>
	/// Gets or sets the default queue name for consuming CloudEvents.
	/// </summary>
	/// <value>
	/// The default queue name for consuming CloudEvents.
	/// </value>
	public string? DefaultQueue { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether queues should be durable.
	/// </summary>
	/// <remarks> Durable queues survive broker restarts. Recommended for production environments. </remarks>
	/// <value>
	/// A value indicating whether queues should be durable.
	/// </value>
	public bool DurableQueues { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether exchanges should be durable.
	/// </summary>
	/// <value>
	/// A value indicating whether exchanges should be durable.
	/// </value>
	public bool DurableExchanges { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable publisher confirms.
	/// </summary>
	/// <remarks> Publisher confirms provide delivery guarantees but impact performance. </remarks>
	/// <value>
	/// A value indicating whether to enable publisher confirms.
	/// </value>
	public bool EnablePublisherConfirms { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable mandatory publishing.
	/// </summary>
	/// <remarks> Mandatory publishing returns unroutable messages to the publisher. </remarks>
	/// <value>
	/// A value indicating whether to enable mandatory publishing.
	/// </value>
	public bool MandatoryPublishing { get; set; } = true;

	/// <summary>
	/// Gets or sets the message persistence level.
	/// </summary>
	/// <value>
	/// The message persistence level.
	/// </value>
	public RabbitMqPersistence Persistence { get; set; } = RabbitMqPersistence.Persistent;

	/// <summary>
	/// Gets or sets the default message TTL for CloudEvents.
	/// </summary>
	/// <value>
	/// The default message TTL for CloudEvents.
	/// </value>
	public TimeSpan? MessageTtl { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the maximum message size for RabbitMQ CloudEvents.
	/// </summary>
	/// <remarks> RabbitMQ doesn't have a hard limit, but large messages can impact performance. </remarks>
	/// <value>
	/// The maximum message size for RabbitMQ CloudEvents.
	/// </value>
	public long MaxMessageSizeBytes { get; set; } = 128 * 1024 * 1024; // 128MB

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter exchange for failed CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable dead letter exchange for failed CloudEvents.
	/// </value>
	public bool EnableDeadLetterExchange { get; set; } = true;

	/// <summary>
	/// Gets or sets the dead letter exchange name.
	/// </summary>
	/// <value>
	/// The dead letter exchange name.
	/// </value>
	public string DeadLetterExchange { get; set; } = "cloudevents.dlx";

	/// <summary>
	/// Gets or sets the maximum number of retry attempts before moving to dead letter.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts before moving to dead letter.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	/// <value>
	/// The delay between retry attempts.
	/// </value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to use quorum queues for high availability.
	/// </summary>
	/// <remarks> Quorum queues provide better availability and data safety but require RabbitMQ 3.8+. </remarks>
	/// <value>
	/// A value indicating whether to use quorum queues for high availability.
	/// </value>
	public bool UseQuorumQueues { get; set; }

	/// <summary>
	/// Gets or sets the prefetch count for consumers.
	/// </summary>
	/// <remarks>
	/// Controls how many unacknowledged messages a consumer can have. Higher values improve throughput but use more memory.
	/// </remarks>
	/// <value>
	/// The prefetch count for consumers.
	/// </value>
	public ushort PrefetchCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable consumer acknowledgments.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable consumer acknowledgments.
	/// </value>
	public bool EnableConsumerAcks { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic recovery from connection failures.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable automatic recovery from connection failures.
	/// </value>
	public bool AutomaticRecoveryEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the connection recovery interval.
	/// </summary>
	/// <value>
	/// The connection recovery interval.
	/// </value>
	public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);
}
