// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Fluent builder interface for configuring Kafka transport.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides the single entry point for Kafka transport configuration.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddKafkaTransport("events", kafka =>
/// {
///     kafka.BootstrapServers("localhost:9092")
///          .UseSchemaRegistry(registry => registry.Url = "http://localhost:8081")
///          .ConfigureProducer(producer => producer.Acks(KafkaAckLevel.All))
///          .ConfigureConsumer(consumer => consumer.GroupId("my-group"))
///          .MapTopic&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public interface IKafkaTransportBuilder
{
	/// <summary>
	/// Configures the Kafka bootstrap servers.
	/// </summary>
	/// <param name="servers">The bootstrap servers connection string (e.g., "localhost:9092").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="servers"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Bootstrap servers are the initial connection points for the Kafka client.
	/// Multiple servers can be specified as a comma-separated list for high availability.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.BootstrapServers("broker1:9092,broker2:9092,broker3:9092");
	/// </code>
	/// </example>
	IKafkaTransportBuilder BootstrapServers(string servers);

	/// <summary>
	/// Configures the transport to use Confluent Schema Registry for schema validation.
	/// </summary>
	/// <param name="configure">Optional action to configure schema registry options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When schema registry is enabled:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Schemas are automatically registered on first publish</description></item>
	///   <item><description>Incoming messages are validated against registered schemas</description></item>
	///   <item><description>Schema compatibility is enforced based on the configured mode</description></item>
	/// </list>
	/// <para>
	/// <strong>Note:</strong> Prefer <see cref="UseConfluentSchemaRegistry"/> for new code.
	/// This method provides backward compatibility with options-based configuration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.UseSchemaRegistry(registry =>
	/// {
	///     registry.Url = "http://localhost:8081";
	///     registry.DefaultCompatibility = CompatibilityMode.Backward;
	/// });
	/// </code>
	/// </example>
	IKafkaTransportBuilder UseSchemaRegistry(Action<ConfluentSchemaRegistryOptions>? configure = null);

	/// <summary>
	/// Configures the transport to use Confluent Schema Registry with fluent builder configuration.
	/// </summary>
	/// <param name="configure">The action to configure schema registry options using the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method follows Microsoft-style fluent builder patterns and provides a
	/// discoverable API for Schema Registry configuration.
	/// </para>
	/// <para>
	/// When schema registry is enabled:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Messages are serialized with a 5-byte Confluent wire format header</description></item>
	///   <item><description>Schemas are automatically registered on first publish (if enabled)</description></item>
	///   <item><description>Incoming messages are validated against registered schemas</description></item>
	///   <item><description>Schema compatibility is enforced based on the configured mode</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.UseConfluentSchemaRegistry(registry =>
	/// {
	///     registry.SchemaRegistryUrl("http://localhost:8081")
	///             .SubjectNameStrategy(SubjectNameStrategy.TopicNameStrategy)
	///             .CompatibilityMode(CompatibilityMode.Backward)
	///             .AutoRegisterSchemas(true)
	///             .CacheSchemas(true)
	///             .CacheCapacity(1000);
	/// });
	/// </code>
	/// </example>
	IKafkaTransportBuilder UseConfluentSchemaRegistry(Action<IConfluentSchemaRegistryBuilder> configure);

	/// <summary>
	/// Configures the Kafka producer settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The producer configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure producer-specific settings such as acknowledgment level,
	/// compression, batching, and transactions.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.ConfigureProducer(producer =>
	/// {
	///     producer.Acks(KafkaAckLevel.All)
	///             .EnableIdempotence(true)
	///             .CompressionType(KafkaCompressionType.Snappy);
	/// });
	/// </code>
	/// </example>
	IKafkaTransportBuilder ConfigureProducer(Action<IKafkaProducerBuilder> configure);

	/// <summary>
	/// Configures the Kafka consumer settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The consumer configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure consumer-specific settings such as group ID,
	/// offset reset behavior, and session management.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.ConfigureConsumer(consumer =>
	/// {
	///     consumer.GroupId("my-consumer-group")
	///             .AutoOffsetReset(KafkaOffsetReset.Earliest)
	///             .SessionTimeout(TimeSpan.FromSeconds(45));
	/// });
	/// </code>
	/// </example>
	IKafkaTransportBuilder ConfigureConsumer(Action<IKafkaConsumerBuilder> configure);

	/// <summary>
	/// Maps a message type to a specific Kafka topic.
	/// </summary>
	/// <typeparam name="TMessage">The message type to map.</typeparam>
	/// <param name="topic">The Kafka topic name for this message type.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topic"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When a mapping exists for a message type, the transport will publish that
	/// message to the specified topic instead of using the default topic derivation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.MapTopic&lt;OrderCreated&gt;("orders-events")
	///      .MapTopic&lt;PaymentReceived&gt;("payment-events");
	/// </code>
	/// </example>
	IKafkaTransportBuilder MapTopic<TMessage>(string topic) where TMessage : class;

	/// <summary>
	/// Sets a prefix to apply to automatically generated topic names.
	/// </summary>
	/// <param name="prefix">The topic name prefix (e.g., "myapp-", "prod-").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="prefix"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The prefix is applied to topic names that are automatically derived from
	/// message type names, helping to organize topics by application or environment.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.WithTopicPrefix("myapp-prod-");
	/// // Messages of type OrderCreated would go to "myapp-prod-ordercreated"
	/// </code>
	/// </example>
	IKafkaTransportBuilder WithTopicPrefix(string prefix);

	/// <summary>
	/// Configures CloudEvents settings for the Kafka transport.
	/// </summary>
	/// <param name="configure">The action to configure CloudEvents options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure CloudEvents-specific settings such as:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Partitioning strategy (CorrelationId, EventType, etc.)</description></item>
	///   <item><description>Default topic for CloudEvents</description></item>
	///   <item><description>Acknowledgment levels and idempotency</description></item>
	///   <item><description>Compression settings</description></item>
	///   <item><description>Transaction support for exactly-once semantics</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// kafka.ConfigureCloudEvents(ce =>
	/// {
	///     ce.PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId;
	///     ce.AcknowledgmentLevel = KafkaAckLevel.All;
	///     ce.EnableIdempotentProducer = true;
	///     ce.EnableTransactions = true;
	///     ce.TransactionalId = "orders-producer";
	/// });
	/// </code>
	/// </example>
	IKafkaTransportBuilder ConfigureCloudEvents(Action<KafkaCloudEventOptions> configure);
}

