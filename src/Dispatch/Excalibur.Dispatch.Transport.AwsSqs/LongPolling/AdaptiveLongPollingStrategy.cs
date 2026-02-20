// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Excalibur.Dispatch.Abstractions.Diagnostics;

using LocalLongPollingStatistics = Excalibur.Dispatch.Transport.LongPollingStatistics;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Implements an adaptive long polling strategy that adjusts wait times based on message flow patterns.
/// </summary>
public sealed class AdaptiveLongPollingStrategy : ILongPollingStrategy, IDisposable
{
	private readonly LongPollingConfiguration _configuration;
	private readonly ConcurrentQueue<ReceiveRecord> _receiveHistory;
	private readonly SemaphoreSlim _updateLock;

	private readonly ValueStopwatch _uptime;
	private double _currentLoadFactor;
	private TimeSpan _currentWaitTime;
	private long _totalReceives;
	private long _totalMessages;
	private long _emptyReceives;
	private long _apiCallsSaved;
	private DateTimeOffset _lastReceiveTime;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdaptiveLongPollingStrategy" /> class.
	/// </summary>
	/// <param name="configuration"> The long polling configuration. </param>
	public AdaptiveLongPollingStrategy(LongPollingConfiguration configuration)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_configuration.Validate();

		_receiveHistory = new ConcurrentQueue<ReceiveRecord>();
		_updateLock = new SemaphoreSlim(1, 1);
		_currentWaitTime = TimeSpan.FromSeconds(10); // Start with moderate wait time
		_currentLoadFactor = 0.5;
		_uptime = ValueStopwatch.StartNew();
		_lastReceiveTime = DateTimeOffset.UtcNow;
	}

	/// <inheritdoc />
	public string Name => "Adaptive";

	/// <inheritdoc />
	public ValueTask<TimeSpan> CalculateOptimalWaitTimeAsync()
	{
		if (!_configuration.EnableAdaptivePolling)
		{
			return new ValueTask<TimeSpan>(_configuration.MaxWaitTime);
		}

		// Clean up old history entries
		CleanupHistory();

		// Calculate based on current load factor
		var optimalWaitTime = CalculateWaitTimeFromLoadFactor(_currentLoadFactor);

		return new ValueTask<TimeSpan>(optimalWaitTime);
	}

	/// <inheritdoc />
	public async ValueTask RecordReceiveResultAsync(int messageCount, TimeSpan actualWaitTime)
	{
		await _updateLock.WaitAsync().ConfigureAwait(false);
		try
		{
			// Update counters
			_totalReceives++;
			_totalMessages += messageCount;
			if (messageCount == 0)
			{
				_emptyReceives++;
			}

			_lastReceiveTime = DateTimeOffset.UtcNow;

			// Record in history
			_receiveHistory.Enqueue(new ReceiveRecord
			{
				Timestamp = _lastReceiveTime,
				MessageCount = messageCount,
				WaitTime = actualWaitTime,
			});

			// Update load factor if adaptive polling is enabled
			if (_configuration.EnableAdaptivePolling)
			{
				UpdateLoadFactor();
				_currentWaitTime = CalculateWaitTimeFromLoadFactor(_currentLoadFactor);

				// Calculate API calls saved
				if (actualWaitTime > TimeSpan.FromSeconds(1))
				{
					var potentialEmptyPolls = (int)(actualWaitTime.TotalSeconds / 1) - 1;
					if (messageCount == 0)
					{
						_apiCallsSaved += potentialEmptyPolls;
					}
				}
			}
		}
		finally
		{
			_ = _updateLock.Release();
		}
	}

	/// <inheritdoc />
	public ValueTask<double> GetCurrentLoadFactorAsync() => new(_currentLoadFactor);

	/// <inheritdoc />
	public async ValueTask ResetAsync()
	{
		await _updateLock.WaitAsync().ConfigureAwait(false);
		try
		{
			while (_receiveHistory.TryDequeue(out _))
			{
			}

			_currentLoadFactor = 0.5;
			_currentWaitTime = TimeSpan.FromSeconds(10);
			_totalReceives = 0;
			_totalMessages = 0;
			_emptyReceives = 0;
			_apiCallsSaved = 0;
			_lastReceiveTime = DateTimeOffset.UtcNow;
		}
		finally
		{
			_ = _updateLock.Release();
		}
	}

	/// <inheritdoc />
	public ValueTask<LocalLongPollingStatistics> GetStatisticsAsync()
	{
		var stats = new LocalLongPollingStatistics
		{
			TotalReceives = _totalReceives,
			TotalMessages = _totalMessages,
			EmptyReceives = _emptyReceives,
			CurrentLoadFactor = _currentLoadFactor,
			CurrentWaitTime = _currentWaitTime,
			ApiCallsSaved = _apiCallsSaved,
			LastReceiveTime = _lastReceiveTime,
		};

		return new ValueTask<LocalLongPollingStatistics>(stats);
	}

	/// <inheritdoc />
	public Task<List<TMessage>> PollAsync<TMessage>(string queueUrl, CancellationToken cancellationToken)
		where TMessage : class =>

		// This strategy doesn't actually poll - it just provides wait time calculations
		// The actual polling is done by the receiver using this strategy
		throw new InvalidOperationException(
			"AdaptiveLongPollingStrategy provides wait time calculations only. Use ILongPollingReceiver for actual polling.");

	/// <inheritdoc />
	public void Dispose()
	{
		_updateLock?.Dispose();
		GC.SuppressFinalize(this);
	}

	private void UpdateLoadFactor()
	{
		var cutoffTime = DateTimeOffset.UtcNow - _configuration.AdaptationWindow;
		var recentRecords = _receiveHistory.Where(r => r.Timestamp > cutoffTime).ToList();

		if (recentRecords.Count == 0)
		{
			return;
		}

		// Calculate average messages per receive in the window
		var totalMessagesInWindow = recentRecords.Sum(r => r.MessageCount);
		var averageMessagesPerReceive = (double)totalMessagesInWindow / recentRecords.Count;

		// Calculate new load factor
		var newLoadFactor = averageMessagesPerReceive / _configuration.MaxMessagesPerReceive;
		newLoadFactor = Math.Min(1.0, Math.Max(0.0, newLoadFactor));

		// Apply exponential smoothing
		_currentLoadFactor = (_configuration.SmoothingFactor * newLoadFactor) +
							 ((1 - _configuration.SmoothingFactor) * _currentLoadFactor);
	}

	private TimeSpan CalculateWaitTimeFromLoadFactor(double loadFactor)
	{

		if (loadFactor >= _configuration.HighLoadThreshold)
		{
			// High load: Use minimum wait time for maximum throughput
			return _configuration.MinWaitTime;
		}
		else if (loadFactor <= _configuration.LowLoadThreshold)
		{
			// Low load: Use maximum wait time to reduce API calls
			return _configuration.MaxWaitTime;
		}
		else
		{
			// Medium load: Linear interpolation between min and max
			var range = _configuration.HighLoadThreshold - _configuration.LowLoadThreshold;
			var position = (loadFactor - _configuration.LowLoadThreshold) / range;
			var waitRange = _configuration.MaxWaitTime - _configuration.MinWaitTime;
			return _configuration.MaxWaitTime - TimeSpan.FromMilliseconds(waitRange.TotalMilliseconds * position);
		}
	}

	private void CleanupHistory()
	{
		var cutoffTime = DateTimeOffset.UtcNow - _configuration.AdaptationWindow;

		// Remove old records
		while (_receiveHistory.TryPeek(out var record) && record.Timestamp < cutoffTime)
		{
			_ = _receiveHistory.TryDequeue(out _);
		}
	}

	[StructLayout(LayoutKind.Auto)]
	private readonly record struct ReceiveRecord
	{
		public DateTimeOffset Timestamp { get; init; }

		public int MessageCount { get; init; }

		public TimeSpan WaitTime { get; init; }
	}
}
