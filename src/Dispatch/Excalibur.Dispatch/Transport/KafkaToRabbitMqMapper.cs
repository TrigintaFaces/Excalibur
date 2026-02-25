// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Message mapper for converting Kafka messages to RabbitMQ format.
/// </summary>
/// <remarks>
/// <para>
/// This mapper handles the property translation between Kafka and RabbitMQ transports.
/// Properties stored in Kafka headers using the <c>x-</c> prefix convention are restored
/// to their native RabbitMQ equivalents.
/// </para>
/// <para>
/// <strong>Property Mapping:</strong>
/// <list type="table">
/// <listheader><term>Kafka</term><description>RabbitMQ</description></listheader>
/// <item><term>Key</term><description>RoutingKey (direct mapping)</description></item>
/// <item><term>Header x-priority</term><description>Priority (restored if present)</description></item>
/// <item><term>Header x-expiration</term><description>Expiration (restored if present)</description></item>
/// <item><term>Header x-reply-to</term><description>ReplyTo (restored if present)</description></item>
/// <item><term>Topic</term><description>Not mapped (different routing paradigm)</description></item>
/// <item><term>Partition/Offset</term><description>Omitted (Kafka-specific)</description></item>
/// </list>
/// </para>
/// </remarks>
public class KafkaToRabbitMqMapper : DefaultMessageMapper
{
	/// <summary>
	/// Header name for restoring RabbitMQ priority from Kafka messages.
	/// </summary>
	public const string PriorityHeader = "x-priority";

	/// <summary>
	/// Header name for restoring RabbitMQ expiration from Kafka messages.
	/// </summary>
	public const string ExpirationHeader = "x-expiration";

	/// <summary>
	/// Header name for restoring RabbitMQ reply-to from Kafka messages.
	/// </summary>
	public const string ReplyToHeader = "x-reply-to";

	/// <summary>
	/// Default delivery mode for RabbitMQ (persistent).
	/// </summary>
	private const byte PersistentDeliveryMode = 2;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaToRabbitMqMapper"/> class.
	/// </summary>
	public KafkaToRabbitMqMapper()
		: base("KafkaToRabbitMq", "kafka", "rabbitmq")
	{
	}

	/// <inheritdoc/>
	protected override void CopyTransportProperties(ITransportMessageContext source, TransportMessageContext target)
	{
		base.CopyTransportProperties(source, target);

		if (source is KafkaMessageContext kafka && target is RabbitMqMessageContext rmq)
		{
			// Core mapping: Key → RoutingKey
			// Both serve routing purposes - Kafka determines partition, RabbitMQ routes to queues
			rmq.RoutingKey = kafka.Key ?? string.Empty;

			// Restore priority from header if present
			if (kafka.Headers.TryGetValue(PriorityHeader, out var priorityStr) &&
				byte.TryParse(priorityStr, out var priority))
			{
				rmq.Priority = priority;
			}

			// Restore expiration from header if present
			if (kafka.Headers.TryGetValue(ExpirationHeader, out var expiration))
			{
				rmq.Expiration = expiration;
			}

			// Restore reply-to from header if present for request-reply patterns
			if (kafka.Headers.TryGetValue(ReplyToHeader, out var replyTo))
			{
				rmq.ReplyTo = replyTo;
			}

			// Default to persistent delivery mode (Kafka messages are always persistent)
			rmq.DeliveryMode = PersistentDeliveryMode;

			// Note: Topic is intentionally not mapped to Exchange - different routing paradigms
			// Note: Partition/Offset/LeaderEpoch are Kafka-specific and not applicable to RabbitMQ
		}
	}
}
