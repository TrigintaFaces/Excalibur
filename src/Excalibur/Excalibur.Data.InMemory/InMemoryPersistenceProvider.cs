// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#nullable disable warnings

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.InMemory.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory;

/// <summary>
/// In-memory implementation of the persistence provider for testing purposes.
/// </summary>
public sealed partial class InMemoryPersistenceProvider : IPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private readonly InMemoryProviderOptions _options;
	private readonly ILogger<InMemoryPersistenceProvider> _logger;
	private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _collections;
	private readonly SemaphoreSlim _transactionLock;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The in-memory provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public InMemoryPersistenceProvider(
		IOptions<InMemoryProviderOptions> options,
		ILogger<InMemoryPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_collections = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>(StringComparer.Ordinal);
		_transactionLock = new SemaphoreSlim(1, 1);

		Name = _options.Name ?? "inmemory";
		ConnectionString = $"InMemory:{Name}";
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string ConnectionString { get; }

	/// <inheritdoc />
	public string ProviderType => "InMemory";

	/// <inheritdoc />
	public bool IsReadOnly => _options.IsReadOnly;

	/// <inheritdoc />
	public bool IsAvailable => !_disposed;

	/// <inheritdoc />
	public IDataRequestRetryPolicy RetryPolicy { get; } = new NullRetryPolicy();

	/// <inheritdoc />
	public IDbConnection CreateConnection()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return new InMemoryConnection(this);
	}

	/// <inheritdoc />
	// R0.8: Remove unused parameter - public API contract requires cancellationToken even though in-memory operations are synchronous
#pragma warning disable IDE0060

	public ValueTask<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
#pragma warning restore IDE0060
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		// R0.8: Dispose objects before losing scope - Connection ownership transferred to caller who is responsible for disposal
#pragma warning disable CA2000
		return ValueTask.FromResult<IDbConnection>(new InMemoryConnection(this));
