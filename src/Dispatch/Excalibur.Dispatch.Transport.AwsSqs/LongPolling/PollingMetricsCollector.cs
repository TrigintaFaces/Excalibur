// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Collects and reports metrics for long polling operations.
/// </summary>
public sealed class PollingMetricsCollector : IPollingMetricsCollector, IDisposable
{
	private readonly ILogger<PollingMetricsCollector> _logger;
	private readonly ConcurrentBag<MetricData> _metrics;
	private readonly SemaphoreSlim _publishLock;

	private long _totalAttempts;
	private long _successfulAttempts;
	private long _failedAttempts;
	private long _totalMessagesReceived;
	private long _totalDurationTicks;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollingMetricsCollector" /> class.
	/// </summary>
	/// <param name="logger"> The logger. </param>
	public PollingMetricsCollector(ILogger<PollingMetricsCollector> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metrics = new ConcurrentBag<MetricData>();
		_publishLock = new SemaphoreSlim(1, 1);
	}

	/// <inheritdoc />
	public void RecordPollingAttempt(int messagesReceived, TimeSpan duration)
	{
		_ = Interlocked.Increment(ref _totalAttempts);
		_ = Interlocked.Add(ref _totalMessagesReceived, messagesReceived);
		_ = Interlocked.Add(ref _totalDurationTicks, duration.Ticks);

		if (messagesReceived > 0)
		{
			_ = Interlocked.Increment(ref _successfulAttempts);
		}

		// Record metrics
		RecordMetric("PollingAttempts", 1, MetricUnit.Count);
		RecordMetric("MessagesReceived", messagesReceived, MetricUnit.Count);
		RecordMetric("PollDuration", duration.TotalMilliseconds, MetricUnit.Milliseconds);
	}

	/// <inheritdoc />
	public void RecordPollingError(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		_ = Interlocked.Increment(ref _failedAttempts);
		_logger.LogWarning(exception, "Error during polling operation");

		RecordMetric("PollingErrors", 1, MetricUnit.Count);
	}

	/// <inheritdoc />
	public PollingStatistics GetStatistics()
	{
		var totalAttempts = Interlocked.Read(ref _totalAttempts);
		var totalMessages = Interlocked.Read(ref _totalMessagesReceived);
		var totalDurationTicks = Interlocked.Read(ref _totalDurationTicks);

		return new PollingStatistics
		{
			TotalAttempts = totalAttempts,
			SuccessfulAttempts = Interlocked.Read(ref _successfulAttempts),
			FailedAttempts = Interlocked.Read(ref _failedAttempts),
			TotalMessagesReceived = totalMessages,
			AverageMessagesPerPoll = totalAttempts > 0 ? (double)totalMessages / totalAttempts : 0,
			AveragePollDuration = totalAttempts > 0 ? TimeSpan.FromTicks(totalDurationTicks / totalAttempts) : TimeSpan.Zero,
		};
	}

	/// <inheritdoc />
	public void RecordMetric(string name, double value, MetricUnit unit = MetricUnit.None)
	{
		ArgumentNullException.ThrowIfNull(name);

		_metrics.Add(new MetricData
		{
			Name = name,
			Value = value,
			Unit = unit,
			Timestamp = DateTimeOffset.UtcNow,
		});
	}

	/// <inheritdoc />
	public void RecordMetric(string name, double value, MetricUnit unit, Dictionary<string, string> dimensions)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(dimensions);

		var metricData = new MetricData
		{
			Name = name,
			Value = value,
			Unit = unit,
			Timestamp = DateTimeOffset.UtcNow,
		};

		foreach (var dimension in dimensions)
		{
			metricData.Dimensions[dimension.Key] = dimension.Value;
		}

		_metrics.Add(metricData);
	}

	/// <inheritdoc />
	public IReadOnlyList<MetricData> GetMetrics()
	{
		return _metrics.ToArray();
	}

	/// <inheritdoc />
	public void Clear()
	{
		_metrics.Clear();
		_totalAttempts = 0;
		_successfulAttempts = 0;
		_failedAttempts = 0;
		_totalMessagesReceived = 0;
		_totalDurationTicks = 0;
	}

	/// <summary>
	/// Disposes the metrics collector.
	/// </summary>
	public void Dispose()
	{
		_publishLock?.Dispose();
		GC.SuppressFinalize(this);
	}
}
