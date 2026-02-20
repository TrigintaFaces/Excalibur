// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
public class RabbitMQTransportOptions
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
public class RabbitMQConnectionOptions
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
	public string Password { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the connection string (alternative to individual connection properties).
	/// </summary>
	/// <value>The AMQP connection string, or null to use individual properties.</value>
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
}

/// <summary>
/// Topology configuration options for RabbitMQ transport.
/// </summary>
/// <remarks>
/// Groups all topology-related settings: exchanges, queues, bindings,
/// type-to-name mappings, and name prefixes.
/// </remarks>
public class RabbitMQTopologyOptions
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
public class RabbitMQSslOptions
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
	/// Gets or sets a value indicating whether to accept untrusted certificates.
	/// </summary>
	/// <value><see langword="true"/> to accept untrusted certificates; otherwise, <see langword="false"/>.</value>
	/// <remarks>
	/// <b>Warning:</b> Setting this to <see langword="true"/> disables certificate validation
	/// and should only be used in development/testing environments.
	/// </remarks>
	public bool AcceptUntrustedCertificates { get; set; }
}

/// <summary>
/// Configuration options for a RabbitMQ exchange.
/// </summary>
public class RabbitMQExchangeOptions
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
public class RabbitMQQueueOptions
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
	/// <value>The prefetch count. Default is 10.</value>
	public ushort PrefetchCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether messages are auto-acknowledged.
	/// </summary>
	/// <value><see langword="true"/> for auto-acknowledge; otherwise, <see langword="false"/>.</value>
	public bool AutoAck { get; set; }

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
	/// Gets the additional arguments for queue declaration.
	/// </summary>
	/// <value>A dictionary of additional arguments.</value>
	public Dictionary<string, object> Arguments { get; } = [];
}

/// <summary>
/// Configuration options for a RabbitMQ binding.
/// </summary>
public class RabbitMQBindingOptions
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
public class RabbitMQDeadLetterOptions
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

	/// <summary>
	/// Gets or sets the maximum number of retry attempts before dead-lettering.
	/// </summary>
	/// <value>The max retries. Default is 3.</value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	/// <value>The retry delay. Default is 30 seconds.</value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
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
