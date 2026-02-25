// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// MongoDB CDC processor using Change Streams.
/// </summary>
public sealed partial class MongoDbCdcProcessor : IMongoDbCdcProcessor
{
	private readonly MongoDbCdcOptions _options;
	private readonly IMongoDbCdcStateStore _stateStore;
	private readonly ILogger<MongoDbCdcProcessor> _logger;
	private readonly IMongoClient _client;

	private MongoDbCdcPosition _currentPosition;
	private MongoDbCdcPosition _confirmedPosition;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcProcessor"/> class.
	/// </summary>
	/// <param name="client">The MongoDB client from DI.</param>
	/// <param name="options">The CDC options.</param>
	/// <param name="stateStore">The state store for position tracking.</param>
	/// <param name="logger">The logger.</param>
	public MongoDbCdcProcessor(
		IMongoClient client,
		IOptions<MongoDbCdcOptions> options,
		IMongoDbCdcStateStore stateStore,
		ILogger<MongoDbCdcProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();

		_client = client;
		_stateStore = stateStore;
		_logger = logger;
		_currentPosition = MongoDbCdcPosition.Start;
		_confirmedPosition = MongoDbCdcPosition.Start;
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		LogStarting(_options.ProcessorId);

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		LogResuming(_confirmedPosition.TokenString ?? "<start>");

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await ProcessChangesAsync(eventHandler, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStopping();
				throw;
			}
			catch (Exception ex)
			{
				LogError(ex);

				// Wait before reconnecting
				await Task.Delay(_options.ReconnectInterval, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	public async Task<int> ProcessBatchAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		// Load last confirmed position
		_confirmedPosition = await _stateStore
			.GetLastPositionAsync(_options.ProcessorId, cancellationToken)
			.ConfigureAwait(false);

		_currentPosition = _confirmedPosition;

		var count = 0;
		var changeStreamOptions = BuildChangeStreamOptions();

		using var cursor = await WatchAsync(changeStreamOptions, cancellationToken).ConfigureAwait(false);

		// Use a timeout for batch processing
		using var batchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		batchCts.CancelAfter(_options.MaxAwaitTime * 2);

		try
		{
			while (count < _options.BatchSize &&
				   await cursor.MoveNextAsync(batchCts.Token).ConfigureAwait(false))
			{
				foreach (var change in cursor.Current)
				{
					var changeEvent = ConvertToChangeEvent(change);
					if (changeEvent is not null && ShouldProcessChange(changeEvent))
					{
						await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);
						count++;
					}

					// Update position
					_currentPosition = new MongoDbCdcPosition(change.ResumeToken);

					if (count >= _options.BatchSize)
					{
						break;
					}
				}
			}
		}
		catch (OperationCanceledException) when (batchCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Batch timeout - normal behavior
		}

		// Save position if we processed anything
		if (count > 0)
		{
			await _stateStore
				.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
				.ConfigureAwait(false);

			_confirmedPosition = _currentPosition;
		}

		return count;
	}

	/// <inheritdoc/>
	public Task<MongoDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return Task.FromResult(_currentPosition);
	}

