// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Fluent configuration for the Azure Service Bus transport.
/// </summary>
/// <remarks>
/// The builder is a view over the <see cref="AzureServiceBusOptions"/> instance the options system owns:
/// every call writes into the options the transport's own components resolve, so there is no second
/// model and nothing to carry between them. Settings that are plain values are reached through
/// <see cref="ConfigureSender"/>, <see cref="ConfigureProcessor"/> and <see cref="ConfigureOptions"/>,
/// which hand you the options directly; the builder itself carries only operations that do more than
/// assign — <see cref="MapEntity{TMessage}"/> appends to a routing table.
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusTransport("orders", sb =>
/// {
///     sb.ConnectionString("Endpoint=sb://...")
///       .ConfigureProcessor(processor => processor.MaxConcurrentCalls = 20)
///       .MapEntity&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public interface IAzureServiceBusTransportBuilder
{
	/// <summary>
	/// Sets the connection string used to authenticate against Service Bus.
	/// </summary>
	/// <param name="connectionString">The Service Bus connection string.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Authenticates against the given namespace with a managed identity instead of a connection string.
	/// </summary>
	/// <param name="fullyQualifiedNamespace">The fully-qualified namespace, for example <c>my-bus.servicebus.windows.net</c>.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder FullyQualifiedNamespace(string fullyQualifiedNamespace);

	/// <summary>
	/// Sets the transport protocol used for the connection.
	/// </summary>
	/// <param name="transportType">The transport type.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder TransportType(ServiceBusTransportType transportType);

	/// <summary>
	/// Configures how messages are sent.
	/// </summary>
	/// <param name="configure">Receives the sender options to configure.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder ConfigureSender(Action<AzureServiceBusSenderOptions> configure);

	/// <summary>
	/// Configures how messages are received.
	/// </summary>
	/// <param name="configure">Receives the processor options to configure.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder ConfigureProcessor(Action<AzureServiceBusProcessorOptions> configure);

	/// <summary>
	/// Configures the CloudEvents behavior registered alongside this transport.
	/// </summary>
	/// <param name="configure">Receives the CloudEvents options to configure.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder ConfigureCloudEvents(Action<AzureServiceBusCloudEventOptions> configure);

	/// <summary>
	/// Configures any option on the transport, including those without a dedicated fluent method.
	/// </summary>
	/// <param name="configure">Receives the transport options to configure.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder ConfigureOptions(Action<AzureServiceBusOptions> configure);

	/// <summary>
	/// Routes messages of <typeparamref name="TMessage"/> to a specific queue or topic, overriding the
	/// sender's default entity.
	/// </summary>
	/// <typeparam name="TMessage">The message type to route.</typeparam>
	/// <param name="entityName">The queue or topic name to route it to.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureServiceBusTransportBuilder MapEntity<TMessage>(string entityName) where TMessage : class;
}

/// <summary>
/// Default <see cref="IAzureServiceBusTransportBuilder"/>, a view over the options being configured.
/// </summary>
internal sealed class AzureServiceBusTransportBuilder : IAzureServiceBusTransportBuilder
{
	private readonly AzureServiceBusOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusTransportBuilder"/> class over
	/// <paramref name="options"/>. The builder does not own the instance and never copies out of it.
	/// </summary>
	/// <param name="options">The options this builder configures.</param>
	public AzureServiceBusTransportBuilder(AzureServiceBusOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options;
	}

	/// <summary>
	/// Gets the CloudEvents configuration the consumer supplied, if any. CloudEvents options are a
	/// separate registration with its own entry point, so the delegate is handed on rather than its
	/// values being copied into a nested duplicate.
	/// </summary>
	public Action<AzureServiceBusCloudEventOptions>? CloudEventsConfigure { get; private set; }

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder FullyQualifiedNamespace(string fullyQualifiedNamespace)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace);
		_options.Namespace = fullyQualifiedNamespace;
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
	public IAzureServiceBusTransportBuilder ConfigureSender(Action<AzureServiceBusSenderOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options.Sender);
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConfigureProcessor(Action<AzureServiceBusProcessorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options.Processor);
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConfigureCloudEvents(Action<AzureServiceBusCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		CloudEventsConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder ConfigureOptions(Action<AzureServiceBusOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}

	/// <inheritdoc/>
	public IAzureServiceBusTransportBuilder MapEntity<TMessage>(string entityName) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
		_options.EntityMappings[typeof(TMessage)] = entityName;
		return this;
	}
}
