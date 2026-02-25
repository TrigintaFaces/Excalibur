// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Processes Firestore CDC events using Realtime Listeners.
/// </summary>
/// <remarks>
/// <para>
/// This processor uses Firestore's push-based Realtime Listeners to capture
/// document changes. Position is tracked synthetically using document UpdateTime
/// and DocumentId for deterministic ordering.
/// </para>
/// <para>
/// For continuous processing, use <see cref="StartAsync"/>. For serverless
/// batch processing, use <see cref="ProcessBatchAsync"/>.
/// </para>
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "CDC processors inherently couple with many SDK and abstraction types.")]
public sealed partial class FirestoreCdcProcessor : IFirestoreCdcProcessor
{
	private readonly FirestoreDb _db;
	private readonly FirestoreCdcOptions _options;
	private readonly IFirestoreCdcStateStore _stateStore;
	private readonly ILogger<FirestoreCdcProcessor> _logger;
	private readonly Channel<FirestoreDataChangeEvent> _channel;
#if NET9_0_OR_GREATER

	private readonly Lock _positionLock = new();

#else

	private readonly object _positionLock = new();

#endif

	private FirestoreChangeListener? _listener;
	private FirestoreCdcPosition _currentPosition;
	private volatile bool _isRunning;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreCdcProcessor"/> class.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="options">The CDC options.</param>
	/// <param name="stateStore">The state store for position persistence.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreCdcProcessor(
		FirestoreDb db,
		IOptions<FirestoreCdcOptions> options,
		IFirestoreCdcStateStore stateStore,
		ILogger<FirestoreCdcProcessor> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(logger);

		var resolvedOptions = options.Value;
		resolvedOptions.Validate();

		_db = db;
		_options = resolvedOptions;
		_stateStore = stateStore;
		_logger = logger;
		_currentPosition = FirestoreCdcPosition.Beginning(resolvedOptions.CollectionPath);

