// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Fluent builder interface for configuring Azure Service Bus transport.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides the single entry point for Azure Service Bus transport configuration.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusTransport("orders", sb =>
/// {
///     sb.ConnectionString("Endpoint=sb://...")
///       .ConfigureSender(sender => sender.EnableBatching(true))
///       .ConfigureProcessor(processor => processor.MaxConcurrentCalls(20))
///       .MapEntity&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public interface IAzureServiceBusTransportBuilder
{
	/// <summary>
	/// Configures the Azure Service Bus connection string.
	/// </summary>
	/// <param name="connectionString">The Service Bus connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The connection string must include the Endpoint and SharedAccessKeyName.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.ConnectionString("Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=...");
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Configures the Azure Service Bus using a fully qualified namespace with managed identity.
	/// </summary>
	/// <param name="fullyQualifiedNamespace">The Service Bus fully qualified namespace.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="fullyQualifiedNamespace"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method configures the transport to use Azure Managed Identity for authentication.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.FullyQualifiedNamespace("mynamespace.servicebus.windows.net");
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder FullyQualifiedNamespace(string fullyQualifiedNamespace);

	/// <summary>
	/// Configures the transport type for Service Bus connections.
	/// </summary>
	/// <param name="transportType">The transport type (AMQP TCP or AMQP WebSockets).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use <see cref="ServiceBusTransportType.AmqpWebSockets"/> when behind a firewall
	/// that blocks AMQP over TCP.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.TransportType(ServiceBusTransportType.AmqpWebSockets);
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder TransportType(ServiceBusTransportType transportType);

	/// <summary>
	/// Configures the sender (producer) settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The sender configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure message publishing settings such as batching,
	/// default entity name, and message properties.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.ConfigureSender(sender =>
	/// {
	///     sender.DefaultEntity("orders-queue")
	///           .EnableBatching(true)
	///           .MaxBatchSizeBytes(256 * 1024);
	/// });
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder ConfigureSender(Action<IAzureServiceBusSenderBuilder> configure);

	/// <summary>
	/// Configures the processor (consumer) settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The processor configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure message consumption settings such as concurrency,
	/// auto-completion, and lock renewal.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.ConfigureProcessor(processor =>
	/// {
	///     processor.DefaultEntity("orders-queue")
	///              .MaxConcurrentCalls(20)
	///              .PrefetchCount(100)
	///              .AutoCompleteMessages(false);
	/// });
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder ConfigureProcessor(Action<IAzureServiceBusProcessorBuilder> configure);

	/// <summary>
	/// Configures CloudEvents format options.
	/// </summary>
	/// <param name="configure">The CloudEvents configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// CloudEvents provide a standardized message format for interoperability.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.ConfigureCloudEvents(ce =>
	/// {
	///     ce.EnableDuplicateDetection(true)
	///       .DuplicateDetectionWindow(TimeSpan.FromMinutes(5))
	///       .MaxDeliveryCount(10);
	/// });
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder ConfigureCloudEvents(Action<AzureServiceBusCloudEventOptions> configure);

	/// <summary>
	/// Maps a message type to a specific queue or topic.
	/// </summary>
	/// <typeparam name="TMessage">The message type to map.</typeparam>
	/// <param name="entityName">The queue or topic name for this message type.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="entityName"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When a mapping exists for a message type, the transport will send that
	/// message to the specified queue or topic instead of using the default entity.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.MapEntity&lt;OrderCreated&gt;("orders-topic")
	///   .MapEntity&lt;PaymentReceived&gt;("payments-queue");
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder MapEntity<TMessage>(string entityName) where TMessage : class;

	/// <summary>
	/// Sets a prefix to apply to entity names.
	/// </summary>
	/// <param name="prefix">The entity name prefix (e.g., "myapp-", "prod-").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="prefix"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The prefix is applied to entity names that are automatically derived from
	/// message type names, helping to organize entities by application or environment.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sb.WithEntityPrefix("myapp-prod-");
	/// // Messages of type OrderCreated would go to "myapp-prod-ordercreated"
	/// </code>
	/// </example>
	IAzureServiceBusTransportBuilder WithEntityPrefix(string prefix);
}

/// <summary>
/// Internal implementation of the Azure Service Bus transport builder.
/// </summary>
internal sealed class AzureServiceBusTransportBuilder : IAzureServiceBusTransportBuilder
{
	private readonly AzureServiceBusTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public AzureServiceBusTransportBuilder(AzureServiceBusTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		_options.UseManagedIdentity = false;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder FullyQualifiedNamespace(string fullyQualifiedNamespace)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace);
		_options.FullyQualifiedNamespace = fullyQualifiedNamespace;
		_options.UseManagedIdentity = true;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder TransportType(ServiceBusTransportType transportType)
	{
		_options.TransportType = transportType;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConfigureSender(Action<IAzureServiceBusSenderBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new AzureServiceBusSenderBuilder(_options.Sender);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConfigureProcessor(Action<IAzureServiceBusProcessorBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new AzureServiceBusProcessorBuilder(_options.Processor);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConfigureCloudEvents(Action<AzureServiceBusCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options.CloudEvents);
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder MapEntity<TMessage>(string entityName) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
		_options.EntityMappings[typeof(TMessage)] = entityName;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder WithEntityPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.EntityPrefix = prefix;
		return this;
	}
}
