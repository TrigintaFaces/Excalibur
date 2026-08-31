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
/// <para>
/// Properties are organized into sub-option groups for clarity:
/// <see cref="Connection"/> for connection settings,
/// <see cref="Queue"/> for queue declaration settings,
/// <see cref="DeadLetter"/> for dead letter exchange settings,
/// and <see cref="Consumption"/> for consumer behavior settings.
/// </para>
/// </remarks>
public sealed class RabbitMqOptions
{
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
	/// Gets or sets the connection options.
	/// </summary>
	/// <value>The connection configuration options.</value>
	public RabbitMqConnectionOptions Connection { get; set; } = new();

	/// <summary>
	/// Gets or sets the queue declaration options.
	/// </summary>
	/// <value>The queue configuration options.</value>
	public RabbitMqQueueOptions Queue { get; set; } = new();

	/// <summary>
	/// Gets or sets the dead letter exchange options.
	/// </summary>
	/// <value>The dead letter configuration options.</value>
	public RabbitMqDeadLetterExchangeOptions DeadLetter { get; set; } = new();

	/// <summary>
	/// Gets or sets the consumption options.
	/// </summary>
	/// <value>The consumption configuration options.</value>
	public RabbitMqConsumptionOptions Consumption { get; set; } = new();
}

/// <summary>
/// Connection-related options for RabbitMQ.
/// </summary>
public sealed class RabbitMqConnectionOptions
{
	/// <summary>
	/// Gets or sets the RabbitMQ connection string.
	/// </summary>
	/// <value>The RabbitMQ connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// Queue declaration options for RabbitMQ.
/// </summary>
public sealed class RabbitMqQueueOptions
{
	/// <summary>
	/// Gets or sets the queue name for consuming.
	/// </summary>
	/// <value>The queue name for consuming.</value>
	public string QueueName { get; set; } = string.Empty;

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
}

/// <summary>
/// Dead letter exchange options for RabbitMQ.
/// </summary>
public sealed class RabbitMqDeadLetterExchangeOptions
{
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
}

/// <summary>
/// Consumption behavior options for RabbitMQ.
/// </summary>
public sealed class RabbitMqConsumptionOptions
{
	/// <summary>
	/// Gets or sets the maximum inbound-payload length, in bytes, enforced at receive ingress before
	/// the body is materialized (defense-in-depth DoS hardening; the RabbitMQ analogue of Kestrel's
	/// <c>MaxRequestBodySize</c>). An over-limit message is rejected before deserialization.
	/// </summary>
	/// <value>
	/// The maximum payload length in bytes. Default is 4 MiB (bounded by default so the guard is never
	/// inert). Set to <see langword="null"/> to opt out (unbounded) for larger legitimate payloads.
	/// </value>
	[Range(1, int.MaxValue)]
	public int? MaxPayloadBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;
}
