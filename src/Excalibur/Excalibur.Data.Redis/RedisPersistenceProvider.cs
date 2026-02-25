// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Net;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.Redis.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using StackExchange.Redis;

namespace Excalibur.Data.Redis;

/// <summary>
/// Redis implementation of the persistence provider.
/// </summary>
public sealed partial class RedisPersistenceProvider : IPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private readonly ConnectionMultiplexer _connection;
	private readonly RedisProviderOptions _options;
	private readonly ILogger<RedisPersistenceProvider> _logger;
	private readonly AsyncRetryPolicy _retryPolicy;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The Redis provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public RedisPersistenceProvider(
		IOptions<RedisProviderOptions> options,
		ILogger<RedisPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			throw new ArgumentException(
					Resources.RedisPersistenceProvider_ConnectionStringRequired,
					nameof(options));
		}

		var configOptions = ConfigurationOptions.Parse(_options.ConnectionString);
		configOptions.ConnectTimeout = _options.ConnectTimeout * 1000; // Convert to milliseconds
		configOptions.SyncTimeout = _options.SyncTimeout * 1000;
		configOptions.AsyncTimeout = _options.AsyncTimeout * 1000;
		configOptions.ConnectRetry = _options.ConnectRetry;
		configOptions.AbortOnConnectFail = _options.AbortOnConnectFail;
		configOptions.AllowAdmin = _options.AllowAdmin;

		if (_options.UseSsl)
		{
			configOptions.Ssl = true;
		}

		if (!string.IsNullOrWhiteSpace(_options.Password))
		{
			configOptions.Password = _options.Password;
		}

		_connection = ConnectionMultiplexer.Connect(configOptions);

		// Setup retry policy
		_retryPolicy = Policy
			.Handle<RedisException>()
			.Or<RedisTimeoutException>()
			.WaitAndRetryAsync(
				_options.RetryCount,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogRetryWarning(_logger, exception, retryCount, timeSpan.TotalMilliseconds));

		// Initialize data request retry policy
		RetryPolicy = new RedisRetryPolicy(_options.RetryCount, _logger);

		Name = _options.Name ?? "redis";
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string ConnectionString => _options.ConnectionString;

	/// <inheritdoc />
	public string ProviderType => "Redis";

	/// <inheritdoc />
	public bool IsAvailable => !_disposed && _connection is { IsConnected: true };

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

		LogExecutingRequest(_logger, request.GetType().Name);

		try
		{
			// For Redis, we pass the database as the connection
			var connection = (TConnection)GetDatabase();
			return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogExecutionFailed(_logger, ex, request.GetType().Name);
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

		LogExecutingRequestInTransaction(_logger, request.GetType().Name);

		try
		{
			var connection = (TConnection)GetDatabase();
			var result = await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);

			// Commit transaction if successful
			await transactionScope.CommitAsync(cancellationToken).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex)
		{
			// Rollback transaction on error
			await transactionScope.RollbackAsync(cancellationToken).ConfigureAwait(false);
			LogTransactionExecutionFailed(_logger, ex, request.GetType().Name);
			throw;
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return new RedisTransactionScope(this, isolationLevel, timeout);
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _retryPolicy.ExecuteAsync(async () =>
			{
				var database = GetDatabase();
				_ = await database.PingAsync().ConfigureAwait(false);
			}).ConfigureAwait(false);

			LogConnectionTestSuccessful(_logger);
			return true;
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(_logger, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["Provider"] = "Redis",
			["Name"] = Name,
			["DatabaseId"] = _options.DatabaseId,
			["UseSsl"] = _options.UseSsl,
			["ConnectTimeout"] = _options.ConnectTimeout,
			["SyncTimeout"] = _options.SyncTimeout,
			["AsyncTimeout"] = _options.AsyncTimeout,
			["IsConnected"] = _connection.IsConnected,
			["IsReadOnly"] = _options.IsReadOnly,
			["IsAvailable"] = IsAvailable,
		};

		try
		{
			var endpoints = _connection.GetEndPoints();
			metrics["Endpoints"] = string.Join(", ", endpoints.Select(static e => e.ToString()));

			if (endpoints.Length > 0)
			{
				var server = _connection.GetServer(endpoints[0]);
				if (server.IsConnected)
				{
					var info = await server.InfoAsync().ConfigureAwait(false);
					var serverSection = info?.FirstOrDefault(static g => string.Equals(g.Key, "Server", StringComparison.Ordinal));
					if (serverSection != null)
					{
						var versionEntry =
							serverSection.FirstOrDefault(static e => string.Equals(e.Key, "redis_version", StringComparison.Ordinal));
						if (versionEntry.Key != null)
						{
							metrics["ServerVersion"] = versionEntry.Value;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogMetadataRetrievalFailed(_logger, ex);
		}

		return metrics;
	}

	/// <inheritdoc />
	public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		LogInitializing(_logger, Name, _options.DatabaseId);

		// Redis doesn't require special initialization beyond connection setup Connection is already established in constructor
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var stats = new Dictionary<string, object>
				(StringComparer.Ordinal)
			{
				["ConnectTimeout"] = _options.ConnectTimeout,
				["SyncTimeout"] = _options.SyncTimeout,
				["AsyncTimeout"] = _options.AsyncTimeout,
				["ConnectRetry"] = _options.ConnectRetry,
				["AbortOnConnectFail"] = _options.AbortOnConnectFail,
				["IsConnected"] = _connection.IsConnected,
			};

			// Try to get more detailed connection info
			var endpoints = _connection.GetEndPoints();
			if (endpoints.Length > 0)
			{
				stats["EndpointCount"] = endpoints.Length;
				stats["Endpoints"] = string.Join(", ", endpoints.Select(static e => e.ToString()));
			}

			return Task.FromResult<IDictionary<string, object>?>(stats);
		}
		catch (Exception ex)
		{
			LogPoolStatsFailed(_logger, ex);
			return Task.FromResult<IDictionary<string, object>?>(null);
		}
	}

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

	/// <summary>
	/// Gets the Redis database instance.
	/// </summary>
	/// <param name="databaseId"> Optional database ID. If not specified, uses the configured default. </param>
	/// <returns> The Redis database. </returns>
	public IDatabase GetDatabase(int? databaseId = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _connection.GetDatabase(databaseId ?? _options.DatabaseId);
	}

	/// <summary>
	/// Gets the Redis subscriber for pub/sub operations.
	/// </summary>
	/// <returns> The Redis subscriber. </returns>
	public ISubscriber GetSubscriber()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _connection.GetSubscriber();
	}

	/// <summary>
	/// Gets a Redis server instance.
	/// </summary>
	/// <param name="endPoint"> The server endpoint. If not specified, uses the first available. </param>
	/// <returns> The Redis server. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	public IServer GetServer(EndPoint? endPoint = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (endPoint == null)
		{
			var endpoints = _connection.GetEndPoints();
			if (endpoints.Length == 0)
			{
				throw new InvalidOperationException(Resources.RedisPersistenceProvider_NoEndpointsAvailable);
			}

			endPoint = endpoints[0];
		}

		return _connection.GetServer(endPoint);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposing(_logger, Name);

		try
		{
			_connection?.Close();
			_connection?.Dispose();
		}
		catch (Exception ex)
		{
			LogDisposeError(_logger, ex);
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	[LoggerMessage(DataRedisEventId.ProviderRetryWarning, LogLevel.Warning, "Redis operation failed. Retry {RetryCount} after {TimeSpan}ms")]
	private static partial void LogRetryWarning(ILogger logger, Exception exception, int retryCount, double timeSpan);

	[LoggerMessage(DataRedisEventId.ExecutingRequest, LogLevel.Debug, "Executing Redis data request of type {RequestType}")]
	private static partial void LogExecutingRequest(ILogger logger, string requestType);

	[LoggerMessage(DataRedisEventId.ExecutionFailed, LogLevel.Error, "Failed to execute Redis data request of type {RequestType}")]
	private static partial void LogExecutionFailed(ILogger logger, Exception exception, string requestType);

	[LoggerMessage(DataRedisEventId.ExecutingRequestInTransaction, LogLevel.Debug, "Executing Redis data request of type {RequestType} in transaction")]
	private static partial void LogExecutingRequestInTransaction(ILogger logger, string requestType);

	[LoggerMessage(DataRedisEventId.TransactionExecutionFailed, LogLevel.Error, "Failed to execute Redis data request of type {RequestType} in transaction")]
	private static partial void LogTransactionExecutionFailed(ILogger logger, Exception exception, string requestType);

	[LoggerMessage(DataRedisEventId.ConnectionTestSuccessful, LogLevel.Information, "Redis connection test successful")]
	private static partial void LogConnectionTestSuccessful(ILogger logger);

	[LoggerMessage(DataRedisEventId.ConnectionTestFailed, LogLevel.Error, "Redis connection test failed")]
	private static partial void LogConnectionTestFailed(ILogger logger, Exception exception);

	[LoggerMessage(DataRedisEventId.MetadataRetrievalFailed, LogLevel.Warning, "Failed to retrieve Redis server metadata")]
	private static partial void LogMetadataRetrievalFailed(ILogger logger, Exception exception);

	[LoggerMessage(DataRedisEventId.Initializing, LogLevel.Information, "Initializing Redis persistence provider '{Name}' for database {DatabaseId}")]
	private static partial void LogInitializing(ILogger logger, string name, int databaseId);

	[LoggerMessage(DataRedisEventId.PoolStatsFailed, LogLevel.Warning, "Failed to retrieve Redis connection pool statistics")]
	private static partial void LogPoolStatsFailed(ILogger logger, Exception exception);

	[LoggerMessage(DataRedisEventId.Disposing, LogLevel.Debug, "Disposing Redis provider '{Name}'")]
	private static partial void LogDisposing(ILogger logger, string name);

	[LoggerMessage(DataRedisEventId.DisposeError, LogLevel.Warning, "Error disposing Redis connection")]
	private static partial void LogDisposeError(ILogger logger, Exception exception);
}
