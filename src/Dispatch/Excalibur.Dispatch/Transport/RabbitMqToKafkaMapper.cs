// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Message mapper for converting RabbitMQ messages to Kafka format.
/// </summary>
/// <remarks>
/// <para>
/// This mapper handles the property translation between RabbitMQ and Kafka transports.
/// RabbitMQ properties that have no direct Kafka equivalent are preserved in headers
/// using the <c>x-</c> prefix convention.
/// </para>
/// <para>
/// <strong>Property Mapping:</strong>
/// <list type="table">
/// <listheader><term>RabbitMQ</term><description>Kafka</description></listheader>
/// <item><term>RoutingKey</term><description>Key (direct mapping)</description></item>
/// <item><term>Priority</term><description>Header x-priority (Kafka lacks native priority)</description></item>
/// <item><term>Expiration</term><description>Header x-expiration</description></item>
/// <item><term>ReplyTo</term><description>Header x-reply-to</description></item>
/// <item><term>DeliveryMode</term><description>Omitted (Kafka is always persistent)</description></item>
/// </list>
/// </para>
/// </remarks>
public class RabbitMqToKafkaMapper : DefaultMessageMapper
{
	/// <summary>
	/// Header name for preserving RabbitMQ priority in Kafka messages.
	/// </summary>
	public const string PriorityHeader = "x-priority";

	/// <summary>
	/// Header name for preserving RabbitMQ expiration in Kafka messages.
	/// </summary>
	public const string ExpirationHeader = "x-expiration";

	/// <summary>
	/// Header name for preserving RabbitMQ reply-to in Kafka messages.
	/// </summary>
	public const string ReplyToHeader = "x-reply-to";

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqToKafkaMapper"/> class.
	/// </summary>
	public RabbitMqToKafkaMapper()
		: base("RabbitMqToKafka", "rabbitmq", "kafka")
	{
	}

	/// <inheritdoc/>
	protected override void CopyTransportProperties(ITransportMessageContext source, TransportMessageContext target)
	{
		base.CopyTransportProperties(source, target);

		if (source is RabbitMqMessageContext rmq && target is KafkaMessageContext kafka)
		{
			// Core mapping: RoutingKey → Key
			// Both serve routing purposes - RabbitMQ routes to queues, Kafka determines partition
			kafka.Key = rmq.RoutingKey ?? string.Empty;

			// Priority → Header (Kafka lacks native priority support)
			if (rmq.Priority.HasValue)
			{
				kafka.SetHeader(PriorityHeader, rmq.Priority.Value.ToString());
			}

			// Preserve expiration via header
			if (!string.IsNullOrEmpty(rmq.Expiration))
			{
				kafka.SetHeader(ExpirationHeader, rmq.Expiration);
			}

			// Preserve reply-to via header for request-reply patterns
			if (!string.IsNullOrEmpty(rmq.ReplyTo))
			{
				kafka.SetHeader(ReplyToHeader, rmq.ReplyTo);
			}

			// Note: DeliveryMode is intentionally not mapped - Kafka is always persistent
			// Note: Exchange is intentionally not mapped - Kafka uses topics differently
			// Note: DeliveryTag/Redelivered are transport-specific acknowledgment properties
		}
	}
}
