// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.MongoDB.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// MongoDB implementation of the persistence provider.
/// </summary>
public sealed partial class MongoDbPersistenceProvider : IDocumentPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private readonly IMongoClient? _client;
	private readonly IMongoDatabase? _database;
	private readonly MongoDbProviderOptions _options;
	private readonly ILogger<MongoDbPersistenceProvider> _logger;
	private readonly AsyncRetryPolicy _retryPolicy;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbPersistenceProvider" /> class for testing.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	public MongoDbPersistenceProvider(ILogger<MongoDbPersistenceProvider> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = new MongoDbProviderOptions { ConnectionString = "mongodb://localhost:27017", DatabaseName = "test", Name = "MongoDB" };

		// Initialize with default values for testing
		_retryPolicy = Policy
			.Handle<MongoException>()
			.WaitAndRetryAsync(
				3,
				static retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

		RetryPolicy = new MongoDbRetryPolicy(3, _logger);
		Name = "MongoDB";
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The MongoDB provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public MongoDbPersistenceProvider(
		IOptions<MongoDbProviderOptions> options,
		ILogger<MongoDbPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			throw new ArgumentException("Connection string required.", nameof(options));
		}

		if (string.IsNullOrWhiteSpace(_options.DatabaseName))
		{
			throw new ArgumentException("Database name required.", nameof(options));
		}

		var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
		settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeout);
		settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeout);
		settings.MaxConnectionPoolSize = _options.MaxPoolSize;
		settings.MinConnectionPoolSize = _options.MinPoolSize;

		if (_options.UseSsl)
		{
			settings.UseTls = true;
		}

		_client = new MongoClient(settings);
		_database = _client.GetDatabase(_options.DatabaseName);

		// Setup retry policy
		_retryPolicy = Policy
			.Handle<MongoException>()
			.WaitAndRetryAsync(
				_options.RetryCount,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) => LogMongoOperationRetry(
					retryCount,
					timeSpan.TotalMilliseconds,
					exception));

		// Initialize data request retry policy
		RetryPolicy = new MongoDbRetryPolicy(_options.RetryCount, _logger);

		Name = _options.Name ?? "mongodb";
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string ConnectionString => _options.ConnectionString;

	/// <inheritdoc />
	public string ProviderType => "Document";

	/// <inheritdoc />
	public string DocumentStoreType => "MongoDB";

	/// <inheritdoc />
	public bool IsAvailable => !_disposed && _client != null;

	/// <inheritdoc />
	public IDataRequestRetryPolicy RetryPolicy { get; }

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ArgumentNullException.ThrowIfNull(request);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDataRequest(request.GetType().Name);

		try
		{
			if (_database == null)
			{
				throw new InvalidOperationException("MongoDB database not initialized.");
			}

			// For MongoDB, we pass the database as the connection
			var connection = (TConnection)_database;
			return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogFailedToExecuteDataRequest(request.GetType().Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Executes a MongoDB-specific document data request within a transaction scope with automatic commit/rollback handling.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the MongoDB database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The MongoDB document data request to execute. </param>
	/// <param name="transactionScope"> The transaction scope to use. Must be a <see cref="MongoDbTransactionScope" />. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the result of the document request execution. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="request" /> or <paramref name="transactionScope" /> is null.
	/// </exception>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="transactionScope" /> is not a <see cref="MongoDbTransactionScope" />. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the MongoDB database is not initialized. </exception>
	/// <exception cref="ObjectDisposedException"> Thrown when this provider has been disposed. </exception>
	// R0.8: Remove unused parameter - parameter is part of public API
#pragma warning disable IDE0060

	public async Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(transactionScope);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDocumentDataRequestInTransaction(request.GetType().Name);

		if (transactionScope is not MongoDbTransactionScope mongoTransactionScope)
		{
			throw new ArgumentException(
				"Transaction scope must be MongoDbTransactionScope.",
				nameof(transactionScope));
		}

		try
		{
			var connection = (TConnection)mongoTransactionScope.Database;
			var result = await request.ResolveAsync(connection).ConfigureAwait(false);

			LogSuccessfullyExecutedDocumentDataRequestInTransaction(request.GetType().Name);
			return result;
		}
		catch (Exception ex)
		{
			LogFailedToExecuteDocumentDataRequestInTransaction(request.GetType().Name, ex);
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

		LogExecutingDataRequestInTransaction(request.GetType().Name);

		try
		{
			if (_database == null)
			{
				throw new InvalidOperationException("MongoDB database not initialized.");
			}

			var connection = (TConnection)_database;
			var result = await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);

			// Commit transaction if successful
			await transactionScope.CommitAsync(cancellationToken).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex)
		{
			// Rollback transaction on error
			await transactionScope.RollbackAsync(cancellationToken).ConfigureAwait(false);
			LogFailedToExecuteDataRequestInTransaction(request.GetType().Name, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return new MongoDbTransactionScope(this, isolationLevel, timeout);
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _retryPolicy.ExecuteAsync(
				async ct =>
				{
					if (_database == null)
					{
						throw new InvalidOperationException("MongoDB database not initialized.");
					}

					_ = await _database.RunCommandAsync<object>( /*lang=json*/ "{ ping: 1 }", cancellationToken: ct).ConfigureAwait(false);
				}, cancellationToken).ConfigureAwait(false);

			LogConnectionTestSuccessful(_options.DatabaseName);
			return true;
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(_options.DatabaseName, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "MongoDB",
			["Name"] = Name,
			["DatabaseName"] = _options.DatabaseName,
			["MaxPoolSize"] = _options.MaxPoolSize,
			["MinPoolSize"] = _options.MinPoolSize,
			["UseSsl"] = _options.UseSsl,
			["ServerSelectionTimeout"] = _options.ServerSelectionTimeout,
			["IsReadOnly"] = _options.IsReadOnly,
			["IsAvailable"] = IsAvailable,
		};

		try
		{
			var servers = _client?.Cluster.Description.Servers;
			var serverDescription = servers is { Count: > 0 } ? servers[0] : null;
			if (serverDescription != null)
			{
				// Use WireVersionRange instead of obsolete Version property
				metrics["ServerVersion"] = serverDescription.WireVersionRange != null
					? $"{serverDescription.WireVersionRange.Min}-{serverDescription.WireVersionRange.Max}"
					: "Unknown";
				metrics["ServerType"] = serverDescription.Type.ToString();
				metrics["State"] = serverDescription.State.ToString();
			}
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveServerMetadata(ex);
		}

		return Task.FromResult<IDictionary<string, object>>(metrics);
	}

	/// <inheritdoc />
	public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		LogInitializingProvider(Name, _options.DatabaseName);

		// MongoDB doesn't require special initialization beyond connection setup But we could perform any database-specific setup here if needed
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var stats = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["MaxPoolSize"] = _options.MaxPoolSize,
				["MinPoolSize"] = _options.MinPoolSize,
				["ServerSelectionTimeout"] = _options.ServerSelectionTimeout,
				["ConnectTimeout"] = _options.ConnectTimeout,
				["ActiveConnections"] = 0, // MongoDB driver doesn't expose this directly
				["AvailableConnections"] = 0, // MongoDB driver doesn't expose this directly
				["TotalConnections"] = 0, // MongoDB driver doesn't expose this directly
			};

			return Task.FromResult<IDictionary<string, object>?>(stats);
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveConnectionPoolStats(ex);
			return Task.FromResult<IDictionary<string, object>?>(null);
		}
	}

	/// <summary>
	/// Gets the MongoDB database instance.
	/// </summary>
	/// <returns> The MongoDB database. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	public IMongoDatabase GetDatabase()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_database == null)
		{
			throw new InvalidOperationException("MongoDB database not initialized.");
		}

		return _database;
	}

	/// <summary>
	/// Gets a MongoDB collection.
	/// </summary>
	/// <typeparam name="T"> The document type. </typeparam>
	/// <param name="collectionName"> The collection name. </param>
	/// <returns> The MongoDB collection. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	public IMongoCollection<T> GetCollection<T>(string collectionName)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_database == null)
		{
			throw new InvalidOperationException("MongoDB database not initialized.");
		}

		return _database.GetCollection<T>(collectionName);
	}

	/// <summary>
	/// Begins a new MongoDB session.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A new MongoDB client session. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	public async Task<IClientSessionHandle> BeginSessionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_client == null)
		{
			throw new InvalidOperationException("MongoDB client not initialized.");
		}

		var session = await _client.StartSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

		if (_options.UseTransactions)
		{
			session.StartTransaction(new TransactionOptions(
				readConcern: ReadConcern.Snapshot,
				readPreference: ReadPreference.Primary,
				writeConcern: WriteConcern.WMajority));
		}

		return session;
	}

	/// <inheritdoc />
	public bool ValidateDocumentRequest<TConnection, TResult>(IDocumentDataRequest<TConnection, TResult> documentRequest)
	{
		if (documentRequest == null)
		{
			return false;
		}

		// Check if connection type is compatible with MongoDB
		if (typeof(TConnection) != typeof(IMongoDatabase))
		{
			return false;
		}

		// Check if collection name is provided
		if (string.IsNullOrWhiteSpace(documentRequest.CollectionName))
		{
			return false;
		}

		// Check if operation type is supported
		var supportedOps = GetSupportedOperationTypes();
		return supportedOps.Contains(documentRequest.OperationType, StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public IEnumerable<string> GetSupportedOperationTypes() =>
		new[]
		{
			"Insert", "Find", "Update", "UpdateOne", "UpdateMany", "Delete", "DeleteOne", "DeleteMany", "Aggregate", "BulkWrite",
			"CreateIndex", "DropIndex", "Count",
		};

	/// <inheritdoc />
	public async Task<TResult> ExecuteDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(documentRequest);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!IsAvailable || _database == null)
		{
			throw new InvalidOperationException("MongoDB provider not initialized.");
		}

		if (!ValidateDocumentRequest(documentRequest))
		{
			throw new ArgumentException("Invalid document request for MongoDB provider.", nameof(documentRequest));
		}

		LogExecutingDocumentRequest(documentRequest.OperationType, documentRequest.CollectionName);

		try
		{
			if (_database == null)
			{
				throw new InvalidOperationException("MongoDB database not initialized.");
			}

			var connection = (TConnection)_database;
			return await documentRequest.ResolveAsync(connection).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogFailedToExecuteDocumentRequest(documentRequest.OperationType, documentRequest.CollectionName, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteDocumentInTransactionAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(documentRequest);
		ArgumentNullException.ThrowIfNull(transactionScope);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (transactionScope is not MongoDbTransactionScope)
		{
			throw new ArgumentException("Transaction scope must be MongoDbTransactionScope.", nameof(transactionScope));
		}

		LogExecutingDocumentRequestInTransaction(documentRequest.OperationType, documentRequest.CollectionName);

		try
		{
			if (_database == null)
			{
				throw new InvalidOperationException("MongoDB database not initialized.");
			}

			var connection = (TConnection)_database;
			var result = await documentRequest.ResolveAsync(connection).ConfigureAwait(false);
			await transactionScope.CommitAsync(cancellationToken).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex)
		{
			await transactionScope.RollbackAsync(cancellationToken).ConfigureAwait(false);
			LogFailedToExecuteDocumentRequestInTransaction(documentRequest.OperationType, documentRequest.CollectionName, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteDocumentBatchAsync<TConnection>(
		IEnumerable<IDocumentDataRequest<TConnection, object>> documentRequests,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(documentRequests);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var requestList = documentRequests.ToList();
		if (requestList.Count == 0)
		{
			return [];
		}

		LogExecutingBatchOfDocumentRequests(requestList.Count);

		var results = new List<object>();
		foreach (var request in requestList)
		{
			var result = await ExecuteDocumentAsync(request, cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		return results;
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteBulkDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> bulkDocumentRequest,
		CancellationToken cancellationToken) =>

		// For MongoDB, bulk operations are handled the same as regular document operations The actual bulk logic should be in the request's
		// ResolveAsync method
		ExecuteDocumentAsync(bulkDocumentRequest, cancellationToken);

	/// <inheritdoc />
	public Task<TResult> ExecuteAggregationAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> aggregationRequest,
		CancellationToken cancellationToken) =>

		// For MongoDB, aggregation operations are handled the same as regular document operations The actual aggregation pipeline should be
		// in the request's ResolveAsync method
		ExecuteDocumentAsync(aggregationRequest, cancellationToken);

	/// <inheritdoc />
	public Task<string> ExecuteIndexOperationAsync<TConnection>(
		IDocumentDataRequest<TConnection, string> indexRequest,
		CancellationToken cancellationToken) =>

		// For MongoDB, index operations are handled the same as regular document operations The actual index logic should be in the
		// request's ResolveAsync method
		ExecuteDocumentAsync(indexRequest, cancellationToken);

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(CancellationToken cancellationToken)
	{
		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["provider"] = "MongoDB",
			["document_store_type"] = DocumentStoreType,
			["connection.available"] = IsAvailable,
		};

		if (!IsAvailable || _database == null)
		{
			return stats;
		}

		try
		{
			// Get database statistics
			var command = new BsonDocument("dbStats", 1);
			var result = await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);

			stats["database_name"] = _options.DatabaseName;
			stats["collections"] = result.GetValue("collections", 0).AsInt32;
			stats["objects"] = result.GetValue("objects", 0).AsInt64;
			stats["data_size"] = result.GetValue("dataSize", 0).AsInt64;
			stats["storage_size"] = result.GetValue("storageSize", 0).AsInt64;
			stats["indexes"] = result.GetValue("indexes", 0).AsInt32;
			stats["index_size"] = result.GetValue("indexSize", 0).AsInt64;
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveDatabaseStatistics(ex);
			stats["error"] = ex.Message;
		}

		return stats;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetCollectionInfoAsync(
		string collectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

		var info = new Dictionary<string, object>(StringComparer.Ordinal) { ["collection_name"] = collectionName, ["exists"] = false };

		if (!IsAvailable || _database == null)
		{
			return info;
		}

		try
		{
			// Check if collection exists
			var filter = new BsonDocument("name", collectionName);
			var collections = await _database.ListCollectionNamesAsync(
				new ListCollectionNamesOptions { Filter = filter },
				cancellationToken).ConfigureAwait(false);

			var collectionList = await collections.ToListAsync(cancellationToken).ConfigureAwait(false);
			info["exists"] = collectionList.Count != 0;

			if ((bool)info["exists"])
			{
				// Get collection statistics
				var command = new BsonDocument { { "collStats", collectionName } };
				var result = await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken)
					.ConfigureAwait(false);

				info["count"] = result.GetValue("count", 0).AsInt64;
				info["size"] = result.GetValue("size", 0).AsInt64;
				info["storage_size"] = result.GetValue("storageSize", 0).AsInt64;
				info["total_index_size"] = result.GetValue("totalIndexSize", 0).AsInt64;
				info["index_count"] = result.GetValue("nindexes", 0).AsInt32;
			}
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveCollectionInfo(collectionName, ex);
			info["error"] = ex.Message;
		}

		return info;
	}

	/// <summary>
	/// Gets an implementation-specific service. Returns <see langword="this"/> for
	/// <see cref="IPersistenceProviderHealth"/> and <see cref="IPersistenceProviderTransaction"/>.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
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
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// IMongoClient does not implement IDisposable; it manages connection pooling internally
		_disposed = true;
		LogDisposingProvider(Name);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	// Source-generated logging methods
	[LoggerMessage(DataMongoDbEventId.MongoOperationRetry, LogLevel.Warning,
		"MongoDB operation failed. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogMongoOperationRetry(int retryCount, double timeSpan, Exception? ex);

	[LoggerMessage(DataMongoDbEventId.ExecutingDataRequest, LogLevel.Debug,
		"Executing MongoDB data request of type {RequestType}")]
	private partial void LogExecutingDataRequest(string requestType);

	[LoggerMessage(DataMongoDbEventId.FailedToExecuteDataRequest, LogLevel.Error,
		"Failed to execute MongoDB data request of type {RequestType}")]
	private partial void LogFailedToExecuteDataRequest(string requestType, Exception ex);

	[LoggerMessage(DataMongoDbEventId.ExecutingDocumentDataRequestInTransaction, LogLevel.Debug,
		"Executing MongoDB document data request of type {RequestType} in transaction")]
	private partial void LogExecutingDocumentDataRequestInTransaction(string requestType);

	[LoggerMessage(DataMongoDbEventId.SuccessfullyExecutedDocumentDataRequestInTransaction, LogLevel.Debug,
		"Successfully executed MongoDB document data request of type {RequestType} in transaction")]
	private partial void LogSuccessfullyExecutedDocumentDataRequestInTransaction(string requestType);

	[LoggerMessage(DataMongoDbEventId.FailedToExecuteDocumentDataRequestInTransaction, LogLevel.Error,
		"Failed to execute MongoDB document data request of type {RequestType} in transaction")]
	private partial void LogFailedToExecuteDocumentDataRequestInTransaction(string requestType, Exception ex);

	[LoggerMessage(DataMongoDbEventId.ExecutingDataRequestInTransaction, LogLevel.Debug,
		"Executing MongoDB data request of type {RequestType} in transaction")]
	private partial void LogExecutingDataRequestInTransaction(string requestType);

	[LoggerMessage(DataMongoDbEventId.FailedToExecuteDataRequestInTransaction, LogLevel.Error,
		"Failed to execute MongoDB data request of type {RequestType} in transaction")]
	private partial void LogFailedToExecuteDataRequestInTransaction(string requestType, Exception ex);

	[LoggerMessage(DataMongoDbEventId.ConnectionTestSuccessful, LogLevel.Information,
		"MongoDB connection test successful for database '{Database}'")]
	private partial void LogConnectionTestSuccessful(string database);

	[LoggerMessage(DataMongoDbEventId.ConnectionTestFailed, LogLevel.Error,
		"MongoDB connection test failed for database '{Database}'")]
	private partial void LogConnectionTestFailed(string database, Exception ex);

	[LoggerMessage(DataMongoDbEventId.FailedToRetrieveServerMetadata, LogLevel.Warning,
		"Failed to retrieve MongoDB server metadata")]
	private partial void LogFailedToRetrieveServerMetadata(Exception ex);

	[LoggerMessage(DataMongoDbEventId.InitializingProvider, LogLevel.Information,
		"Initializing MongoDB persistence provider '{Name}' for database '{Database}'")]
	private partial void LogInitializingProvider(string name, string database);

	[LoggerMessage(DataMongoDbEventId.FailedToRetrieveConnectionPoolStats, LogLevel.Warning,
		"Failed to retrieve MongoDB connection pool statistics")]
	private partial void LogFailedToRetrieveConnectionPoolStats(Exception ex);

	[LoggerMessage(DataMongoDbEventId.ExecutingDocumentRequest, LogLevel.Debug,
		"Executing MongoDB document request: {OperationType} on {Collection}")]
	private partial void LogExecutingDocumentRequest(string operationType, string collection);

	[LoggerMessage(DataMongoDbEventId.FailedToExecuteDocumentRequest, LogLevel.Error,
		"Failed to execute MongoDB document request: {OperationType} on {Collection}")]
	private partial void LogFailedToExecuteDocumentRequest(string operationType, string collection, Exception ex);

	[LoggerMessage(DataMongoDbEventId.ExecutingDocumentRequestInTransaction, LogLevel.Debug,
		"Executing MongoDB document request in transaction: {OperationType} on {Collection}")]
	private partial void LogExecutingDocumentRequestInTransaction(string operationType, string collection);

	[LoggerMessage(DataMongoDbEventId.FailedToExecuteDocumentRequestInTransaction, LogLevel.Error,
		"Failed to execute MongoDB document request in transaction: {OperationType} on {Collection}")]
	private partial void LogFailedToExecuteDocumentRequestInTransaction(string operationType, string collection, Exception ex);

	[LoggerMessage(DataMongoDbEventId.ExecutingBatchOfDocumentRequests, LogLevel.Debug,
		"Executing batch of {Count} MongoDB document requests")]
	private partial void LogExecutingBatchOfDocumentRequests(int count);

	[LoggerMessage(DataMongoDbEventId.FailedToRetrieveDatabaseStatistics, LogLevel.Warning,
		"Failed to retrieve MongoDB database statistics")]
	private partial void LogFailedToRetrieveDatabaseStatistics(Exception ex);

	[LoggerMessage(DataMongoDbEventId.FailedToRetrieveCollectionInfo, LogLevel.Warning,
		"Failed to retrieve MongoDB collection info for {Collection}")]
	private partial void LogFailedToRetrieveCollectionInfo(string collection, Exception ex);

	[LoggerMessage(DataMongoDbEventId.DisposingProvider, LogLevel.Debug,
		"Disposing MongoDB provider '{Name}'")]
	private partial void LogDisposingProvider(string name);
}
