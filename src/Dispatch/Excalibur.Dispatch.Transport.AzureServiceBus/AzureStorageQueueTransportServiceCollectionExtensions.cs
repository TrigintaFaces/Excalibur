// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Azure.Identity;
using Azure.Storage.Queues;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Storage Queue transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for Azure Storage Queue transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAzureStorageQueueTransport(IServiceCollection, string, Action{IAzureStorageQueueTransportBuilder})"/>
/// to register a named Azure Storage Queue transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureStorageQueueTransport("orders", sq =>
/// {
///     sq.ConnectionString("DefaultEndpointsProtocol=https;AccountName=...")
///       .QueueName("orders-queue")
///       .VisibilityTimeout(TimeSpan.FromMinutes(5))
///       .MaxConcurrentMessages(10);
/// });
/// </code>
/// </example>
public static class AzureStorageQueueTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "azure-storagequeue";

	/// <summary>
	/// Adds an Azure Storage Queue transport with the specified name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name for multi-transport routing.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for Azure Storage Queue transport configuration.
	/// It provides access to all fluent builder APIs for queue configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different Storage Queues.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAzureStorageQueueTransport("orders", sq =>
	/// {
	///     sq.ConnectionString("DefaultEndpointsProtocol=https;...")
	///       .QueueName("orders-queue")
	///       .MaxConcurrentMessages(20);
	/// });
	///
	/// services.AddAzureStorageQueueTransport("notifications", sq =>
	/// {
	///     sq.StorageAccountUri(new Uri("https://myaccount.queue.core.windows.net"))
	///       .UseManagedIdentity()
	///       .QueueName("notifications-queue");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureStorageQueueTransport(
		this IServiceCollection services,
		string name,
		Action<IAzureStorageQueueTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new AzureStorageQueueTransportOptions { Name = name };
		var builder = new AzureStorageQueueTransportBuilder(transportOptions);
		configure(builder);

		// Register core Azure Storage Queue services
		RegisterAzureStorageQueueServices(services, name, transportOptions);

		return services;
	}

	/// <summary>
	/// Adds an Azure Storage Queue transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "azure-storagequeue".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAzureStorageQueueTransport(sq =>
	/// {
	///     sq.ConnectionString("DefaultEndpointsProtocol=https;...")
	///       .QueueName("my-queue");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureStorageQueueTransport(
		this IServiceCollection services,
		Action<IAzureStorageQueueTransportBuilder> configure)
	{
		return services.AddAzureStorageQueueTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Accumulated message type registrations for the <see cref="MessageDeserializerRegistry"/>,
	/// drained once when the registry is first resolved.
	/// </summary>
	internal static readonly ConcurrentBag<Action<MessageDeserializerRegistry>> MessageDeserializerPendingRegistrations = [];

	/// <summary>
	/// Registers a message type for AOT-safe deserialization with Azure Storage Queue transport.
	/// </summary>
	/// <typeparam name="TMessage">The message type to register for deserialization.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// AOT consumers must call this method for each message type used with Storage Queues.
	/// JIT consumers do not need this — the existing reflection path handles type resolution automatically.
	/// </para>
	/// <para>
	/// This follows the same explicit-generic-DI pattern used by <c>AddSaga&lt;TSaga, TSagaState&gt;()</c>
	/// and <c>AddCachePolicy&lt;TMessage, TPolicy&gt;()</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Register message types for AOT-safe Storage Queue deserialization
	/// services.AddStorageQueueMessage&lt;OrderCreatedEvent&gt;();
	/// services.AddStorageQueueMessage&lt;PaymentProcessedEvent&gt;();
	/// </code>
	/// </example>
	public static IServiceCollection AddStorageQueueMessage<TMessage>(
		this IServiceCollection services)
		where TMessage : class, IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(services);

		// Accumulate for AOT registry population
		MessageDeserializerPendingRegistrations.Add(
			static registry => registry.Register<TMessage>());

		// Registry is built from the accumulated registrations on first resolution (idempotent).
		services.TryAddSingleton(_ =>
		{
			var registry = new MessageDeserializerRegistry();
			foreach (var pending in MessageDeserializerPendingRegistrations)
			{
				pending(registry);
			}

			registry.Freeze();
			return registry;
		});

		return services;
	}

	/// <summary>
	/// Registers the core Azure Storage Queue services with the service collection.
	/// </summary>
	private static void RegisterAzureStorageQueueServices(
		IServiceCollection services,
		string name,
		AzureStorageQueueTransportOptions transportOptions)
	{
		// Keyed by transport name: TryAddSingleton-by-type de-duplicates, so a second named Storage
		// Queue transport contributed no registration and both names resolved the first transport's
		// client, silently sending every named transport to the first transport's queue.
		services.TryAddKeyedSingleton<QueueClient>(name, (_, _) =>
		{
			if (!string.IsNullOrEmpty(transportOptions.Connection.ConnectionString) && !string.IsNullOrEmpty(transportOptions.QueueName))
			{
				return new QueueClient(transportOptions.Connection.ConnectionString, transportOptions.QueueName);
			}

			if (transportOptions.Connection.StorageAccountUri != null && transportOptions.Connection.UseManagedIdentity)
			{
				var queueUri = new Uri(transportOptions.Connection.StorageAccountUri, transportOptions.QueueName);
				return new QueueClient(queueUri, new DefaultAzureCredential());
			}

			throw new InvalidOperationException(
				"Azure Storage Queue requires either a ConnectionString or StorageAccountUri with managed identity, and a QueueName.");
		});

		// Unkeyed convenience registration for the single-transport host. TryAdd, so the
		// first-registered named transport wins -- a multi-transport host must resolve the keyed
		// client by name instead.
		services.TryAddSingleton(sp => sp.GetRequiredKeyedService<QueueClient>(name));
	}

}

/// <summary>
/// Builder interface for fluent Azure Storage Queue transport configuration.
/// </summary>
public interface IAzureStorageQueueTransportBuilder
{
	/// <summary>
	/// Sets the Azure Storage connection string.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets the storage account URI for managed identity authentication.
	/// </summary>
	/// <param name="storageAccountUri">The storage account URI (e.g., https://myaccount.queue.core.windows.net).</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder StorageAccountUri(Uri storageAccountUri);

	/// <summary>
	/// Enables managed identity authentication.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder UseManagedIdentity();

	/// <summary>
	/// Sets the queue name.
	/// </summary>
	/// <param name="queueName">The queue name.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder QueueName(string queueName);

	/// <summary>
	/// Configures the Azure Storage Queue options.
	/// </summary>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder ConfigureOptions(Action<AzureStorageQueueTransportOptions> configure);
}

/// <summary>
/// Implementation of the Azure Storage Queue transport builder.
/// </summary>
internal sealed class AzureStorageQueueTransportBuilder : IAzureStorageQueueTransportBuilder
{
	private readonly AzureStorageQueueTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureStorageQueueTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public AzureStorageQueueTransportBuilder(AzureStorageQueueTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.Connection.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder StorageAccountUri(Uri storageAccountUri)
	{
		ArgumentNullException.ThrowIfNull(storageAccountUri);
		_options.Connection.StorageAccountUri = storageAccountUri;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder UseManagedIdentity()
	{
		_options.Connection.UseManagedIdentity = true;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder QueueName(string queueName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
		_options.QueueName = queueName;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder ConfigureOptions(Action<AzureStorageQueueTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}
}
