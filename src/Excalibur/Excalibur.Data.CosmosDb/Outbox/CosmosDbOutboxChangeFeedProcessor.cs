// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Cosmos DB change feed processor for outbox messages.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Cosmos DB change feed processor pattern to receive real-time notifications
/// when outbox messages are created or modified. This provides lower latency than polling
/// and automatic load balancing across multiple processor instances.
/// </para>
/// <para>
/// Follows the Microsoft BackgroundService pattern with Start/Stop lifecycle (2 methods).
/// Reference: Azure Cosmos DB Change Feed Processor Library.
/// </para>
/// </remarks>
public sealed partial class CosmosDbOutboxChangeFeedProcessor : IAsyncDisposable
{
	private readonly CosmosDbOutboxOptions _outboxOptions;
	private readonly CosmosDbChangeFeedOptions _feedOptions;
	private readonly ILogger<CosmosDbOutboxChangeFeedProcessor> _logger;
	private readonly Func<IReadOnlyCollection<OutboundMessage>, CancellationToken, Task>? _onChangesReceived;

	private CosmosClient? _client;
	private ChangeFeedProcessor? _processor;
	private volatile bool _disposed;
	private volatile bool _running;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxChangeFeedProcessor"/> class.
	/// </summary>
	/// <param name="outboxOptions"> The Cosmos DB outbox store options. </param>
	/// <param name="feedOptions"> The change feed processor options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="onChangesReceived"> Optional callback when changes are detected. </param>
	public CosmosDbOutboxChangeFeedProcessor(
		IOptions<CosmosDbOutboxOptions> outboxOptions,
		IOptions<CosmosDbChangeFeedOptions> feedOptions,
		ILogger<CosmosDbOutboxChangeFeedProcessor> logger,
		Func<IReadOnlyCollection<OutboundMessage>, CancellationToken, Task>? onChangesReceived = null)
	{
		ArgumentNullException.ThrowIfNull(outboxOptions);
		ArgumentNullException.ThrowIfNull(feedOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_outboxOptions = outboxOptions.Value;
		_feedOptions = feedOptions.Value;
		_logger = logger;
		_onChangesReceived = onChangesReceived;
	}

	/// <summary>
	/// Starts the change feed processor.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous start operation. </returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_running)
		{
			return;
		}

		LogChangeFeedStarting(_outboxOptions.ContainerName);

		_client = CreateClient();
		var database = _client.GetDatabase(_outboxOptions.DatabaseName);
		var monitoredContainer = database.GetContainer(_outboxOptions.ContainerName);

		if (_feedOptions.CreateLeaseContainerIfNotExists)
		{
			_ = await database.CreateContainerIfNotExistsAsync(
				_feedOptions.LeaseContainerName,
				"/id",
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		var leaseContainer = database.GetContainer(_feedOptions.LeaseContainerName);

		var builder = monitoredContainer
			.GetChangeFeedProcessorBuilder<CosmosDbOutboxDocument>(
				processorName: "outbox-change-feed",
				onChangesDelegate: HandleChangesAsync)
			.WithInstanceName(_feedOptions.InstanceName)
			.WithLeaseContainer(leaseContainer)
			.WithPollInterval(_feedOptions.FeedPollInterval)
			.WithMaxItems(_feedOptions.MaxItemsPerBatch)
			.WithErrorNotification(HandleErrorAsync);

		if (_feedOptions.StartFromBeginning)
		{
			builder = builder.WithStartTime(DateTime.MinValue.ToUniversalTime());
		}

		_processor = builder.Build();
		await _processor.StartAsync().ConfigureAwait(false);

		_running = true;
		LogChangeFeedStarted(_outboxOptions.ContainerName, _feedOptions.InstanceName);
	}

	/// <summary>
	/// Stops the change feed processor.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stop operation. </returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!_running || _processor == null)
		{
			return;
		}

		LogChangeFeedStopping(_outboxOptions.ContainerName);

		await _processor.StopAsync().ConfigureAwait(false);
		_running = false;

		LogChangeFeedStopped(_outboxOptions.ContainerName);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_running && _processor != null)
		{
			await _processor.StopAsync().ConfigureAwait(false);
		}

		_client?.Dispose();
		_running = false;
	}

	private async Task HandleChangesAsync(
		ChangeFeedProcessorContext context,
		IReadOnlyCollection<CosmosDbOutboxDocument> changes,
		CancellationToken cancellationToken)
	{
		LogChangeFeedBatchReceived(changes.Count, context.LeaseToken);

		if (_onChangesReceived == null)
		{
			return;
		}

		var messages = changes
			.Where(doc => doc.Status == (int)OutboxStatus.Staged)
			.Select(doc => doc.ToOutboundMessage())
			.ToList();

		if (messages.Count > 0)
		{
			await _onChangesReceived(messages, cancellationToken).ConfigureAwait(false);
		}
	}

	private Task HandleErrorAsync(string leaseToken, Exception exception)
	{
		LogChangeFeedError(leaseToken, exception);
		return Task.CompletedTask;
	}

	private CosmosClient CreateClient()
	{
		var clientOptions = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _outboxOptions.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_outboxOptions.MaxRetryWaitTimeInSeconds),
			ConnectionMode = _outboxOptions.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
		};

		if (_outboxOptions.HttpClientFactory != null)
		{
			clientOptions.HttpClientFactory = _outboxOptions.HttpClientFactory;
		}

		return !string.IsNullOrWhiteSpace(_outboxOptions.ConnectionString)
			? new CosmosClient(_outboxOptions.ConnectionString, clientOptions)
			: new CosmosClient(_outboxOptions.AccountEndpoint, _outboxOptions.AccountKey, clientOptions);
	}

	[LoggerMessage(102450, LogLevel.Information,
		"Starting Cosmos DB outbox change feed processor for container '{ContainerName}'")]
	private partial void LogChangeFeedStarting(string containerName);

	[LoggerMessage(102451, LogLevel.Information,
		"Cosmos DB outbox change feed processor started for container '{ContainerName}' with instance '{InstanceName}'")]
	private partial void LogChangeFeedStarted(string containerName, string instanceName);

	[LoggerMessage(102452, LogLevel.Information,
		"Stopping Cosmos DB outbox change feed processor for container '{ContainerName}'")]
	private partial void LogChangeFeedStopping(string containerName);

	[LoggerMessage(102453, LogLevel.Information,
		"Cosmos DB outbox change feed processor stopped for container '{ContainerName}'")]
	private partial void LogChangeFeedStopped(string containerName);

	[LoggerMessage(102454, LogLevel.Debug,
		"Received change feed batch with {Count} items from lease '{LeaseToken}'")]
	private partial void LogChangeFeedBatchReceived(int count, string leaseToken);

	[LoggerMessage(102455, LogLevel.Error,
		"Change feed error on lease '{LeaseToken}'")]
	private partial void LogChangeFeedError(string leaseToken, Exception ex);
}
