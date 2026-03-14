// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Fluent builder for configuring message mapping between transports.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>Microsoft.AspNetCore.Builder.IEndpointConventionBuilder</c> pattern:
/// a single <see cref="Add"/> method with all fluent configuration via extension methods.
/// </para>
/// <example>
/// <code>
/// builder.WithMessageMapping(mapping => mapping
///     .MapMessage&lt;OrderCreatedEvent&gt;()
///         .ToRabbitMq(ctx => ctx.RoutingKey = "orders.created")
///         .ToKafka(ctx => ctx.Topic = "orders")
///     .MapMessage&lt;PaymentProcessedEvent&gt;()
///         .ToRabbitMq(ctx => ctx.Exchange = "payments")
///         .ToAzureServiceBus(ctx => ctx.TopicOrQueueName = "payments"));
/// </code>
/// </example>
/// </remarks>
public interface IMessageMappingBuilder
{
	/// <summary>
	/// Adds a convention to the message mapping builder.
	/// </summary>
	/// <param name="convention">The convention to add.</param>
	void Add(Action<IMessageMappingConventions> convention);
}

/// <summary>
/// Provides the conventions context for configuring message mappings.
/// </summary>
/// <remarks>
/// This type is the target of <see cref="IMessageMappingBuilder.Add"/> callbacks.
/// Extension methods on <see cref="IMessageMappingBuilder"/> delegate to this interface
/// to perform their work.
/// </remarks>
public interface IMessageMappingConventions
{
	/// <summary>
	/// Begins configuration for mapping a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to configure mapping for.</typeparam>
	/// <returns>A builder for configuring transport-specific mappings.</returns>
	IMessageTypeMappingBuilder<TMessage> MapMessage<TMessage>()
		where TMessage : class;

	/// <summary>
	/// Registers a custom message mapper.
	/// </summary>
	/// <param name="mapper">The mapper to register.</param>
	void RegisterMapper(IMessageMapper mapper);

	/// <summary>
	/// Registers a custom message mapper with a factory.
	/// </summary>
	/// <typeparam name="TMapper">The type of mapper to register.</typeparam>
	void RegisterMapper<TMapper>()
		where TMapper : class, IMessageMapper;

	/// <summary>
	/// Registers the default set of mappers for common transport combinations.
	/// </summary>
	void UseDefaultMappers();

	/// <summary>
	/// Configures a global default mapping that applies to all message types
	/// when no specific mapping is defined.
	/// </summary>
	/// <param name="configure">Action to configure the default mapping.</param>
	void ConfigureDefaults(Action<IDefaultMappingBuilder> configure);
}

/// <summary>
/// Builder for configuring transport-specific mappings for a message type.
/// </summary>
/// <typeparam name="TMessage">The message type being configured.</typeparam>
/// <remarks>
/// The core interface provides <see cref="ToTransport"/> for custom/generic transport
/// configuration. Transport-specific methods (ToRabbitMq, ToKafka, etc.) are provided
/// as extension methods.
/// </remarks>
public interface IMessageTypeMappingBuilder<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Configures a custom transport mapping for this message type.
	/// </summary>
	/// <param name="transportName">The name of the custom transport.</param>
	/// <param name="configure">Action to configure the transport context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	IMessageTypeMappingBuilder<TMessage> ToTransport(string transportName, Action<ITransportMessageContext> configure);

	/// <summary>
	/// Returns to the parent builder to configure another message type.
	/// </summary>
	/// <returns>The parent message mapping builder.</returns>
	IMessageMappingBuilder And();
}

/// <summary>
/// Builder for configuring default mapping behavior.
/// </summary>
/// <remarks>
/// The core interface provides <see cref="ForTransport"/> for custom/generic transport
/// default configuration. Transport-specific methods (ForRabbitMq, ForKafka, etc.)
/// are provided as extension methods.
/// </remarks>
public interface IDefaultMappingBuilder
{
	/// <summary>
	/// Configures the default mapping for a specific transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="configure">Action to configure the transport context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	IDefaultMappingBuilder ForTransport(string transportName, Action<ITransportMessageContext> configure);
}

/// <summary>
/// RabbitMQ-specific mapping context for configuring message properties.
/// </summary>
public interface IRabbitMqMappingContext
{
	/// <summary>
	/// Gets or sets the exchange name.
	/// </summary>
	string? Exchange { get; set; }

	/// <summary>
	/// Gets or sets the routing key.
	/// </summary>
	string? RoutingKey { get; set; }

	/// <summary>
	/// Gets or sets the message priority (0-255).
	/// </summary>
	byte? Priority { get; set; }

	/// <summary>
	/// Gets or sets the reply-to queue name.
	/// </summary>
	string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the message expiration in milliseconds.
	/// </summary>
	string? Expiration { get; set; }

	/// <summary>
	/// Gets or sets the delivery mode (1 = non-persistent, 2 = persistent).
	/// </summary>
	byte? DeliveryMode { get; set; }