		_channel = Channel.CreateBounded<FirestoreDataChangeEvent>(
			new BoundedChannelOptions(_options.ChannelCapacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = true,
			});
	}

	/// <inheritdoc/>
	public async Task StartAsync(
		Func<FirestoreDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		await InitializePositionAsync(cancellationToken).ConfigureAwait(false);

		LogStarting(_options.ProcessorName, _options.CollectionPath);

		_isRunning = true;

		// Start the listener
		StartListener();

		// Process events from the channel
		try
		{
			await ProcessEventsAsync(eventHandler, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await StopListenerAsync().ConfigureAwait(false);
			_isRunning = false;
		}
	}

	/// <inheritdoc/>
	public async Task<int> ProcessBatchAsync(
		Func<FirestoreDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(eventHandler);

		await InitializePositionAsync(cancellationToken).ConfigureAwait(false);

		// For batch processing, we perform a one-time query instead of using listeners.
		// Note: This can only detect Modified events reliably. Added/Removed events
		// may be missed since we're using a point-in-time query.

		var query = BuildQuery();
		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var processedCount = 0;

		foreach (var docSnapshot in snapshot.Documents)
		{
			if (processedCount >= _options.MaxBatchSize)
			{
				break;
			}

			var docUpdateTime = docSnapshot.UpdateTime?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow;
			var docId = docSnapshot.Id;

			// Skip already processed documents
			if (!_currentPosition.IsAfterPosition(docUpdateTime, docId))
			{
				continue;
			}

			var newPosition = _currentPosition.WithDocument(docUpdateTime, docId);

			var changeEvent = FirestoreDataChangeEvent.CreateModified(
				newPosition,
				_options.CollectionPath,
				docId,
				docSnapshot.ToDictionary(),
				DateTimeOffset.UtcNow,
				docUpdateTime,
				docSnapshot.CreateTime?.ToDateTimeOffset());

			LogProcessingChange(changeEvent.ChangeType.ToString(), docId);

			try
			{
				await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);

				lock (_positionLock)
				{
					_currentPosition = newPosition;
				}

				processedCount++;
			}
			catch (Exception ex)
			{
				LogProcessingError(_options.ProcessorName, docId, ex);
				throw;
			}
		}

		return processedCount;
	}

	/// <inheritdoc/>
	public Task<FirestoreCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_positionLock)
		{
			return Task.FromResult(_currentPosition);
		}
	}

	/// <inheritdoc/>
	public async Task ConfirmPositionAsync(
		FirestoreCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(position);

		LogConfirmingPosition(_options.ProcessorName);

		await _stateStore.SavePositionAsync(
			_options.ProcessorName,
			position,
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isRunning = false;

		await StopListenerAsync().ConfigureAwait(false);
		_channel.Writer.Complete();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isRunning = false;

		// Do not block on async StopAsync — callers should use DisposeAsync for proper cleanup.
		// Setting _isRunning = false and _disposed = true signals the listener to stop.
		_channel.Writer.Complete();
	}

	private async Task InitializePositionAsync(CancellationToken cancellationToken)
	{
		if (_options.StartPosition is not null)
		{
			_currentPosition = _options.StartPosition;
			return;
		}

		var savedPosition = await _stateStore.GetPositionAsync(
			_options.ProcessorName,
			cancellationToken).ConfigureAwait(false);

		if (savedPosition is not null)
		{
			LogResumingFromPosition(_options.ProcessorName);
			_currentPosition = savedPosition;
		}
		else
		{
			LogStartingFromBeginning(_options.ProcessorName);
			_currentPosition = FirestoreCdcPosition.Beginning(_options.CollectionPath);
		}
	}

	private void StartListener()
	{
		var query = BuildListenerQuery();

		_listener = query.Listen(snapshot =>
		{
			try
			{
				ProcessSnapshot(snapshot);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing Firestore snapshot for CDC processor {ProcessorName}", _options.ProcessorName);
			}
		});
	}

	private async Task StopListenerAsync()
	{
		LogStopping(_options.ProcessorName);

		if (_listener is not null)
		{
			await _listener.StopAsync().ConfigureAwait(false);
			_listener = null;
		}
	}

	private Query BuildListenerQuery()
	{
		Query query;

		if (_options.UseCollectionGroup)
		{
			query = _db.CollectionGroup(_options.CollectionPath);
		}
		else
		{
			query = _db.Collection(_options.CollectionPath);
		}

		// Order by update time for consistent position tracking
		query = query.OrderBy(FieldPath.DocumentId);

		return query;
	}

	private Query BuildQuery()
	{
		Query query;

		if (_options.UseCollectionGroup)
		{
			query = _db.CollectionGroup(_options.CollectionPath);
		}
		else
		{
			query = _db.Collection(_options.CollectionPath);
		}

		// For batch processing, we need to filter by UpdateTime if we have a position
		// Note: Firestore doesn't support filtering by UpdateTime directly in queries,
		// so we order by document ID and filter client-side using IsAfterPosition

		query = query.OrderBy(FieldPath.DocumentId);
		query = query.Limit(_options.MaxBatchSize * 2); // Fetch extra to account for filtering

		return query;
	}

	private void ProcessSnapshot(QuerySnapshot snapshot)
	{
		if (!_isRunning || _disposed)
		{
			return;
		}

		var changes = snapshot.Changes.ToList();
		if (changes.Count == 0)
		{
			return;
		}

		LogReceivedChanges(_options.ProcessorName, changes.Count);

		foreach (var change in changes)
		{
			var docSnapshot = change.Document;
			var docUpdateTime = docSnapshot.UpdateTime?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow;
			var docId = docSnapshot.Id;

			// Skip already processed documents
			if (!_currentPosition.IsAfterPosition(docUpdateTime, docId))
			{
				continue;
			}

			var newPosition = _currentPosition.WithDocument(docUpdateTime, docId);

			var changeType = change.ChangeType switch
			{
				DocumentChange.Type.Added => FirestoreDataChangeType.Added,
				DocumentChange.Type.Modified => FirestoreDataChangeType.Modified,
				DocumentChange.Type.Removed => FirestoreDataChangeType.Removed,
				_ => FirestoreDataChangeType.Modified,
			};

			FirestoreDataChangeEvent changeEvent;

			if (changeType == FirestoreDataChangeType.Removed)
			{
				changeEvent = FirestoreDataChangeEvent.CreateRemoved(
					newPosition,
					_options.CollectionPath,
					docId,
					DateTimeOffset.UtcNow);
			}
			else if (changeType == FirestoreDataChangeType.Added)
			{
				changeEvent = FirestoreDataChangeEvent.CreateAdded(
					newPosition,
					_options.CollectionPath,
					docId,
					docSnapshot.Exists ? docSnapshot.ToDictionary() : null,
					DateTimeOffset.UtcNow,
					docUpdateTime,
					docSnapshot.CreateTime?.ToDateTimeOffset());
			}
			else
			{
				changeEvent = FirestoreDataChangeEvent.CreateModified(
					newPosition,
					_options.CollectionPath,
					docId,
					docSnapshot.Exists ? docSnapshot.ToDictionary() : null,
					DateTimeOffset.UtcNow,
					docUpdateTime,
					docSnapshot.CreateTime?.ToDateTimeOffset());
			}

			if (!_channel.Writer.TryWrite(changeEvent))
			{
				LogEventDropped(_options.ProcessorName, docId);
				throw new InvalidOperationException(
					$"CDC event for document '{docId}' could not be written to the processing channel. " +
					"The channel is full — increase MaxBatchSize or process events faster.");
			}
		}
	}

	private async Task ProcessEventsAsync(
		Func<FirestoreDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && _isRunning)
		{
			try
			{
				var hasEvent = await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				if (!hasEvent)
				{
					break;
				}

				while (_channel.Reader.TryRead(out var changeEvent))
				{
					LogProcessingChange(changeEvent.ChangeType.ToString(), changeEvent.DocumentId);

					try
					{
						await eventHandler(changeEvent, cancellationToken).ConfigureAwait(false);

						// Update position
						lock (_positionLock)
						{
							_currentPosition = changeEvent.Position;
						}

						// Auto-confirm position after each successful event
						await ConfirmPositionAsync(changeEvent.Position, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						LogProcessingError(_options.ProcessorName, changeEvent.DocumentId, ex);
						throw;
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (ChannelClosedException)
			{
				break;
			}
		}
	}
}