	/// <inheritdoc/>
	public async Task ConfirmPositionAsync(
		MongoDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _stateStore
			.SavePositionAsync(_options.ProcessorId, position, cancellationToken)
			.ConfigureAwait(false);

		_confirmedPosition = position;

		LogConfirmed(position.TokenString ?? "<unknown>");
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		if (_client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		return ValueTask.CompletedTask;
	}

	private static string NormalizeOperationType(string operationType)
	{
		// MongoDB operation types are lowercase in change streams
		// This mapping ensures user-provided values are correctly normalized
		return operationType.ToUpperInvariant() switch
		{
			"INSERT" => "insert",
			"UPDATE" => "update",
			"REPLACE" => "replace",
			"DELETE" => "delete",
			"DROP" => "drop",
			"DROPDATABASE" => "dropDatabase",
			"RENAME" => "rename",
			"INVALIDATE" => "invalidate",
			_ => operationType,
		};
	}

	private static MongoDbDataChangeEvent? ConvertToChangeEvent(ChangeStreamDocument<BsonDocument> change)
	{
		var position = new MongoDbCdcPosition(change.ResumeToken);
		var databaseName = change.DatabaseNamespace?.DatabaseName ?? string.Empty;
		var collectionName = change.CollectionNamespace?.CollectionName ?? string.Empty;
		var clusterTime = change.ClusterTime;
		var wallTime = change.WallTime;

		return change.OperationType switch
		{
			ChangeStreamOperationType.Insert => MongoDbDataChangeEvent.CreateInsert(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocument,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Update => MongoDbDataChangeEvent.CreateUpdate(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocument,
				change.FullDocumentBeforeChange,
				ConvertUpdateDescription(change.UpdateDescription),
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Replace => MongoDbDataChangeEvent.CreateReplace(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocument,
				change.FullDocumentBeforeChange,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Delete => MongoDbDataChangeEvent.CreateDelete(
				position,
				databaseName,
				collectionName,
				change.DocumentKey,
				change.FullDocumentBeforeChange,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Drop => MongoDbDataChangeEvent.CreateDrop(
				position,
				databaseName,
				collectionName,
				clusterTime,
				wallTime),

			ChangeStreamOperationType.Invalidate => MongoDbDataChangeEvent.CreateInvalidate(
				position,
				clusterTime,
				wallTime),

			_ => null,
		};
	}

	private static MongoDbUpdateDescription? ConvertUpdateDescription(ChangeStreamUpdateDescription? description)
	{
		if (description is null)
		{
			return null;
		}

		return new MongoDbUpdateDescription
		{
			UpdatedFields = description.UpdatedFields,
			RemovedFields = description.RemovedFields?.ToList() ?? [],
			TruncatedArrays = ConvertTruncatedArrays(description.TruncatedArrays),
		};
	}

	private static IReadOnlyList<MongoDbArrayTruncation> ConvertTruncatedArrays(BsonArray? truncatedArrays)
	{
		if (truncatedArrays is null || truncatedArrays.Count == 0)
		{
			return [];
		}

		var result = new List<MongoDbArrayTruncation>();

		foreach (var item in truncatedArrays)
		{
			if (item is BsonDocument doc)
			{
				result.Add(new MongoDbArrayTruncation
				{
					Field = doc.GetValue("field", string.Empty).AsString,
					NewSize = doc.GetValue("newSize", 0).AsInt32,
				});
			}
		}

		return result;
	}

	private ChangeStreamOptions BuildChangeStreamOptions()
	{
		var options = new ChangeStreamOptions { BatchSize = _options.BatchSize, MaxAwaitTime = _options.MaxAwaitTime, };

		if (_confirmedPosition.IsValid)
		{
			options.ResumeAfter = _confirmedPosition.ResumeToken;
		}

		if (_options.FullDocument)
		{
			options.FullDocument = ChangeStreamFullDocumentOption.UpdateLookup;
		}

		if (_options.FullDocumentBeforeChange)
		{
			options.FullDocumentBeforeChange = ChangeStreamFullDocumentBeforeChangeOption.WhenAvailable;
		}

		return options;
	}

	private async Task<IAsyncCursor<ChangeStreamDocument<BsonDocument>>> WatchAsync(
		ChangeStreamOptions options,
		CancellationToken cancellationToken)
	{
		// Build pipeline for filtering operation types
		var pipeline = BuildPipeline();

		// Watch at appropriate level based on configuration
		if (!string.IsNullOrEmpty(_options.DatabaseName))
		{
			var database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CollectionNames.Length == 1)
			{
				// Watch single collection
				var collection = database.GetCollection<BsonDocument>(_options.CollectionNames[0]);
				return await collection
					.WatchAsync(pipeline, options, cancellationToken)
					.ConfigureAwait(false);
			}

			// Watch entire database
			return await database
				.WatchAsync(pipeline, options, cancellationToken)
				.ConfigureAwait(false);
		}

		// Watch entire cluster
		return await _client
			.WatchAsync(pipeline, options, cancellationToken)
			.ConfigureAwait(false);
	}

	private PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>? BuildPipeline()
	{
		var stages = new List<IPipelineStageDefinition>();

		// Filter by operation types if specified
		if (_options.OperationTypes.Length > 0)
		{
			// MongoDB change stream operation types are lowercase (insert, update, replace, delete, etc.)
			var operationTypes = new BsonArray(_options.OperationTypes.Select(NormalizeOperationType));
			var matchStage = PipelineStageDefinitionBuilder.Match(
				Builders<ChangeStreamDocument<BsonDocument>>.Filter.In("operationType", operationTypes));
			stages.Add(matchStage);
		}

		// Filter by collection names if watching a database
		if (!string.IsNullOrEmpty(_options.DatabaseName) && _options.CollectionNames.Length > 1)
		{
			var collectionNames = new BsonArray(_options.CollectionNames);
			var matchStage = PipelineStageDefinitionBuilder.Match(
				Builders<ChangeStreamDocument<BsonDocument>>.Filter.In("ns.coll", collectionNames));
			stages.Add(matchStage);
		}

		if (stages.Count == 0)
		{
			return null;
		}

		return PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>
			.Create(stages);
	}

	private async Task ProcessChangesAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		var options = BuildChangeStreamOptions();

		using var cursor = await WatchAsync(options, cancellationToken).ConfigureAwait(false);

		LogConnected();

		while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var change in cursor.Current)
			{
				var changeEvent = ConvertToChangeEvent(change);

				if (changeEvent is not null)
				{
					// Handle invalidation
					if (changeEvent.ChangeType == MongoDbDataChangeType.Invalidate)
					{
						LogInvalidate();

						// Save position and break to restart the stream
						await _stateStore
							.SavePositionAsync(_options.ProcessorId, _currentPosition, cancellationToken)
							.ConfigureAwait(false);

						return;
					}

					if (ShouldProcessChange(changeEvent))
					{
						await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);

						LogProcessed(changeEvent.ChangeType, changeEvent.FullNamespace);
					}
				}

				// Update position
				_currentPosition = new MongoDbCdcPosition(change.ResumeToken);
			}
		}
	}