#pragma warning restore CA2000
	}

	/// <inheritdoc />
	public IDbTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-221-2: Use timeout to prevent thread pool starvation from indefinite blocking
		if (!_transactionLock.Wait(TimeSpan.FromSeconds(30)))
		{
			throw new TimeoutException("Failed to acquire transaction lock within 30 seconds.");
		}

		return new InMemoryTransaction(this, _transactionLock, isolationLevel);
	}

	/// <inheritdoc />
	public async ValueTask<IDbTransaction> BeginTransactionAsync(
		IsolationLevel isolationLevel,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		return new InMemoryTransaction(this, _transactionLock, isolationLevel);
	}

	/// <inheritdoc />
	public Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			LogConnectionTestSuccessful(_logger, Name);
			return Task.FromResult(true);
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(_logger, ex, Name);
			return Task.FromResult(false);
		}
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ArgumentNullException.ThrowIfNull(request);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDataRequest(_logger, request.GetType().Name);

		// For in-memory provider, we directly execute without actual connection The request should handle the in-memory data operations
		try
		{
			// Create a dummy connection for the request R0.8: Dispose objects before losing scope - Wrapped in using statement, disposal is guaranteed
#pragma warning disable CA2000
			using var connection = (TConnection)(object)new InMemoryConnection(this);
#pragma warning restore CA2000
			return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogExecuteDataRequestFailed(_logger, ex, request.GetType().Name);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(transactionScope);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDataRequestInTransaction(_logger, request.GetType().Name);

		await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// R0.8: Dispose objects before losing scope - Wrapped in using statement, disposal is guaranteed
#pragma warning disable CA2000
			using var connection = (TConnection)(object)new InMemoryConnection(this);
#pragma warning restore CA2000
			var result = await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);

			// Commit transaction if successful
			await transactionScope.CommitAsync(cancellationToken).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex)
		{
			// Rollback transaction on error
			await transactionScope.RollbackAsync(cancellationToken).ConfigureAwait(false);
			LogExecuteDataRequestInTransactionFailed(_logger, ex, request.GetType().Name);
			throw;
		}
		finally
		{
			_ = _transactionLock.Release();
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return new InMemoryTransactionScope(this, isolationLevel, timeout);
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["Provider"] = "InMemory",
			["Name"] = Name,
			["Collections"] = _collections.Count,
			["TotalItems"] = _collections.Values.Sum(static c => c.Count),
			["IsReadOnly"] = IsReadOnly,
			["MaxItemsPerCollection"] = _options.MaxItemsPerCollection,
			["IsAvailable"] = IsAvailable,
		};

		return Task.FromResult<IDictionary<string, object>>(metrics);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		LogInitializingProvider(_logger, Name);

		// In-memory provider doesn't need special initialization but we can load from disk if PersistToDisk is enabled
		if (_options.PersistToDisk && !string.IsNullOrEmpty(_options.PersistenceFilePath) && File.Exists(_options.PersistenceFilePath))
		{
			LogLoadingPersistedData(_logger, _options.PersistenceFilePath);
			await LoadFromDiskAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken) =>

		// In-memory provider doesn't have a connection pool
		Task.FromResult<IDictionary<string, object>?>(null);

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		if (serviceType == typeof(IPersistenceProviderTransaction))
		{
			return this;
		}

		return null;
	}

	/// <inheritdoc />
	public IDictionary<string, object> GetMetadata() =>
		new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["Provider"] = "InMemory",
			["Name"] = Name,
			["Collections"] = _collections.Count,
			["TotalItems"] = _collections.Values.Sum(static c => c.Count),
			["MaxItemsPerCollection"] = _options.MaxItemsPerCollection,
			["PersistToDisk"] = _options.PersistToDisk,
			["IsReadOnly"] = _options.IsReadOnly,
		};

	/// <summary>
	/// Gets or creates a collection.
	/// </summary>
	/// <param name="collectionName"> The collection name. </param>
	/// <returns> The collection dictionary. </returns>
	public ConcurrentDictionary<string, object> GetCollection(string collectionName)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _collections.GetOrAdd(collectionName, static _ => new ConcurrentDictionary<string, object>(StringComparer.Ordinal));
	}

	/// <summary>
	/// Stores an item in a collection.
	/// </summary>
	/// <typeparam name="T"> The item type. </typeparam>
	/// <param name="collectionName"> The collection name. </param>
	/// <param name="key"> The item key. </param>
	/// <param name="item"> The item to store. </param>
	/// <exception cref="InvalidOperationException"> </exception>
	public void Store<T>(string collectionName, string key, T item)
		where T : notnull
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (IsReadOnly)
		{
			throw new InvalidOperationException("Cannot write to a read-only provider.");
		}

		var collection = GetCollection(collectionName);

		if (_options.MaxItemsPerCollection > 0 && collection.Count >= _options.MaxItemsPerCollection)
		{
			throw new InvalidOperationException(
				$"Collection '{collectionName}' has reached maximum capacity of {_options.MaxItemsPerCollection} items.");
		}

		collection[key] = item;
		LogStoredItem(_logger, key, collectionName);
	}

	/// <summary>
	/// Retrieves an item from a collection.
	/// </summary>
	/// <typeparam name="T"> The item type. </typeparam>
	/// <param name="collectionName"> The collection name. </param>
	/// <param name="key"> The item key. </param>
	/// <returns> The item if found; otherwise, default. </returns>
	public T? Retrieve<T>(string collectionName, string key)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var collection = GetCollection(collectionName);
		if (collection.TryGetValue(key, out var item) && item is T typedItem)
		{
			LogRetrievedItem(_logger, key, collectionName);
			return typedItem;
		}

		LogItemNotFound(_logger, key, collectionName);
		return default;
	}

	/// <summary>
	/// Removes an item from a collection.
	/// </summary>
	/// <param name="collectionName"> The collection name. </param>
	/// <param name="key"> The item key. </param>
	/// <returns> True if the item was removed; otherwise, false. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	public bool Remove(string collectionName, string key)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (IsReadOnly)
		{
			throw new InvalidOperationException("Cannot delete from a read-only provider.");
		}

		var collection = GetCollection(collectionName);
		var removed = collection.TryRemove(key, out _);

		if (removed)
		{
			LogRemovedItem(_logger, key, collectionName);
		}

		return removed;
	}

	/// <summary>
	/// Clears all data from the provider.
	/// </summary>
	/// <exception cref="InvalidOperationException"> </exception>
	public void Clear()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (IsReadOnly)
		{
			throw new InvalidOperationException("Cannot clear a read-only provider.");
		}

		_collections.Clear();
		LogClearedAllData(_logger, Name);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposingProvider(_logger, Name);

		if (_options.PersistToDisk && !string.IsNullOrWhiteSpace(_options.PersistenceFilePath))
		{
			// Use fire-and-forget pattern to avoid blocking in Dispose
			_ = Task.Factory.StartNew(
					async () =>
					{
						try
						{
							await PersistToDiskAsync(CancellationToken.None).ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							LogFailedToPersistOnDispose(_logger, ex, Name);
						}
					},
					CancellationToken.None,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap();
		}

		_collections.Clear();
		_transactionLock?.Dispose();
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposingProvider(_logger, Name);

		if (_options.PersistToDisk && !string.IsNullOrWhiteSpace(_options.PersistenceFilePath))
		{
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await PersistToDiskAsync(cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				LogFailedToPersistOnAsyncDispose(_logger, new TimeoutException("Persist to disk timed out after 30 seconds"), Name);
			}
			catch (Exception ex)
			{
				LogFailedToPersistOnAsyncDispose(_logger, ex, Name);
			}
		}

		_collections.Clear();
		_transactionLock?.Dispose();
	}

	/// <summary>
	/// Loads data from disk into memory collections.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private async Task LoadFromDiskAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(_options.PersistenceFilePath))
		{
			return;
		}

		try
		{
			LogLoadingDataFromDisk(_logger, _options.PersistenceFilePath);

			// Use semaphore to ensure thread safety during loading
			await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var jsonContent = await File.ReadAllTextAsync(_options.PersistenceFilePath, cancellationToken).ConfigureAwait(false);

				if (string.IsNullOrWhiteSpace(jsonContent))
				{
					LogPersistenceFileEmpty(_logger, _options.PersistenceFilePath);
					return;
				}

				var persistenceData = JsonSerializer.Deserialize<PersistenceData>(jsonContent);
				if (persistenceData?.Collections == null)
				{
					LogInvalidPersistenceDataFormat(_logger, _options.PersistenceFilePath);
					return;
				}

				// Clear existing collections and load from disk
				_collections.Clear();
				foreach (var collection in persistenceData.Collections)
				{
					var collectionDict = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
					foreach (var item in collection.Value)
					{
						// Store as JsonElement to preserve type information for later retrieval
						collectionDict[item.Key] = item.Value;
					}

					_collections[collection.Key] = collectionDict;
				}

				var loadedCollections = persistenceData.Collections.Count;
				var totalItems = persistenceData.Collections.Values.Sum(static c => c.Count);

				LogSuccessfullyLoadedData(_logger, loadedCollections, totalItems, _options.PersistenceFilePath,
					persistenceData.Metadata?.Version, persistenceData.Metadata?.Timestamp);
			}
			finally
			{
				_ = _transactionLock.Release();
			}
		}
		catch (JsonException ex)
		{
			LogFailedToDeserializePersistenceData(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"Failed to load persistence data: {ex.Message}", ex);
		}
		catch (FileNotFoundException)
		{
			LogPersistenceFileNotFound(_logger, _options.PersistenceFilePath);
		}
		catch (UnauthorizedAccessException ex)
		{
			LogAccessDeniedReadingPersistenceFile(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"Cannot access persistence file: {ex.Message}", ex);
		}
		catch (IOException ex)
		{
			LogIOErrorReadingPersistenceFile(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"I/O error reading persistence file: {ex.Message}", ex);
		}
		catch (Exception ex)
		{
			LogUnexpectedErrorLoadingData(_logger, ex, _options.PersistenceFilePath);
			throw;
		}
	}

	/// <summary>
	/// Persists current memory collections to disk.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private async Task PersistToDiskAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(_options.PersistenceFilePath))
		{
			return;
		}

		try
		{
			LogPersistingDataToDisk(_logger, _options.PersistenceFilePath);

			// Use semaphore to ensure thread safety during persistence
			await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// Create directory if it doesn't exist
				var directory = Path.GetDirectoryName(_options.PersistenceFilePath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					_ = Directory.CreateDirectory(directory);
					LogCreatedDirectoryForPersistenceFile(_logger, directory);
				}

				// Create persistence data structure
				var collectionsSnapshot = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
				foreach (var collection in _collections)
				{
					var collectionCopy = new Dictionary<string, object>(StringComparer.Ordinal);
					foreach (var item in collection.Value)
					{
						collectionCopy[item.Key] = item.Value;
					}

					collectionsSnapshot[collection.Key] = collectionCopy;
				}

				var persistenceData = new PersistenceData
				{
					Collections = collectionsSnapshot,
					Metadata = new PersistenceMetadata
					{
						Version = "1.0",
						Timestamp = DateTimeOffset.UtcNow.ToString("O"), // ISO 8601 format
						Provider = "InMemory",
					},
				};

				// Serialize to JSON with proper options
				var jsonContent = JsonSerializer.Serialize(persistenceData, JsonOptions);

				// Write to temporary file first, then rename to ensure atomicity
				var tempFilePath = _options.PersistenceFilePath + ".tmp";
				await File.WriteAllTextAsync(tempFilePath, jsonContent, cancellationToken).ConfigureAwait(false);

				// Atomic rename operation
				if (File.Exists(_options.PersistenceFilePath))
				{
					File.Delete(_options.PersistenceFilePath);
				}

				File.Move(tempFilePath, _options.PersistenceFilePath);

				var collectionCount = collectionsSnapshot.Count;
				var totalItems = collectionsSnapshot.Values.Sum(static c => c.Count);

				LogSuccessfullyPersistedData(_logger, collectionCount, totalItems, _options.PersistenceFilePath);
			}
			finally
			{
				_ = _transactionLock.Release();
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			LogAccessDeniedWritingPersistenceFile(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"Cannot write to persistence file: {ex.Message}", ex);
		}
		catch (DirectoryNotFoundException ex)
		{
			LogDirectoryNotFoundForPersistenceFile(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"Directory not found for persistence file: {ex.Message}", ex);
		}
		catch (IOException ex)
		{
			LogIOErrorWritingPersistenceFile(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"I/O error writing persistence file: {ex.Message}", ex);
		}
		catch (JsonException ex)
		{
			LogFailedToSerializeData(_logger, ex, _options.PersistenceFilePath);
			throw new InvalidOperationException($"Failed to serialize persistence data: {ex.Message}", ex);
		}
		catch (Exception ex)
		{
			LogUnexpectedErrorPersistingData(_logger, ex, _options.PersistenceFilePath);
			throw;
		}
	}

	#region LoggerMessage Methods

	[LoggerMessage(DataInMemoryEventId.ConnectionTestSuccessful, LogLevel.Information, "In-memory connection test successful for provider '{Name}'")]
	private static partial void LogConnectionTestSuccessful(ILogger logger, string name);

	[LoggerMessage(DataInMemoryEventId.ConnectionTestFailed, LogLevel.Error, "In-memory connection test failed for provider '{Name}'")]
	private static partial void LogConnectionTestFailed(ILogger logger, Exception ex, string name);

	[LoggerMessage(DataInMemoryEventId.ExecutingDataRequest, LogLevel.Debug, "Executing data request of type {RequestType}")]
	private static partial void LogExecutingDataRequest(ILogger logger, string requestType);

	[LoggerMessage(DataInMemoryEventId.ExecuteDataRequestFailed, LogLevel.Error, "Failed to execute data request of type {RequestType}")]
	private static partial void LogExecuteDataRequestFailed(ILogger logger, Exception ex, string requestType);

	[LoggerMessage(DataInMemoryEventId.ExecutingDataRequestInTransaction, LogLevel.Debug, "Executing data request of type {RequestType} in transaction")]
	private static partial void LogExecutingDataRequestInTransaction(ILogger logger, string requestType);

	[LoggerMessage(DataInMemoryEventId.ExecuteDataRequestInTransactionFailed, LogLevel.Error, "Failed to execute data request of type {RequestType} in transaction")]
	private static partial void LogExecuteDataRequestInTransactionFailed(ILogger logger, Exception ex, string requestType);

	[LoggerMessage(DataInMemoryEventId.InitializingProvider, LogLevel.Information, "Initializing in-memory persistence provider '{Name}'")]
	private static partial void LogInitializingProvider(ILogger logger, string name);

	[LoggerMessage(DataInMemoryEventId.LoadingPersistedData, LogLevel.Information, "Loading persisted data from {FilePath}")]
	private static partial void LogLoadingPersistedData(ILogger logger, string filePath);

	[LoggerMessage(DataInMemoryEventId.StoredItem, LogLevel.Debug, "Stored item with key '{Key}' in collection '{Collection}'")]
	private static partial void LogStoredItem(ILogger logger, string key, string collection);

	[LoggerMessage(DataInMemoryEventId.RetrievedItem, LogLevel.Debug, "Retrieved item with key '{Key}' from collection '{Collection}'")]
	private static partial void LogRetrievedItem(ILogger logger, string key, string collection);

	[LoggerMessage(DataInMemoryEventId.ItemNotFound, LogLevel.Debug, "Item with key '{Key}' not found in collection '{Collection}'")]
	private static partial void LogItemNotFound(ILogger logger, string key, string collection);

	[LoggerMessage(DataInMemoryEventId.RemovedItem, LogLevel.Debug, "Removed item with key '{Key}' from collection '{Collection}'")]
	private static partial void LogRemovedItem(ILogger logger, string key, string collection);

	[LoggerMessage(DataInMemoryEventId.ClearedAllData, LogLevel.Information, "Cleared all data from in-memory provider '{Name}'")]
	private static partial void LogClearedAllData(ILogger logger, string name);

	[LoggerMessage(DataInMemoryEventId.DisposingProvider, LogLevel.Debug, "Disposing in-memory provider '{Name}'")]
	private static partial void LogDisposingProvider(ILogger logger, string name);

	[LoggerMessage(DataInMemoryEventId.FailedToPersistOnDispose, LogLevel.Error, "Failed to persist data to disk during dispose for provider '{Name}'")]
	private static partial void LogFailedToPersistOnDispose(ILogger logger, Exception ex, string name);

	[LoggerMessage(DataInMemoryEventId.FailedToPersistOnAsyncDispose, LogLevel.Error, "Failed to persist data during async disposal for provider '{Name}'")]
	private static partial void LogFailedToPersistOnAsyncDispose(ILogger logger, Exception ex, string name);

	[LoggerMessage(DataInMemoryEventId.LoadingDataFromDisk, LogLevel.Debug, "Loading data from disk at '{FilePath}'")]
	private static partial void LogLoadingDataFromDisk(ILogger logger, string filePath);

	[LoggerMessage(DataInMemoryEventId.PersistenceFileEmpty, LogLevel.Warning, "Persistence file at '{FilePath}' is empty")]
	private static partial void LogPersistenceFileEmpty(ILogger logger, string filePath);

	[LoggerMessage(DataInMemoryEventId.InvalidPersistenceDataFormat, LogLevel.Warning, "Invalid persistence data format in '{FilePath}'")]
	private static partial void LogInvalidPersistenceDataFormat(ILogger logger, string filePath);

	[LoggerMessage(DataInMemoryEventId.SuccessfullyLoadedData, LogLevel.Information, "Successfully loaded {CollectionCount} collections with {TotalItems} total items from '{FilePath}'. Metadata: Version={Version}, Timestamp={Timestamp}")]
	private static partial void LogSuccessfullyLoadedData(ILogger logger, int collectionCount, int totalItems, string filePath, string? version, string? timestamp);

	[LoggerMessage(DataInMemoryEventId.FailedToDeserializePersistenceData, LogLevel.Error, "Failed to deserialize persistence data from '{FilePath}'. JSON format may be invalid")]
	private static partial void LogFailedToDeserializePersistenceData(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.PersistenceFileNotFound, LogLevel.Warning, "Persistence file not found at '{FilePath}', starting with empty collections")]
	private static partial void LogPersistenceFileNotFound(ILogger logger, string filePath);

	[LoggerMessage(DataInMemoryEventId.AccessDeniedReadingPersistenceFile, LogLevel.Error, "Access denied when reading persistence file at '{FilePath}'")]
	private static partial void LogAccessDeniedReadingPersistenceFile(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.IOErrorReadingPersistenceFile, LogLevel.Error, "I/O error when reading persistence file at '{FilePath}'")]
	private static partial void LogIOErrorReadingPersistenceFile(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.UnexpectedErrorLoadingData, LogLevel.Error, "Unexpected error when loading data from '{FilePath}'")]
	private static partial void LogUnexpectedErrorLoadingData(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.PersistingDataToDisk, LogLevel.Debug, "Persisting data to disk at '{FilePath}'")]
	private static partial void LogPersistingDataToDisk(ILogger logger, string filePath);

	[LoggerMessage(DataInMemoryEventId.CreatedDirectoryForPersistenceFile, LogLevel.Debug, "Created directory '{Directory}' for persistence file")]
	private static partial void LogCreatedDirectoryForPersistenceFile(ILogger logger, string directory);

	[LoggerMessage(DataInMemoryEventId.SuccessfullyPersistedData, LogLevel.Information, "Successfully persisted {CollectionCount} collections with {TotalItems} total items to '{FilePath}'")]
	private static partial void LogSuccessfullyPersistedData(ILogger logger, int collectionCount, int totalItems, string filePath);

	[LoggerMessage(DataInMemoryEventId.AccessDeniedWritingPersistenceFile, LogLevel.Error, "Access denied when writing persistence file to '{FilePath}'")]
	private static partial void LogAccessDeniedWritingPersistenceFile(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.DirectoryNotFoundForPersistenceFile, LogLevel.Error, "Directory not found for persistence file path '{FilePath}'")]
	private static partial void LogDirectoryNotFoundForPersistenceFile(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.IOErrorWritingPersistenceFile, LogLevel.Error, "I/O error when writing persistence file to '{FilePath}'")]
	private static partial void LogIOErrorWritingPersistenceFile(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.FailedToSerializeData, LogLevel.Error, "Failed to serialize data for persistence to '{FilePath}'")]
	private static partial void LogFailedToSerializeData(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.UnexpectedErrorPersistingData, LogLevel.Error, "Unexpected error when persisting data to '{FilePath}'")]
	private static partial void LogUnexpectedErrorPersistingData(ILogger logger, Exception ex, string filePath);

	[LoggerMessage(DataInMemoryEventId.TransactionCommitted, LogLevel.Debug, "In-memory transaction committed")]
	private static partial void LogTransactionCommitted(ILogger logger);

	[LoggerMessage(DataInMemoryEventId.TransactionRolledBack, LogLevel.Debug, "In-memory transaction rolled back")]
	private static partial void LogTransactionRolledBack(ILogger logger);

	[LoggerMessage(DataInMemoryEventId.TransactionDisposedWithoutCommit, LogLevel.Debug, "In-memory transaction disposed without commit")]
	private static partial void LogTransactionDisposedWithoutCommit(ILogger logger);

	#endregion LoggerMessage Methods

	/// <summary>
	/// In-memory connection implementation.
	/// </summary>
	private sealed class InMemoryConnection(InMemoryPersistenceProvider provider) : IDbConnection
	{
		[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
			Justification =
				"Provider is a dependency injected object that is not owned by this connection. The provider creates and manages connections, not the inverse. Disposing the provider from a connection would violate object lifetime semantics.")]
		private readonly InMemoryPersistenceProvider _provider = provider;

		private string _connectionString = provider.ConnectionString;

		public string ConnectionString
		{
			get => _connectionString;
			set => _connectionString = value ?? string.Empty;
		}

		public int ConnectionTimeout => 0;

		public string Database => _provider.Name;

		public ConnectionState State { get; private set; } = ConnectionState.Closed;

		public IDbTransaction BeginTransaction() => _provider.BeginTransaction();

		public IDbTransaction BeginTransaction(IsolationLevel il) => _provider.BeginTransaction(il);

		public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

		public void Close() => State = ConnectionState.Closed;

		public IDbCommand CreateCommand() => throw new NotSupportedException("Use provider methods directly");

		public void Dispose() => Close();

		public void Open() => State = ConnectionState.Open;
	}

	/// <summary>
	/// In-memory transaction implementation.
	/// </summary>
	private sealed class InMemoryTransaction(InMemoryPersistenceProvider provider, SemaphoreSlim lockObject, IsolationLevel isolationLevel)
		: IDbTransaction
	{
		private bool _committed;
		private volatile bool _disposed;

		public IDbConnection? Connection { get; private set; } = new InMemoryConnection(provider);

		public IsolationLevel IsolationLevel { get; } = isolationLevel;

		public void Commit()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(InMemoryTransaction));
			}

			_committed = true;
			LogTransactionCommitted(provider._logger);
		}

		public void Rollback()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(InMemoryTransaction));
			}

			LogTransactionRolledBack(provider._logger);
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			Connection = null;
			_ = lockObject.Release();

			if (!_committed)
			{
				LogTransactionDisposedWithoutCommit(provider._logger);
			}
		}
	}
}
