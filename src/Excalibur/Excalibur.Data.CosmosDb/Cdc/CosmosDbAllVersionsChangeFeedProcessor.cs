// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Data.CosmosDb.Diagnostics;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Processes CosmosDb Change Feed in AllVersionsAndDeletes mode,
/// capturing insert, update, and delete operations with before/after state.
/// </summary>
/// <remarks>
/// <para>
/// This processor uses the Azure Cosmos DB SDK AllVersionsAndDeletes change feed mode
/// (full fidelity), which provides:
/// </para>
/// <list type="bullet">
/// <item><description>Insert events with the new document</description></item>
/// <item><description>Update events with both previous and current document state</description></item>
/// <item><description>Delete events with the deleted document's previous state</description></item>
/// </list>
/// <para>
/// The container must be configured with a full fidelity retention window:
/// <code>
/// container.ChangeFeedPolicy = new ChangeFeedPolicy
/// {
///     FullFidelityRetention = TimeSpan.FromMinutes(10)
/// };
/// </code>
/// </para>
/// </remarks>
public sealed partial class CosmosDbAllVersionsChangeFeedProcessor : IAsyncDisposable
{
	private readonly CosmosClient _client;
	private readonly CosmosDbCdcOptions _cdcOptions;
	private readonly CosmosDbAllVersionsChangeFeedOptions _options;
	private readonly ILogger<CosmosDbAllVersionsChangeFeedProcessor> _logger;

	private ChangeFeedProcessor? _changeFeedProcessor;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbAllVersionsChangeFeedProcessor"/> class.
	/// </summary>
	/// <param name="client">The CosmosDB client from DI.</param>
	/// <param name="cdcOptions">The base CDC options for database/container configuration.</param>
	/// <param name="options">The AllVersionsAndDeletes change feed options.</param>
	/// <param name="logger">The logger.</param>
	public CosmosDbAllVersionsChangeFeedProcessor(
		CosmosClient client,
		IOptions<CosmosDbCdcOptions> cdcOptions,
		IOptions<CosmosDbAllVersionsChangeFeedOptions> options,
		ILogger<CosmosDbAllVersionsChangeFeedProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(cdcOptions);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_cdcOptions = cdcOptions.Value;
		_options = options.Value;
		_logger = logger;
	}

