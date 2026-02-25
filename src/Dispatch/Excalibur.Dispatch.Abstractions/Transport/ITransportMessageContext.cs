// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides transport-level context for messages being transformed between different message transports.
/// </summary>
/// <remarks>
/// <para>
/// This interface captures the transport-agnostic metadata required for multi-transport message mapping.
/// It differs from <see cref="IMessageContext"/> which is focused on the dispatch
/// pipeline. <see cref="ITransportMessageContext"/> is specifically designed for:
/// </para>
/// <list type="bullet">
/// <item><description>Capturing source transport metadata when receiving messages</description></item>
/// <item><description>Configuring target transport properties when publishing messages</description></item>
/// <item><description>Enabling message transformation between transport formats (RabbitMQ ↔ Kafka ↔ SQS)</description></item>
/// <item><description>Preserving correlation and tracing information across transport boundaries</description></item>
/// </list>
/// <para>
/// Transport-specific implementations (e.g., <c>RabbitMqMessageContext</c>, <c>KafkaMessageContext</c>)
/// extend this interface with transport-specific properties accessible via <see cref="GetTransportProperty{T}"/>.
/// </para>
/// </remarks>
public interface ITransportMessageContext
{
	/// <summary>
	/// Gets the unique identifier for this message.
	/// </summary>
	/// <value>The message identifier, typically a GUID string.</value>
	string MessageId { get; }

	/// <summary>
	/// Gets the correlation identifier for tracking related messages across services.
	/// </summary>
	/// <value>The correlation identifier, or <see langword="null"/> if not set.</value>
	string? CorrelationId { get; }

	/// <summary>
	/// Gets the causation identifier linking this message to its direct cause.
	/// </summary>
	/// <value>The causation identifier, or <see langword="null"/> if not set.</value>
	string? CausationId { get; }

	/// <summary>
	/// Gets the name of the source transport that originated this message.
	/// </summary>
	/// <value>
	/// The transport name (e.g., "rabbitmq", "kafka", "sqs"), or <see langword="null"/>
	/// if the message did not originate from a transport.
	/// </value>
	string? SourceTransport { get; }

	/// <summary>
	/// Gets the name of the target transport for message delivery.
	/// </summary>
	/// <value>
	/// The transport name (e.g., "rabbitmq", "kafka", "sqs"), or <see langword="null"/>
	/// if no specific target is configured.
	/// </value>
	string? TargetTransport { get; }

	/// <summary>
	/// Gets the message headers as key-value pairs.
	/// </summary>
	/// <value>
	/// A read-only dictionary of header names to header values.
	/// Headers provide metadata that travels with the message across transports.
	/// </value>
	IReadOnlyDictionary<string, string> Headers { get; }

	/// <summary>
	/// Gets the timestamp when the message was created or sent.
	/// </summary>
	/// <value>The message timestamp in UTC.</value>
	DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets the content type of the message payload.
	/// </summary>
	/// <value>
	/// The MIME content type (e.g., "application/json"), or <see langword="null"/> if not specified.
	/// </value>
	string? ContentType { get; }

	/// <summary>
	/// Gets a transport-specific property by name.
	/// </summary>
	/// <typeparam name="T">The expected type of the property value.</typeparam>
	/// <param name="propertyName">The name of the transport-specific property.</param>
	/// <returns>
	/// The property value cast to <typeparamref name="T"/>, or <see langword="default"/>
	/// if the property is not found or cannot be cast.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Use this method to access transport-specific properties that are not part of the
	/// common interface. Examples include:
	/// </para>
	/// <list type="bullet">
	/// <item><description>RabbitMQ: RoutingKey, Exchange, DeliveryTag</description></item>
	/// <item><description>Kafka: Partition, Offset, Key</description></item>
	/// <item><description>SQS: ReceiptHandle, MessageGroupId, SequenceNumber</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// // RabbitMQ-specific property
	/// var routingKey = context.GetTransportProperty&lt;string&gt;("RoutingKey");
	///
	/// // Kafka-specific property
	/// var partition = context.GetTransportProperty&lt;int&gt;("Partition");
	/// </code>
	/// </example>
	T? GetTransportProperty<T>(string propertyName);

	/// <summary>
	/// Sets a transport-specific property by name.
	/// </summary>
	/// <typeparam name="T">The type of the property value.</typeparam>
	/// <param name="propertyName">The name of the transport-specific property.</param>
	/// <param name="value">The property value to set.</param>
	/// <remarks>
	/// <para>
	/// Use this method to set transport-specific properties when preparing a message
	/// for delivery to a specific transport. The properties set here will be used
	/// by the target transport adapter.
	/// </para>
	/// </remarks>
	void SetTransportProperty<T>(string propertyName, T value);

	/// <summary>
	/// Determines whether a transport-specific property exists.
	/// </summary>
	/// <param name="propertyName">The name of the property to check.</param>
	/// <returns>
	/// <see langword="true"/> if the property exists; otherwise, <see langword="false"/>.
	/// </returns>
	bool HasTransportProperty(string propertyName);

	/// <summary>
	/// Gets all transport-specific properties as a dictionary.
	/// </summary>
	/// <returns>A read-only dictionary of all transport-specific property names and values.</returns>
	IReadOnlyDictionary<string, object?> GetAllTransportProperties();
}
