// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Default implementation of SQS channel metrics collector.
/// </summary>
internal sealed class SqsChannelMetricsCollector : ISqsChannelMetricsCollector
{
	private readonly ConcurrentDictionary<string, QueueMetrics> _queueMetrics = new(StringComparer.Ordinal);

	/// <inheritdoc/>
	public void RecordMessageReceived(string queueUrl, TimeSpan processingTime)
	{
		var metrics = _queueMetrics.GetOrAdd(queueUrl, static _ => new QueueMetrics());
		metrics.RecordReceived(processingTime);
	}

	/// <inheritdoc/>
	public void RecordMessageSent(string queueUrl, TimeSpan sendTime)
	{
		var metrics = _queueMetrics.GetOrAdd(queueUrl, static _ => new QueueMetrics());
		metrics.RecordSent(sendTime);
	}

	/// <inheritdoc/>
	public void RecordError(string queueUrl, string errorType)
	{
		var metrics = _queueMetrics.GetOrAdd(queueUrl, static _ => new QueueMetrics());
		metrics.RecordError(errorType);
	}

	/// <inheritdoc/>
	public Task<SqsChannelMetricsSummary> GetSummaryAsync()
	{
		var summary = new SqsChannelMetricsSummary
		{
			QueueMetrics = _queueMetrics.ToDictionary(
				static kvp => kvp.Key,
				static kvp => kvp.Value.GetSnapshot(), StringComparer.Ordinal),
		};

		return Task.FromResult(summary);
	}

	private sealed class QueueMetrics
	{
		private long _messagesReceived;
		private long _messagesSent;
		private long _errors;
		private double _totalReceiveTime;
		private double _totalSendTime;

		public void RecordReceived(TimeSpan processingTime)
		{
			_ = Interlocked.Increment(ref _messagesReceived);
			InterlockedAddDouble(ref _totalReceiveTime, processingTime.TotalMilliseconds);
		}

		public void RecordSent(TimeSpan sendTime)
		{
			_ = Interlocked.Increment(ref _messagesSent);
			InterlockedAddDouble(ref _totalSendTime, sendTime.TotalMilliseconds);
		}

		public void RecordError(string errorType)
		{
			_ = errorType;
			_ = Interlocked.Increment(ref _errors);
		}

		public QueueMetricsSnapshot GetSnapshot() =>
			new()
			{
				MessagesReceived = _messagesReceived,
				MessagesSent = _messagesSent,
				Errors = _errors,
				AverageReceiveTime = _messagesReceived > 0 ? _totalReceiveTime / _messagesReceived : 0,
				AverageSendTime = _messagesSent > 0 ? _totalSendTime / _messagesSent : 0,
			};

		private static void InterlockedAddDouble(ref double location, double value)
		{
			double initial, newValue;
			do
			{
				initial = location;
				newValue = initial + value;
			}
			while (Interlocked.CompareExchange(ref location, newValue, initial) != initial);
		}
	}
}
