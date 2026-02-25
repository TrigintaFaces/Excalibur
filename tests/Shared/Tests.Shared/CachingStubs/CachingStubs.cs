// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.CachingStubs;

/// <summary>Generic distributed cache interface stub for integration tests.</summary>
/// <typeparam name="T">The cached value type.</typeparam>
public interface IDistributedCache<T>
{
	/// <summary>Gets a value from the cache.</summary>
	Task<T?> GetAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>Sets a value in the cache.</summary>
	Task SetAsync(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

	/// <summary>Removes a value from the cache.</summary>
	Task RemoveAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>Gets or sets a value using a factory.</summary>
	Task<T?> GetOrSetAsync(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}

/// <summary>In-memory implementation of IDistributedCache for testing.</summary>
/// <typeparam name="T">The cached value type.</typeparam>
public class InMemoryDistributedCache<T> : IDistributedCache<T>
{
	private readonly Dictionary<string, (T Value, DateTime? Expiry)> _cache = new();

	/// <inheritdoc/>
	public Task<T?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		if (_cache.TryGetValue(key, out var entry))
		{
			if (entry.Expiry == null || entry.Expiry > DateTime.UtcNow)
			{
				return Task.FromResult<T?>(entry.Value);
			}
			_ = _cache.Remove(key);
		}
		return Task.FromResult<T?>(default);
	}

	/// <inheritdoc/>
	public Task SetAsync(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
	{
		var expiry = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null;
		_cache[key] = (value, expiry);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
	{
		_ = _cache.Remove(key);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task<T?> GetOrSetAsync(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
	{
		var existing = await GetAsync(key, cancellationToken);
		if (existing != null)
		{
			return existing;
		}

		var value = await factory(cancellationToken);
		await SetAsync(key, value, expiration, cancellationToken);
		return value;
	}
}

/// <summary>Cache stampede metrics interface.</summary>
public interface ICacheStampedeMetrics
{
	/// <summary>Records a factory execution.</summary>
	void RecordFactoryExecution(string key, double durationMs);

	/// <summary>Records a stampede wait.</summary>
	void RecordStampedeWait(string key, double waitTimeMs);

	/// <summary>Records a timeout.</summary>
	void RecordTimeout(string key);

	/// <summary>Records a factory failure.</summary>
	void RecordFactoryFailure(string key, string exceptionType);

	/// <summary>Records active locks count.</summary>
	void RecordActiveLocks(int count);
}

/// <summary>Cache metrics interface.</summary>
public interface ICacheMetrics
{
	/// <summary>Gets the total hits.</summary>
	long TotalHits { get; }

	/// <summary>Gets the total misses.</summary>
	long TotalMisses { get; }

	/// <summary>Gets the hit ratio.</summary>
	double HitRatio { get; }

	/// <summary>Records a hit.</summary>
	void RecordHit();

	/// <summary>Records a miss.</summary>
	void RecordMiss();

	/// <summary>Gets metrics snapshot.</summary>
	CacheMetricsSnapshot GetSnapshot();
}

/// <summary>Cache metrics snapshot.</summary>
public class CacheMetricsSnapshot
{
	/// <summary>Gets or sets the total hits.</summary>
	public long TotalHits { get; set; }

	/// <summary>Gets or sets the total misses.</summary>
	public long TotalMisses { get; set; }

	/// <summary>Gets or sets the hit ratio.</summary>
	public double HitRatio { get; set; }

	/// <summary>Gets or sets the average latency.</summary>
	public TimeSpan AverageLatency { get; set; }

	/// <summary>Gets or sets the timestamp.</summary>
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>CQRS cache coordinator interface.</summary>
public interface ICqrsCacheCoordinator
{
	/// <summary>Invalidates cache for a command.</summary>
	Task InvalidateForCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default);

	/// <summary>Gets a cached query result.</summary>
	Task<TResult?> GetCachedAsync<TResult>(string key, CancellationToken cancellationToken = default) where TResult : class;

	/// <summary>Sets a cached query result.</summary>
	Task SetCachedAsync<TResult>(string key, TResult result, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where TResult : class;
}

/// <summary>Projection engine interface.</summary>
public interface IProjectionEngine
{
	/// <summary>Projects events for an aggregate.</summary>
	Task ProjectAsync(string aggregateId, CancellationToken cancellationToken = default);

	/// <summary>Rebuilds a projection.</summary>
	Task RebuildAsync(string projectionName, CancellationToken cancellationToken = default);

	/// <summary>Gets projection status.</summary>
	Task<ProjectionStatus> GetStatusAsync(string projectionName, CancellationToken cancellationToken = default);
}

/// <summary>Projection status.</summary>
public class ProjectionStatus
{
	/// <summary>Gets or sets the projection name.</summary>
	public string ProjectionName { get; set; } = string.Empty;

	/// <summary>Gets or sets the last processed position.</summary>
	public long LastProcessedPosition { get; set; }

	/// <summary>Gets or sets the last updated time.</summary>
	public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

	/// <summary>Gets or sets whether the projection is up to date.</summary>
	public bool IsUpToDate { get; set; }
}

/// <summary>Enhanced cache stampede preventor.</summary>
/// <typeparam name="T">The cached value type.</typeparam>
public sealed class EnhancedCacheStampedePreventor<T> : IDisposable
{
	private readonly IDistributedCache<T> _cache;
	private readonly ICacheStampedeMetrics? _metrics;
	private readonly ILogger? _logger;
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private bool _disposed;

	/// <summary>Initializes a new instance.</summary>
	public EnhancedCacheStampedePreventor(
		IDistributedCache<T> cache,
		ICacheStampedeMetrics? metrics = null,
		ILogger? logger = null)
	{
		_cache = cache;
		_metrics = metrics;
		_logger = logger;
	}

	/// <summary>Gets or creates a cached value with stampede prevention.</summary>
	public async Task<T?> GetOrCreateAsync(
		string key,
		Func<CancellationToken, Task<T>> factory,
		TimeSpan? expiration = null,
		CancellationToken cancellationToken = default)
	{
		var existing = await _cache.GetAsync(key, cancellationToken);
		if (existing != null)
		{
			return existing;
		}

		var waitStopwatch = System.Diagnostics.Stopwatch.StartNew();
		await _semaphore.WaitAsync(cancellationToken);
		waitStopwatch.Stop();
		_metrics?.RecordStampedeWait(key, waitStopwatch.Elapsed.TotalMilliseconds);

		try
		{
			// Double-check after acquiring lock
			existing = await _cache.GetAsync(key, cancellationToken);
			if (existing != null)
			{
				return existing;
			}

			_metrics?.RecordActiveLocks(1);
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				var value = await factory(cancellationToken);
				sw.Stop();
				_metrics?.RecordFactoryExecution(key, sw.Elapsed.TotalMilliseconds);

				await _cache.SetAsync(key, value, expiration, cancellationToken);
				return value;
			}
			catch (Exception ex)
			{
				_metrics?.RecordFactoryFailure(key, ex.GetType().Name);
				throw;
			}
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_semaphore.Dispose();
	}
}

/// <summary>Projection base class for CQRS projections.</summary>
/// <typeparam name="TReadModel">The read model type.</typeparam>
public abstract class ProjectionBase<TReadModel> where TReadModel : class, new()
{
	/// <summary>Gets the projection name.</summary>
	public abstract string ProjectionName { get; }

	/// <summary>Applies an event to the read model.</summary>
	public abstract Task<TReadModel> ApplyAsync(TReadModel readModel, object @event, CancellationToken cancellationToken = default);

	/// <summary>Creates a new read model instance.</summary>
	public virtual TReadModel CreateNew() => new();
}

/// <summary>Projection engine implementation.</summary>
public class ProjectionEngine : IProjectionEngine
{
	/// <inheritdoc/>
	public Task ProjectAsync(string aggregateId, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <inheritdoc/>
	public Task RebuildAsync(string projectionName, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <inheritdoc/>
	public Task<ProjectionStatus> GetStatusAsync(string projectionName, CancellationToken cancellationToken = default)
		=> Task.FromResult(new ProjectionStatus { ProjectionName = projectionName, IsUpToDate = true });
}

/// <summary>Projection invalidation tags interface.</summary>
public interface IProjectionInvalidationTags
{
	/// <summary>Gets the tags for invalidation.</summary>
	IReadOnlyList<string> Tags { get; }

	/// <summary>Gets the projection name.</summary>
	string ProjectionName { get; }
}
