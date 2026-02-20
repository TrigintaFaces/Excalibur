// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.PoolingStubs;

/// <summary>Connection pool state enumeration.</summary>
public enum PoolState
{
	/// <summary>Pool is active and accepting connections.</summary>
	Active,

	/// <summary>Pool is draining connections.</summary>
	Draining,

	/// <summary>Pool is closed.</summary>
	Closed
}

/// <summary>Unified connection pool interface for integration tests.</summary>
/// <typeparam name="TConnection">The connection type.</typeparam>
public interface IUnifiedConnectionPool<TConnection> : IAsyncDisposable
	where TConnection : class
{
	/// <summary>Gets a connection from the pool.</summary>
	Task<PooledConnection<TConnection>> GetConnectionAsync(CancellationToken cancellationToken);

	/// <summary>Returns a connection to the pool.</summary>
	Task ReturnConnectionAsync(PooledConnection<TConnection> connection, CancellationToken cancellationToken);

	/// <summary>Gets the current pool state.</summary>
	PoolState State { get; }

	/// <summary>Gets the number of available connections.</summary>
	int AvailableCount { get; }

	/// <summary>Gets the number of active connections.</summary>
	int ActiveCount { get; }
}

/// <summary>Pooled connection wrapper for integration tests.</summary>
/// <typeparam name="TConnection">The connection type.</typeparam>
public class PooledConnection<TConnection> : IAsyncDisposable
	where TConnection : class
{
	/// <summary>Gets the underlying connection.</summary>
	public TConnection Connection { get; }

	/// <summary>Gets the connection ID.</summary>
	public string ConnectionId { get; }

	/// <summary>Gets when the connection was acquired.</summary>
	public DateTime AcquiredAt { get; }

	/// <summary>Gets or sets whether the connection is valid.</summary>
	public bool IsValid { get; set; } = true;

	/// <summary>Initializes a new instance.</summary>
	public PooledConnection(TConnection connection, string connectionId)
	{
		Connection = connection ?? throw new ArgumentNullException(nameof(connection));
		ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
		AcquiredAt = DateTime.UtcNow;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Connection pool options for configuration.</summary>
public class ConnectionPoolOptions
{
	/// <summary>Gets or sets the minimum pool size.</summary>
	public int MinPoolSize { get; set; } = 1;

	/// <summary>Gets or sets the maximum pool size.</summary>
	public int MaxPoolSize { get; set; } = 10;

	/// <summary>Gets or sets the connection timeout.</summary>
	public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>Gets or sets the idle timeout.</summary>
	public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Connection health checker interface.</summary>
/// <typeparam name="TConnection">The connection type.</typeparam>
public interface IConnectionHealthChecker<TConnection>
	where TConnection : class
{
	/// <summary>Checks if a connection is healthy.</summary>
	Task<bool> IsHealthyAsync(TConnection connection, CancellationToken cancellationToken);

	/// <summary>Gets the health check interval.</summary>
	TimeSpan CheckInterval { get; }
}

/// <summary>In-memory connection pool for testing.</summary>
/// <typeparam name="TConnection">The connection type.</typeparam>
public class UnifiedConnectionPool<TConnection> : IUnifiedConnectionPool<TConnection>
	where TConnection : class, new()
{
	private readonly Queue<PooledConnection<TConnection>> _pool = new();
	private int _connectionCounter;

	/// <inheritdoc/>
	public PoolState State { get; private set; } = PoolState.Active;

	/// <inheritdoc/>
	public int AvailableCount => _pool.Count;

	/// <inheritdoc/>
	public int ActiveCount { get; private set; }

	/// <inheritdoc/>
	public Task<PooledConnection<TConnection>> GetConnectionAsync(CancellationToken cancellationToken)
	{
		if (_pool.TryDequeue(out var pooled))
		{
			ActiveCount++;
			return Task.FromResult(pooled);
		}

		var connection = new TConnection();
		var id = $"conn-{Interlocked.Increment(ref _connectionCounter)}";
		ActiveCount++;
		return Task.FromResult(new PooledConnection<TConnection>(connection, id));
	}

	/// <inheritdoc/>
	public Task ReturnConnectionAsync(PooledConnection<TConnection> connection, CancellationToken cancellationToken)
	{
		if (connection.IsValid && State == PoolState.Active)
		{
			_pool.Enqueue(connection);
		}

		ActiveCount--;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		State = PoolState.Closed;
		_pool.Clear();
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>In-memory connection health checker for testing.</summary>
/// <typeparam name="TConnection">The connection type.</typeparam>
public class InMemoryConnectionHealthChecker<TConnection> : IConnectionHealthChecker<TConnection>
	where TConnection : class
{
	/// <inheritdoc/>
	public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <inheritdoc/>
	public Task<bool> IsHealthyAsync(TConnection connection, CancellationToken cancellationToken)
		=> Task.FromResult(connection != null);
}