	/// <summary>
	/// Starts the AllVersionsAndDeletes change feed processor.
	/// </summary>
	/// <param name="eventHandler">The handler invoked for each change event.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous start operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="eventHandler"/> is null.
	/// </exception>
	public async Task StartAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);
		cancellationToken.ThrowIfCancellationRequested();
		ObjectDisposedException.ThrowIf(_disposed, this);

		var database = _client.GetDatabase(_cdcOptions.DatabaseId);
		var monitoredContainer = database.GetContainer(_cdcOptions.ContainerId);
		var leaseContainer = database.GetContainer(_options.LeaseContainer);

		var builder = monitoredContainer
			.GetChangeFeedProcessorBuilder<JsonDocument>(
				_options.ProcessorName,
				(changes, ct) => HandleChangesAsync(changes, eventHandler, ct))
			.WithInstanceName(Environment.MachineName)
			.WithLeaseContainer(leaseContainer)
			.WithPollInterval(_options.FeedPollInterval)
			.WithMaxItems(_options.MaxBatchSize);

		if (_options.StartTime.HasValue)
		{
			builder = builder.WithStartTime(_options.StartTime.Value.UtcDateTime);
		}

		_changeFeedProcessor = builder.Build();

		LogStartingAllVersionsProcessor(_options.ProcessorName, _cdcOptions.DatabaseId, _cdcOptions.ContainerId);

		await _changeFeedProcessor.StartAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Stops the AllVersionsAndDeletes change feed processor.
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous stop operation.</returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (_changeFeedProcessor is not null)
		{
			LogStoppingAllVersionsProcessor(_options.ProcessorName);
			await _changeFeedProcessor.StopAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_changeFeedProcessor is not null)
		{
			try
			{
				await _changeFeedProcessor.StopAsync().ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// Processor was never started - safe to ignore
			}
		}
	}

	private async Task HandleChangesAsync(
		IReadOnlyCollection<JsonDocument> changes,
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		LogBatchReceived(_options.ProcessorName, changes.Count);

		foreach (var document in changes)
		{
			var changeEvent = ParseAllVersionsChangeEvent(document);
			await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
		}
	}

	private CosmosDbDataChangeEvent ParseAllVersionsChangeEvent(JsonDocument document)
	{
		var root = document.RootElement;

		// AllVersionsAndDeletes mode provides:
		// - "current": the document after the change (null for deletes)
		// - "previous": the document before the change (null for inserts)
		// - "metadata": operation metadata including "operationType"

		JsonDocument? currentDoc = null;
		JsonDocument? previousDoc = null;

		if (root.TryGetProperty("current", out var currentElement) &&
			currentElement.ValueKind != JsonValueKind.Null)
		{
			currentDoc = JsonDocument.Parse(currentElement.GetRawText());
		}

		if (root.TryGetProperty("previous", out var previousElement) &&
			previousElement.ValueKind != JsonValueKind.Null)
		{
			previousDoc = JsonDocument.Parse(previousElement.GetRawText());
		}

		// Determine the change type from metadata
		var changeType = CosmosDbDataChangeType.Update;
		if (root.TryGetProperty("metadata", out var metadata) &&
			metadata.TryGetProperty("operationType", out var opType))
		{
			changeType = opType.GetString()?.ToUpperInvariant() switch
			{
				"CREATE" => CosmosDbDataChangeType.Insert,
				"REPLACE" => CosmosDbDataChangeType.Update,
				"DELETE" => CosmosDbDataChangeType.Delete,
				_ => CosmosDbDataChangeType.Update,
			};
		}
		else
		{
			// Infer from current/previous presence
			if (currentDoc is not null && previousDoc is null)
			{
				changeType = CosmosDbDataChangeType.Insert;
			}
			else if (currentDoc is null && previousDoc is not null)
			{
				changeType = CosmosDbDataChangeType.Delete;
			}
		}

		// Extract document ID from current or previous
		var sourceElement = currentDoc?.RootElement ?? previousDoc?.RootElement;
		var documentId = string.Empty;
		string? partitionKey = null;

		if (sourceElement.HasValue)
		{
			if (sourceElement.Value.TryGetProperty("id", out var idProp))
			{
				documentId = idProp.GetString() ?? string.Empty;
			}

			if (!string.IsNullOrEmpty(_cdcOptions.PartitionKeyPath))
			{
				var pkPath = _cdcOptions.PartitionKeyPath.TrimStart('/');
				if (sourceElement.Value.TryGetProperty(pkPath, out var pkProp))
				{
					partitionKey = pkProp.GetString();
				}
			}
		}

		// Extract timestamp
		var timestamp = DateTimeOffset.UtcNow;
		if (root.TryGetProperty("metadata", out var meta2) &&
			meta2.TryGetProperty("timeStamp", out var tsProp))
		{
			if (tsProp.TryGetDateTimeOffset(out var parsed))
			{
				timestamp = parsed;
			}
		}
		else if (sourceElement.HasValue &&
				 sourceElement.Value.TryGetProperty("_ts", out var tsUnix))
		{
			timestamp = DateTimeOffset.FromUnixTimeSeconds(tsUnix.GetInt64());
		}

		// Extract LSN
		long lsn = 0;
		if (root.TryGetProperty("metadata", out var meta3) &&
			meta3.TryGetProperty("lsn", out var lsnProp))
		{
			lsn = lsnProp.GetInt64();
		}

		var position = CosmosDbCdcPosition.Now();

		return changeType switch
		{
			CosmosDbDataChangeType.Insert => CosmosDbDataChangeEvent.CreateInsert(
				position, documentId, partitionKey, currentDoc!, timestamp, lsn, null),
			CosmosDbDataChangeType.Delete => CosmosDbDataChangeEvent.CreateDelete(
				position, documentId, partitionKey, previousDoc, timestamp, lsn),
			_ => CosmosDbDataChangeEvent.CreateUpdate(
				position, documentId, partitionKey, currentDoc!, previousDoc, timestamp, lsn, null),
		};
	}

	[LoggerMessage(DataCosmosDbEventId.ChangeFeedProcessorStarted, LogLevel.Information,
		"Starting AllVersionsAndDeletes CDC processor '{ProcessorName}' on {DatabaseId}/{ContainerId}")]
	private partial void LogStartingAllVersionsProcessor(string processorName, string databaseId, string containerId);

	[LoggerMessage(DataCosmosDbEventId.ChangeFeedProcessorStopped, LogLevel.Information,
		"Stopping AllVersionsAndDeletes CDC processor '{ProcessorName}'")]
	private partial void LogStoppingAllVersionsProcessor(string processorName);

	[LoggerMessage(DataCosmosDbEventId.ChangeFeedItemsProcessed, LogLevel.Debug,
		"AllVersionsAndDeletes processor '{ProcessorName}' received batch of {Count} changes")]
	private partial void LogBatchReceived(string processorName, int count);
}
