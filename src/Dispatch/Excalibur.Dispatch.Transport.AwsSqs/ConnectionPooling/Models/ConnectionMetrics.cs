// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Metrics collector for connection pool operations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ConnectionMetrics" /> class. </remarks>
/// <param name="metricsPrefix"> The prefix for metric names. </param>
public class ConnectionMetrics(string metricsPrefix) : IDisposable
{
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private long _acquisitions;
	private long _hits;
	private long _errors;
	private TimeSpan _totalAcquisitionTime;

	/// <summary>
	/// Updates the pool size metrics.
	/// </summary>
	/// <param name="available"> Number of available connections. </param>
	/// <param name="active"> Number of active connections. </param>
	public static void UpdatePoolSize(int available, int active)
	{
		_ = available;
		_ = active;
		// In a real implementation, this would update metrics collectors For now, this is a placeholder
	}

	/// <summary>
	/// Records a connection acquisition.
	/// </summary>
	/// <param name="acquisitionTime"> The time taken to acquire the connection. </param>
	/// <param name="hit"> Whether the acquisition was a cache hit. </param>
	public void RecordAcquisition(TimeSpan acquisitionTime, bool hit)
	{
		lock (_lock)
		{
			_acquisitions++;
			_totalAcquisitionTime += acquisitionTime;
			if (hit)
			{
				_hits++;
			}
		}
	}

	/// <summary>
	/// Records an error.
	/// </summary>
	public void RecordError()
	{
		lock (_lock)
		{
			_errors++;
		}
	}

	/// <summary>
	/// Gets the cache hit rate.
	/// </summary>
	/// <returns> The hit rate as a percentage. </returns>
	public double GetHitRate()
	{
		lock (_lock)
		{
			return _acquisitions > 0 ? (double)_hits / _acquisitions : 0;
		}
	}

	/// <summary>
	/// Gets the average acquisition time.
	/// </summary>
	/// <returns> The average acquisition time. </returns>
	public TimeSpan GetAverageAcquisitionTime()
	{
		lock (_lock)
		{
			return _acquisitions > 0
				? TimeSpan.FromMilliseconds(_totalAcquisitionTime.TotalMilliseconds / _acquisitions)
				: TimeSpan.Zero;
		}
	}

	/// <summary>
	/// Gets the total error count.
	/// </summary>
	/// <returns> The error count. </returns>
	public long GetErrorCount()
	{
		lock (_lock)
		{
			return _errors;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="ConnectionMetrics" /> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"> true to release both managed and unmanaged resources; false to release only unmanaged resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			// Cleanup managed resources if needed
		}
	}
}
