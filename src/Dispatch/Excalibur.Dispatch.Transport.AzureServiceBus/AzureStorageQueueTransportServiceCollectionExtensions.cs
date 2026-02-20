// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Identity;
using Azure.Storage.Queues;

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.DependencyInjection.Extensions;

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
		RegisterAzureStorageQueueServices(services, transportOptions);

		// Register Azure Storage Queue options
		RegisterOptions(services, transportOptions);

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
	/// Registers the core Azure Storage Queue services with the service collection.
	/// </summary>
	private static void RegisterAzureStorageQueueServices(
		IServiceCollection services,
		AzureStorageQueueTransportOptions transportOptions)
	{
		// Register QueueClient
		services.TryAddSingleton(sp =>
		{
			if (!string.IsNullOrEmpty(transportOptions.ConnectionString) && !string.IsNullOrEmpty(transportOptions.QueueName))
			{
				return new QueueClient(transportOptions.ConnectionString, transportOptions.QueueName);
			}

			if (transportOptions.StorageAccountUri != null && transportOptions.UseManagedIdentity)
			{
				var queueUri = new Uri(transportOptions.StorageAccountUri, transportOptions.QueueName);
				return new QueueClient(queueUri, new DefaultAzureCredential());
			}

			throw new InvalidOperationException(
				"Azure Storage Queue requires either a ConnectionString or StorageAccountUri with managed identity, and a QueueName.");
		});

	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		AzureStorageQueueTransportOptions transportOptions)
	{
		// Register AzureProviderOptions (base Azure options)
		_ = services.AddOptions<AzureProviderOptions>()
			.Configure(options =>
			{
				options.UseManagedIdentity = transportOptions.UseManagedIdentity;
				options.StorageAccountUri = transportOptions.StorageAccountUri;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Map AzureStorageQueueTransportOptions to existing AzureStorageQueueOptions
		_ = services.AddOptions<AzureStorageQueueOptions>()
			.Configure(options =>
			{
				options.ConnectionString = transportOptions.ConnectionString;
				options.StorageAccountUri = transportOptions.StorageAccountUri;
				options.QueueName = transportOptions.QueueName ?? string.Empty;
				options.MaxConcurrentMessages = transportOptions.MaxConcurrentMessages;
				options.VisibilityTimeout = transportOptions.VisibilityTimeout;
				options.PollingInterval = transportOptions.PollingInterval;
				options.MaxMessages = transportOptions.MaxMessages;
				options.EnableEncryption = transportOptions.EnableEncryption;
				options.DeadLetterQueueName = transportOptions.DeadLetterQueueName;
				options.MaxDequeueCount = transportOptions.MaxDequeueCount;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();
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
	/// Sets the visibility timeout for messages.
	/// </summary>
	/// <param name="visibilityTimeout">The visibility timeout.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder VisibilityTimeout(TimeSpan visibilityTimeout);

	/// <summary>
	/// Sets the maximum number of concurrent messages to process.
	/// </summary>
	/// <param name="maxConcurrentMessages">The maximum number of concurrent messages.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder MaxConcurrentMessages(int maxConcurrentMessages);

	/// <summary>
	/// Sets the polling interval for checking new messages.
	/// </summary>
	/// <param name="pollingInterval">The polling interval.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder PollingInterval(TimeSpan pollingInterval);

	/// <summary>
	/// Enables a dead letter queue.
	/// </summary>
	/// <param name="deadLetterQueueName">The dead letter queue name.</param>
	/// <param name="maxDequeueCount">The maximum dequeue count before sending to DLQ.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureStorageQueueTransportBuilder EnableDeadLetterQueue(string deadLetterQueueName, int maxDequeueCount = 5);

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
		_options.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder StorageAccountUri(Uri storageAccountUri)
	{
		ArgumentNullException.ThrowIfNull(storageAccountUri);
		_options.StorageAccountUri = storageAccountUri;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder UseManagedIdentity()
	{
		_options.UseManagedIdentity = true;
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
	public IAzureStorageQueueTransportBuilder VisibilityTimeout(TimeSpan visibilityTimeout)
	{
		_options.VisibilityTimeout = visibilityTimeout;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder MaxConcurrentMessages(int maxConcurrentMessages)
	{
		_options.MaxConcurrentMessages = maxConcurrentMessages;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder PollingInterval(TimeSpan pollingInterval)
	{
		_options.PollingInterval = pollingInterval;
		return this;
	}

	/// <inheritdoc/>
	public IAzureStorageQueueTransportBuilder EnableDeadLetterQueue(string deadLetterQueueName, int maxDequeueCount = 5)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterQueueName);
		_options.DeadLetterQueueName = deadLetterQueueName;
		_options.MaxDequeueCount = maxDequeueCount;
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

/// <summary>
/// Configuration options for Azure Storage Queue transport.
/// </summary>
public sealed class AzureStorageQueueTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the Azure Storage connection string.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the storage account URI for managed identity authentication.
	/// </summary>
	public Uri? StorageAccountUri { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use managed identity.
	/// </summary>
	public bool UseManagedIdentity { get; set; }

	/// <summary>
	/// Gets or sets the queue name.
	/// </summary>
	public string? QueueName { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages to process. Default is 10.
	/// </summary>
	public int MaxConcurrentMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets the visibility timeout for messages. Default is 5 minutes.
	/// </summary>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the polling interval for checking new messages. Default is 1 second.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of messages to retrieve per poll. Default is 10.
	/// </summary>
	public int MaxMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption.
	/// </summary>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the dead letter queue name.
	/// </summary>
	public string? DeadLetterQueueName { get; set; }

	/// <summary>
	/// Gets or sets the maximum dequeue count before sending to DLQ. Default is 5.
	/// </summary>
	public int MaxDequeueCount { get; set; } = 5;
}