/// <summary>
/// Internal implementation of the Kafka transport builder.
/// </summary>
internal sealed class KafkaTransportBuilder : IKafkaTransportBuilder
{
	private readonly KafkaTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public KafkaTransportBuilder(KafkaTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder BootstrapServers(string servers)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(servers);
		_options.BootstrapServers = servers;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder UseSchemaRegistry(Action<ConfluentSchemaRegistryOptions>? configure = null)
	{
		_options.SchemaRegistry = new ConfluentSchemaRegistryOptions();
		configure?.Invoke(_options.SchemaRegistry);
		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder UseConfluentSchemaRegistry(Action<IConfluentSchemaRegistryBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.SchemaRegistry = new ConfluentSchemaRegistryOptions();
		var builder = new ConfluentSchemaRegistryBuilder(_options.SchemaRegistry);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder ConfigureProducer(Action<IKafkaProducerBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.ProducerOptions = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(_options.ProducerOptions);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder ConfigureConsumer(Action<IKafkaConsumerBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.ConsumerOptions = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(_options.ConsumerOptions);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder MapTopic<TMessage>(string topic) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		_options.TopicMappings[typeof(TMessage)] = topic;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder WithTopicPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.TopicPrefix = prefix;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaTransportBuilder ConfigureCloudEvents(Action<KafkaCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.CloudEventOptions ??= new KafkaCloudEventOptions();
		configure(_options.CloudEventOptions);

		return this;
	}
}
