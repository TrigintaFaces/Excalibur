// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Central configuration options for RabbitMQ transport, containing all sub-configuration.
/// </summary>
/// <remarks>
/// <para>
/// This class aggregates all RabbitMQ transport configuration options using sub-option
/// objects for connection, topology, and dead letter settings.
/// </para>
/// <para>
/// Options are populated via the fluent builder API through
/// <see cref="IRabbitMQTransportBuilder"/> and its sub-builders.
/// </para>
/// </remarks>
public sealed class RabbitMQTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	/// <value>The transport name. Default is "rabbitmq".</value>
	public string Name { get; set; } = "rabbitmq";

	/// <summary>
	/// Gets the connection configuration options.
	/// </summary>
	/// <value>The connection options including host, port, credentials, and SSL.</value>
	public RabbitMQConnectionOptions Connection { get; } = new();

	/// <summary>
	/// Gets the topology configuration options.
	/// </summary>
	/// <value>The topology options including exchanges, queues, bindings, and mappings.</value>
	public RabbitMQTopologyOptions Topology { get; } = new();

	/// <summary>
	/// Gets the dead letter configuration.
	/// </summary>
	/// <value>The dead letter options.</value>
	public RabbitMQDeadLetterOptions DeadLetter { get; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether dead letter exchange is enabled.
	/// </summary>
	/// <value><see langword="true"/> if dead letter exchange is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableDeadLetter { get; set; }

	/// <summary>
	/// Gets the CloudEvents configuration options.
	/// </summary>
	/// <value>The CloudEvents options.</value>
	public RabbitMqCloudEventOptions CloudEvents { get; } = new();

	/// <summary>
	/// Gets the additional configuration dictionary for custom settings.
	/// </summary>
	/// <value>A dictionary of additional configuration key-value pairs.</value>
	public Dictionary<string, string> AdditionalConfig { get; } = [];
}

/// <summary>
/// Connection configuration options for RabbitMQ transport.
/// </summary>
/// <remarks>
/// Groups all connection-related settings: host, port, virtual host,
/// credentials, connection string, and SSL configuration.
/// Follows <c>Azure.Messaging.ServiceBus.ServiceBusClientOptions</c> sub-options pattern.
/// </remarks>
public sealed class RabbitMQConnectionOptions
{
	/// <summary>
	/// Gets or sets the RabbitMQ host name.
	/// </summary>
	/// <value>The host name. Default is "localhost".</value>
	public string HostName { get; set; } = "localhost";

	/// <summary>
	/// Gets or sets the RabbitMQ port.
	/// </summary>
	/// <value>The port number. Default is 5672.</value>
	public int Port { get; set; } = 5672;

	/// <summary>
	/// Gets or sets the virtual host.
	/// </summary>
	/// <value>The virtual host. Default is "/".</value>
	public string VirtualHost { get; set; } = "/";