	/// <summary>
	/// Sets a custom header on the message.
	/// </summary>
	/// <param name="key">The header key.</param>
	/// <param name="value">The header value.</param>
	void SetHeader(string key, string value);
}

/// <summary>
/// Kafka-specific mapping context for configuring message properties.
/// </summary>
public interface IKafkaMappingContext
{
	/// <summary>
	/// Gets or sets the topic name.
	/// </summary>
	string? Topic { get; set; }

	/// <summary>
	/// Gets or sets the message key (used for partitioning).
	/// </summary>
	string? Key { get; set; }

	/// <summary>
	/// Gets or sets the target partition (or null for automatic partitioning).
	/// </summary>
	int? Partition { get; set; }

	/// <summary>
	/// Gets or sets the schema ID for schema registry integration.
	/// </summary>
	int? SchemaId { get; set; }

	/// <summary>
	/// Sets a custom header on the message.
	/// </summary>
	/// <param name="key">The header key.</param>
	/// <param name="value">The header value.</param>
	void SetHeader(string key, string value);
}

/// <summary>
/// Azure Service Bus-specific mapping context for configuring message properties.
/// </summary>
public interface IAzureServiceBusMappingContext
{
	/// <summary>
	/// Gets or sets the topic or queue name.
	/// </summary>
	string? TopicOrQueueName { get; set; }

	/// <summary>
	/// Gets or sets the session ID for session-enabled entities.
	/// </summary>
	string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the partition key.
	/// </summary>
	string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the reply-to session ID.
	/// </summary>
	string? ReplyToSessionId { get; set; }

	/// <summary>
	/// Gets or sets the time to live for the message.
	/// </summary>
	TimeSpan? TimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the scheduled enqueue time.
	/// </summary>
	DateTimeOffset? ScheduledEnqueueTime { get; set; }

	/// <summary>
	/// Sets a custom property on the message.
	/// </summary>
	/// <param name="key">The property key.</param>
	/// <param name="value">The property value.</param>
	void SetProperty(string key, object value);
}

/// <summary>
/// AWS SQS-specific mapping context for configuring message properties.
/// </summary>
public interface IAwsSqsMappingContext
{
	/// <summary>
	/// Gets or sets the queue URL.
	/// </summary>
	string? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the message group ID (for FIFO queues).
	/// </summary>
	string? MessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets the message deduplication ID (for FIFO queues).
	/// </summary>
	string? MessageDeduplicationId { get; set; }

	/// <summary>
	/// Gets or sets the delay in seconds before the message becomes visible.
	/// </summary>
	int? DelaySeconds { get; set; }

	/// <summary>
	/// Sets a message attribute.
	/// </summary>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The attribute value.</param>
	/// <param name="dataType">The attribute data type (String, Number, Binary).</param>
	void SetAttribute(string name, string value, string dataType = "String");
}

/// <summary>
/// AWS SNS-specific mapping context for configuring message properties.
/// </summary>
public interface IAwsSnsMappingContext
{
	/// <summary>
	/// Gets or sets the topic ARN.
	/// </summary>
	string? TopicArn { get; set; }

	/// <summary>
	/// Gets or sets the message group ID (for FIFO topics).
	/// </summary>
	string? MessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets the message deduplication ID (for FIFO topics).
	/// </summary>
	string? MessageDeduplicationId { get; set; }

	/// <summary>
	/// Gets or sets the subject for email endpoints.
	/// </summary>
	string? Subject { get; set; }

	/// <summary>
	/// Sets a message attribute.
	/// </summary>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The attribute value.</param>
	/// <param name="dataType">The attribute data type (String, Number, Binary).</param>
	void SetAttribute(string name, string value, string dataType = "String");
}

/// <summary>
/// Google Pub/Sub-specific mapping context for configuring message properties.
/// </summary>
public interface IGooglePubSubMappingContext
{
	/// <summary>
	/// Gets or sets the topic name.
	/// </summary>
	string? TopicName { get; set; }

	/// <summary>
	/// Gets or sets the ordering key for ordered delivery.
	/// </summary>
	string? OrderingKey { get; set; }

	/// <summary>
	/// Sets a custom attribute on the message.
	/// </summary>
	/// <param name="key">The attribute key.</param>
	/// <param name="value">The attribute value.</param>
	void SetAttribute(string key, string value);
}

/// <summary>
/// gRPC-specific mapping context for configuring message properties.
/// </summary>
public interface IGrpcMappingContext
{
	/// <summary>
	/// Gets or sets the service method name.
	/// </summary>
	string? MethodName { get; set; }

	/// <summary>
	/// Gets or sets the deadline for the call.
	/// </summary>
	TimeSpan? Deadline { get; set; }

	/// <summary>
	/// Sets a custom header on the call.
	/// </summary>
	/// <param name="key">The header key.</param>
	/// <param name="value">The header value.</param>
	void SetHeader(string key, string value);
}
