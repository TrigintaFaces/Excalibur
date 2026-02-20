// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// RabbitMQ-specific message context providing access to RabbitMQ routing and delivery properties.
/// </summary>
/// <remarks>
/// <para>
/// This context extends <see cref="TransportMessageContext"/> with RabbitMQ-specific properties
/// such as exchange, routing key, delivery tag, and priority. These properties are stored
/// as transport properties and can be accessed through strongly-typed properties or via
/// <see cref="TransportMessageContext.GetTransportProperty{T}"/>.
/// </para>
/// </remarks>
public sealed class RabbitMqMessageContext : TransportMessageContext
{
	/// <summary>
	/// The transport property name for the exchange.
	/// </summary>
	public const string ExchangePropertyName = "Exchange";

	/// <summary>
	/// The transport property name for the routing key.
	/// </summary>
	public const string RoutingKeyPropertyName = "RoutingKey";

	/// <summary>
	/// The transport property name for the delivery tag.
	/// </summary>
	public const string DeliveryTagPropertyName = "DeliveryTag";

	/// <summary>
	/// The transport property name for the priority.
	/// </summary>
	public const string PriorityPropertyName = "Priority";

	/// <summary>
	/// The transport property name for the reply-to address.
	/// </summary>
	public const string ReplyToPropertyName = "ReplyTo";

	/// <summary>
	/// The transport property name for message expiration.
	/// </summary>
	public const string ExpirationPropertyName = "Expiration";

	/// <summary>
	/// The transport property name for the delivery mode (persistent/non-persistent).
	/// </summary>
	public const string DeliveryModePropertyName = "DeliveryMode";

	/// <summary>
	/// The transport property name for whether the message was redelivered.
	/// </summary>
	public const string RedeliveredPropertyName = "Redelivered";

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqMessageContext"/> class.
	/// </summary>
	/// <param name="messageId">The unique message identifier.</param>
	public RabbitMqMessageContext(string messageId)
		: base(messageId)
	{
		SourceTransport = "rabbitmq";
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqMessageContext"/> class with a generated message ID.
	/// </summary>
	public RabbitMqMessageContext()
		: base()
	{
		SourceTransport = "rabbitmq";
	}

	/// <summary>
	/// Gets or sets the exchange name for message routing.
	/// </summary>
	/// <value>The RabbitMQ exchange name, or <see langword="null"/> for the default exchange.</value>
	public string? Exchange
	{
		get => GetTransportProperty<string>(ExchangePropertyName);
		set => SetTransportProperty(ExchangePropertyName, value);
	}

	/// <summary>
	/// Gets or sets the routing key for message routing.
	/// </summary>
	/// <value>The routing key used to route the message to queues.</value>
	public string? RoutingKey
	{
		get => GetTransportProperty<string>(RoutingKeyPropertyName);
		set => SetTransportProperty(RoutingKeyPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the delivery tag assigned by RabbitMQ.
	/// </summary>
	/// <value>
	/// The delivery tag uniquely identifying this delivery on the channel.
	/// Used for acknowledgment.
	/// </value>
	public ulong DeliveryTag
	{
		get => GetTransportProperty<ulong>(DeliveryTagPropertyName);
		set => SetTransportProperty(DeliveryTagPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the message priority.
	/// </summary>
	/// <value>The message priority (0-255), or <see langword="null"/> if not set.</value>
	public byte? Priority
	{
		get => GetTransportProperty<byte?>(PriorityPropertyName);
		set => SetTransportProperty(PriorityPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the reply-to address for request-reply patterns.
	/// </summary>
	/// <value>The queue name to send replies to.</value>
	public string? ReplyTo
	{
		get => GetTransportProperty<string>(ReplyToPropertyName);
		set => SetTransportProperty(ReplyToPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the message expiration time.
	/// </summary>
	/// <value>The expiration time as a string (milliseconds), or <see langword="null"/> if the message doesn't expire.</value>
	public string? Expiration
	{
		get => GetTransportProperty<string>(ExpirationPropertyName);
		set => SetTransportProperty(ExpirationPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the delivery mode.
	/// </summary>
	/// <value>1 for non-persistent, 2 for persistent delivery.</value>
	public byte DeliveryMode
	{
		get => GetTransportProperty<byte>(DeliveryModePropertyName);
		set => SetTransportProperty(DeliveryModePropertyName, value);
	}

	/// <summary>
	/// Gets or sets a value indicating whether this message was redelivered.
	/// </summary>
	/// <value><see langword="true"/> if this is a redelivery; otherwise, <see langword="false"/>.</value>
	public bool Redelivered
	{
		get => GetTransportProperty<bool>(RedeliveredPropertyName);
		set => SetTransportProperty(RedeliveredPropertyName, value);
	}
}