	/// <summary>
	/// Gets or sets the username for authentication.
	/// </summary>
	/// <value>The username. Default is "guest".</value>
	public string Username { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the password for authentication.
	/// </summary>
	/// <value>The password. Default is "guest".</value>
	[JsonIgnore]
	public string Password { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the connection string (alternative to individual connection properties).
	/// </summary>
	/// <value>The AMQP connection string, or null to use individual properties.</value>
	[Required]
	[JsonIgnore]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether SSL/TLS is enabled.
	/// </summary>
	/// <value><see langword="true"/> if SSL is enabled; otherwise, <see langword="false"/>.</value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets the SSL configuration options.
	/// </summary>
	/// <value>The SSL options.</value>
	public RabbitMQSslOptions Ssl { get; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether automatic connection recovery is enabled.
	/// </summary>
	/// <value><see langword="true"/> to recover the connection automatically after a network
	/// failure; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool AutomaticRecoveryEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval between network recovery attempts.
	/// </summary>
	/// <value>The recovery interval. Default is 10 seconds.</value>
	public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Topology configuration options for RabbitMQ transport.
/// </summary>
/// <remarks>
/// Groups all topology-related settings: exchanges, queues, bindings,
/// type-to-name mappings, and name prefixes.
/// </remarks>
public sealed class RabbitMQTopologyOptions
{
	/// <summary>
	/// Gets the exchange configurations.
	/// </summary>
	/// <value>The list of exchange configurations.</value>
	public List<RabbitMQExchangeOptions> Exchanges { get; } = [];

	/// <summary>
	/// Gets the queue configurations.
	/// </summary>
	/// <value>The list of queue configurations.</value>
	public List<RabbitMQQueueOptions> Queues { get; } = [];

	/// <summary>
	/// Gets the binding configurations.
	/// </summary>
	/// <value>The list of binding configurations.</value>
	public List<RabbitMQBindingOptions> Bindings { get; } = [];

	/// <summary>
	/// Gets the exchange mappings for message types.
	/// </summary>
	/// <value>A dictionary mapping message types to exchange names.</value>
	public Dictionary<Type, string> ExchangeMappings { get; } = [];

	/// <summary>
	/// Gets the queue mappings for message types.
	/// </summary>
	/// <value>A dictionary mapping message types to queue names.</value>
	public Dictionary<Type, string> QueueMappings { get; } = [];

	/// <summary>
	/// Gets or sets the exchange name prefix for all exchanges.
	/// </summary>
	/// <value>The exchange prefix, or null for no prefix.</value>
	public string? ExchangePrefix { get; set; }

	/// <summary>
	/// Gets or sets the queue name prefix for all queues.
	/// </summary>
	/// <value>The queue prefix, or null for no prefix.</value>
	public string? QueuePrefix { get; set; }
}

/// <summary>
/// SSL/TLS configuration options for RabbitMQ connections.
/// </summary>
public sealed class RabbitMQSslOptions
{
	/// <summary>
	/// Gets or sets the server name for SSL certificate validation.
	/// </summary>
	/// <value>The server name.</value>
	public string? ServerName { get; set; }

	/// <summary>
	/// Gets or sets the path to the client certificate file.
	/// </summary>
	/// <value>The certificate file path.</value>
	public string? CertificatePath { get; set; }

	/// <summary>
	/// Gets or sets the certificate passphrase.
	/// </summary>
	/// <value>The certificate passphrase.</value>
	public string? CertificatePassphrase { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether an unencrypted broker connection is refused.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to refuse to build a connection factory that would connect in the clear;
	/// <see langword="false"/> to permit one. Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// The refusal happens when the connection factory is built, so a plaintext registration fails where
	/// it is wired rather than at the first message. Every AMQP client this transport creates is reached
	/// through that one factory, so the refusal covers all of them.
	/// </para>
	/// <para>
	/// A connection carries TLS when its connection string uses the <c>amqps</c> scheme or when
	/// <see cref="RabbitMQConnectionOptions.UseSsl"/> is set. Neither being present is plaintext, so this
	/// setting is read whether or not the rest of this group has been configured.
	/// </para>
	/// <para>
	/// <strong>Setting this to false permits credentials and message payloads to travel in the clear.</strong>
	/// It exists for local brokers and test fixtures, not for anything holding real data.
	/// </para>
	/// </remarks>
	public bool RequireTls { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether a broker certificate that fails trust validation is accepted.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to complete the handshake despite an untrusted certificate;
	/// <see langword="false"/> to require a certificate that validates. Default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// This waives exactly two validation errors: a certificate that does not chain to a trusted root, and
	/// a certificate whose subject does not match the host being dialled. It does <em>not</em> disable
	/// validation wholesale -- a broker that presents no certificate at all is still refused, so the
	/// connection can never silently become unauthenticated.
	/// </para>
	/// <para>
	/// The relaxation is applied to any connection that carries TLS, whether TLS came from
	/// <see cref="RabbitMQConnectionOptions.UseSsl"/> or from an <c>amqps</c> connection string. It has no
	/// effect on a plaintext connection, which has no handshake to relax.
	/// </para>
	/// <para>
	/// <strong>The connection stays encrypted but stops being authenticated: an interposed party
	/// presenting any certificate is accepted.</strong> It exists for self-signed local brokers and test
	/// fixtures, not for anything holding real data.
	/// </para>
	/// </remarks>
	public bool AcceptUntrustedCertificates { get; set; }
}

/// <summary>
/// Configuration options for a RabbitMQ exchange.
/// </summary>
public sealed class RabbitMQExchangeOptions
{
	/// <summary>
	/// Gets or sets the exchange name.
	/// </summary>
	/// <value>The exchange name.</value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exchange type.
	/// </summary>
	/// <value>The exchange type. Default is <see cref="RabbitMQExchangeType.Topic"/>.</value>
	public RabbitMQExchangeType Type { get; set; } = RabbitMQExchangeType.Topic;

	/// <summary>
	/// Gets or sets a value indicating whether the exchange is durable.
	/// </summary>
	/// <value><see langword="true"/> if durable; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool Durable { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the exchange is auto-deleted when no longer used.
	/// </summary>
	/// <value><see langword="true"/> if auto-deleted; otherwise, <see langword="false"/>.</value>
	public bool AutoDelete { get; set; }

	/// <summary>
	/// Gets the additional arguments for exchange declaration.
	/// </summary>
	/// <value>A dictionary of additional arguments.</value>
	public Dictionary<string, object> Arguments { get; } = [];
}

/// <summary>
/// Configuration options for a RabbitMQ queue.
/// </summary>
/// <remarks>
/// This type intentionally exceeds the Microsoft-first ≤10-property DTO guideline: it is a flat mirror
/// of the AMQP <c>queue.declare</c> / <c>basic.qos</c> parameter surface that RabbitMQ consumers already
/// know — <see cref="Durable"/>, <see cref="Exclusive"/>, <see cref="AutoDelete"/> and the
/// <see cref="Arguments"/> map (into which <see cref="MessageTtl"/> → <c>x-message-ttl</c>,
/// <see cref="MaxLength"/> → <c>x-max-length</c>, <see cref="MaxLengthBytes"/> → <c>x-max-length-bytes</c>),
/// plus the consumer setting <see cref="PrefetchCount"/> (→ <c>basic.qos</c>) and the
/// framework's <see cref="MaxPayloadBytes"/> ingress guard. Splitting these into nested groups would diverge
/// from the well-known broker vocabulary and reduce discoverability, so the flat surface is retained by design.
/// </remarks>
public sealed class RabbitMQQueueOptions
{
	/// <summary>
	/// Gets or sets the queue name.
	/// </summary>
	/// <value>The queue name.</value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the queue is durable.
	/// </summary>
	/// <value><see langword="true"/> if durable; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool Durable { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the queue is exclusive to this connection.
	/// </summary>
	/// <value><see langword="true"/> if exclusive; otherwise, <see langword="false"/>.</value>
	public bool Exclusive { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the queue is auto-deleted when no longer used.
	/// </summary>
	/// <value><see langword="true"/> if auto-deleted; otherwise, <see langword="false"/>.</value>
	public bool AutoDelete { get; set; }

	/// <summary>
	/// Gets or sets the prefetch count for this queue.
	/// </summary>
	/// <value>The prefetch count. Default is 100.</value>
	public ushort PrefetchCount { get; set; } = 100;

	/// <summary>
	/// Gets or sets the message time-to-live.
	/// </summary>
	/// <value>The TTL duration, or null for no TTL.</value>
	public TimeSpan? MessageTtl { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of messages in the queue.
	/// </summary>
	/// <value>The max length, or null for unlimited.</value>
	public int? MaxLength { get; set; }

	/// <summary>
	/// Gets or sets the maximum total size in bytes for messages in the queue.
	/// </summary>
	/// <value>The max length in bytes, or null for unlimited.</value>
	public long? MaxLengthBytes { get; set; }

	/// <summary>
	/// Gets or sets the maximum inbound-payload length, in bytes, enforced at receive ingress before
	/// the body is materialized (defense-in-depth DoS hardening). An over-limit message is rejected
	/// before deserialization.
	/// </summary>
	/// <value>
	/// The maximum payload length in bytes. Default is 4 MiB (bounded so the guard is never inert).
	/// Set to <see langword="null"/> to opt out (unbounded) for larger legitimate payloads.
	/// </value>
	public int? MaxPayloadBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;

	/// <summary>
	/// Gets the additional arguments for queue declaration.
	/// </summary>
	/// <value>A dictionary of additional arguments.</value>
	public Dictionary<string, object> Arguments { get; } = [];
}

/// <summary>
/// Configuration options for a RabbitMQ binding.
/// </summary>
public sealed class RabbitMQBindingOptions
{
	/// <summary>
	/// Gets or sets the source exchange name.
	/// </summary>
	/// <value>The exchange name.</value>
	public string Exchange { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the destination queue name.
	/// </summary>
	/// <value>The queue name.</value>
	public string Queue { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the routing key pattern.
	/// </summary>
	/// <value>The routing key. Default is "#" (all messages).</value>
	public string RoutingKey { get; set; } = "#";

	/// <summary>
	/// Gets the additional arguments for binding.
	/// </summary>
	/// <value>A dictionary of additional arguments.</value>
	public Dictionary<string, object> Arguments { get; } = [];
}

/// <summary>
/// Configuration options for RabbitMQ dead letter handling.
/// </summary>
public sealed class RabbitMQDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the dead letter exchange name.
	/// </summary>
	/// <value>The DLX name.</value>
	public string Exchange { get; set; } = "dead-letters";

	/// <summary>
	/// Gets or sets the dead letter queue name.
	/// </summary>
	/// <value>The DLQ name.</value>
	public string Queue { get; set; } = "dead-letter-queue";

	/// <summary>
	/// Gets or sets the routing key for dead letter messages.
	/// </summary>
	/// <value>The routing key. Default is "#".</value>
	public string RoutingKey { get; set; } = "#";
}

/// <summary>
/// RabbitMQ exchange types.
/// </summary>
public enum RabbitMQExchangeType
{
	/// <summary>
	/// Direct exchange - routes based on exact routing key match.
	/// </summary>
	Direct = 0,

	/// <summary>
	/// Topic exchange - routes based on routing key patterns with wildcards.
	/// </summary>
	Topic = 1,

	/// <summary>
	/// Fanout exchange - routes to all bound queues regardless of routing key.
	/// </summary>
	Fanout = 2,

	/// <summary>
	/// Headers exchange - routes based on message header values.
	/// </summary>
	Headers = 3,
}
