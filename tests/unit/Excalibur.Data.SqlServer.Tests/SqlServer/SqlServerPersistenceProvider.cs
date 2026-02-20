// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

using Microsoft.Extensions.Options;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
///     SQL Server provider options for testing
/// </summary>
public class SqlServerProviderOptions
{
	public string ConnectionString { get; set; } = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true;";

	public int CommandTimeout { get; set; } = 30;

	public int MaxRetryCount { get; set; } = 3;

	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

	public bool EnablePooling { get; set; } = true;

	public int MinPoolSize { get; set; }

	public int MaxPoolSize { get; set; } = 100;

	public bool EnableMars { get; set; }

	public string ApplicationName { get; set; } = "TestApp";
}

/// <summary>
/// Test stub for retry policy.
/// </summary>
public class TestDataRequestRetryPolicy : IDataRequestRetryPolicy
{
	public int MaxRetryAttempts => 3;
	public TimeSpan BaseRetryDelay => TimeSpan.FromSeconds(1);

	public Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(default(TResult)!);

	public Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(default(TResult)!);

	public bool ShouldRetry(Exception exception) => exception is TimeoutException;
}

/// <summary>
/// Test stub for transaction scope.
/// </summary>
public sealed class TestTransactionScope : ITransactionScope
{
	private bool _disposed;

	public string TransactionId { get; } = Guid.NewGuid().ToString();
	public IsolationLevel IsolationLevel { get; } = IsolationLevel.ReadCommitted;
	public TransactionStatus Status { get; private set; } = TransactionStatus.Active;
	public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;
	public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);

	public Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task CommitAsync(CancellationToken cancellationToken = default)
	{ Status = TransactionStatus.Committed; return Task.CompletedTask; }

	public Task RollbackAsync(CancellationToken cancellationToken = default)
	{ Status = TransactionStatus.RolledBack; return Task.CompletedTask; }

	public Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public void OnCommit(Func<Task> callback)
	{ }

	public void OnRollback(Func<Task> callback)
	{ }

	public void OnComplete(Func<TransactionStatus, Task> callback)
	{ }

	public IEnumerable<IPersistenceProvider> GetEnlistedProviders() => [];

	public ITransactionScope CreateNestedScope(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) => new TestTransactionScope();

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}
}

/// <summary>
///     Test stub for SQL Server persistence provider
/// </summary>
public class SqlServerPersistenceProvider : IPersistenceProvider
{
	private readonly SqlServerProviderOptions _options;
	private readonly ILogger<SqlServerPersistenceProvider> _logger;
	private bool _disposed;

	public SqlServerPersistenceProvider(IOptions<SqlServerProviderOptions> options, ILogger<SqlServerPersistenceProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
		_logger = logger;
		RetryPolicy = new TestDataRequestRetryPolicy();
	}

	/// <inheritdoc/>
	public string Name => "SqlServer";

	/// <inheritdoc/>
	public string ProviderType => "SQL";

	/// <inheritdoc/>
	public bool IsAvailable => true;

	/// <inheritdoc/>
	public string ConnectionString => _options.ConnectionString;

	/// <inheritdoc/>
	public IDataRequestRetryPolicy RetryPolicy { get; }

	/// <inheritdoc/>
	public Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken = default)
		where TConnection : IDisposable
	{
		_logger.LogDebug("Executing request in SQL Server");
		return Task.FromResult(default(TResult)!);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken = default)
		where TConnection : IDisposable
	{
		_logger.LogDebug("Executing request in transaction in SQL Server");
		return Task.FromResult(default(TResult)!);
	}

	/// <inheritdoc/>
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		_logger.LogDebug("Creating transaction scope in SQL Server");
		return new TestTransactionScope();
	}

	/// <inheritdoc/>
	public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Testing connection to SQL Server");
		return Task.FromResult(true);
	}

	/// <inheritdoc/>
	public Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting metrics from SQL Server");
		return Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());
	}

	/// <inheritdoc/>
	public Task InitializeAsync(
		IPersistenceOptions options,
		CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Initializing SQL Server provider");
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting connection pool stats from SQL Server");
		return Task.FromResult<IDictionary<string, object>?>(new Dictionary<string, object>());
	}

	public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
	{
		_logger.LogDebug("Getting {Key} from SQL Server", key);
		return Task.FromResult<T?>(default);
	}

	public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
		where T : class
	{
		_logger.LogDebug("Setting {Key} in SQL Server", key);
		return Task.FromResult(true);
	}

	public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Deleting {Key} from SQL Server", key);
		return Task.FromResult(true);
	}

	public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Checking if {Key} exists in SQL Server", key);
		return Task.FromResult(false);
	}

	public Task<IEnumerable<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting keys matching {Pattern} from SQL Server", pattern);
		return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
	}

	public Task<bool> LockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Locking {Key} in SQL Server", key);
		return Task.FromResult(true);
	}

	public Task<bool> UnlockAsync(string key, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Unlocking {Key} in SQL Server", key);
		return Task.FromResult(true);
	}

	public Task FlushAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Flushing SQL Server");
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				// Dispose managed resources
				_logger.LogDebug("Disposing SQL Server provider");
			}

			// Dispose unmanaged resources if any
			_disposed = true;
		}
	}
}