	private bool ShouldProcessChange(MongoDbDataChangeEvent changeEvent)
	{
		// If no collections configured, process all
		if (_options.CollectionNames.Length == 0)
		{
			return true;
		}

		// For invalidate events, always process
		if (changeEvent.ChangeType == MongoDbDataChangeType.Invalidate)
		{
			return true;
		}

		return _options.CollectionNames.Any(c =>
			c.Equals(changeEvent.CollectionName, StringComparison.OrdinalIgnoreCase) ||
			c.Equals(changeEvent.FullNamespace, StringComparison.OrdinalIgnoreCase));
	}

	[LoggerMessage(DataMongoDbEventId.CdcStarting, LogLevel.Information, "Starting MongoDB CDC processor '{ProcessorId}'")]
	private partial void LogStarting(string processorId);

	[LoggerMessage(DataMongoDbEventId.CdcResumingFromToken, LogLevel.Information, "Resuming from token {Token}")]
	private partial void LogResuming(string token);

	[LoggerMessage(DataMongoDbEventId.CdcStreamWatching, LogLevel.Information, "Connected to MongoDB change stream")]
	private partial void LogConnected();

	[LoggerMessage(DataMongoDbEventId.CdcEventProcessed, LogLevel.Debug, "Processed {ChangeType} on {Namespace}")]
	private partial void LogProcessed(MongoDbDataChangeType changeType, string @namespace);

	[LoggerMessage(DataMongoDbEventId.CdcPositionConfirmed, LogLevel.Debug, "Confirmed position {Token}")]
	private partial void LogConfirmed(string token);

	[LoggerMessage(DataMongoDbEventId.CdcStreamInvalidated, LogLevel.Warning, "Received invalidate event, restarting change stream")]
	private partial void LogInvalidate();

	[LoggerMessage(DataMongoDbEventId.CdcStopping, LogLevel.Information, "Stopping MongoDB CDC processor")]
	private partial void LogStopping();

	[LoggerMessage(DataMongoDbEventId.CdcProcessingError, LogLevel.Error, "Error in MongoDB CDC processor")]
	private partial void LogError(Exception ex);
}
