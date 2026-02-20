// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using StackExchange.Redis;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Implementation of cache health monitoring.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CacheHealthMonitor" /> class. </remarks>
/// <param name="connectionMultiplexer"> Optional Redis connection multiplexer for health checks. </param>
public sealed class CacheHealthMonitor(IConnectionMultiplexer? connectionMultiplexer = null) : ICacheHealthMonitor
{
	private long _totalRequests;
	private long _cacheHits;
	private long _errors;

	/// <inheritdoc />
	public async Task<CacheHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (connectionMultiplexer?.IsConnected == true)
			{
				var database = connectionMultiplexer.GetDatabase();
				var pingResult = await database.PingAsync().ConfigureAwait(false);

				return new CacheHealthStatus
				{
					IsHealthy = true,
					ResponseTimeMs = pingResult.TotalMilliseconds,
					ConnectionStatus = "Connected",
					LastChecked = DateTimeOffset.UtcNow,
				};
			}

			return new CacheHealthStatus
			{
				IsHealthy = false,
				ConnectionStatus = "Redis not available",
				LastChecked = DateTimeOffset.UtcNow,
			};
		}
		catch (Exception ex)
		{
			return new CacheHealthStatus
			{
				IsHealthy = false,
				ConnectionStatus = $"Error: {ex.Message}",
				LastChecked = DateTimeOffset.UtcNow,
			};
		}
	}

	/// <inheritdoc />
	public CachePerformanceSnapshot GetPerformanceSnapshot()
	{
		var totalRequests = Interlocked.Read(ref _totalRequests);
		var cacheHits = Interlocked.Read(ref _cacheHits);
		var errors = Interlocked.Read(ref _errors);

		return new CachePerformanceSnapshot
		{
			HitCount = cacheHits,
			MissCount = totalRequests - cacheHits,
			TotalErrors = errors,
			Timestamp = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Records a cache operation for monitoring.
	/// </summary>
	/// <param name="isHit"> True if the operation was a cache hit; otherwise, false. </param>
	/// <param name="hasError"> True if the operation resulted in an error; otherwise, false. </param>
	public void RecordCacheOperation(bool isHit, bool hasError = false)
	{
		_ = Interlocked.Increment(ref _totalRequests);
		if (isHit)
		{
			_ = Interlocked.Increment(ref _cacheHits);
		}

		if (hasError)
		{
			_ = Interlocked.Increment(ref _errors);
		}
	}
}
