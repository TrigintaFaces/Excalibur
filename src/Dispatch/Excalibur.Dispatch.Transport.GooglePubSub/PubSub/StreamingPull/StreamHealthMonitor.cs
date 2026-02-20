// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// EventArgs for unhealthy stream detection events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="UnhealthyStreamEventArgs"/> class.
/// </remarks>
/// <param name="streamId">The identifier of the unhealthy stream.</param>
public sealed class UnhealthyStreamEventArgs(string streamId) : EventArgs
{
	/// <summary>
	/// Gets the identifier of the unhealthy stream.
	/// </summary>
	/// <value>
	/// The identifier of the unhealthy stream.
	/// </value>
	public string StreamId { get; } = streamId ?? throw new ArgumentNullException(nameof(streamId));
}

/// <summary>
/// Monitors the health of streaming pull connections and manages reconnection.
/// </summary>
public sealed partial class StreamHealthMonitor : IDisposable
{
	private readonly ILogger<StreamHealthMonitor> _logger;
	private readonly StreamingPullOptions _options;
	private readonly ConcurrentDictionary<string, StreamHealthInfo> _streamHealth;
	private readonly Timer? _healthCheckTimer;
	private readonly SemaphoreSlim _checkSemaphore;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamHealthMonitor" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="options"> The streaming pull options. </param>
	public StreamHealthMonitor(
		ILogger<StreamHealthMonitor> logger,
		StreamingPullOptions options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_streamHealth = new ConcurrentDictionary<string, StreamHealthInfo>(StringComparer.Ordinal);
		_checkSemaphore = new SemaphoreSlim(1, 1);

		if (_options.EnableHealthMonitoring)
		{
			_healthCheckTimer = new Timer(
				PerformHealthCheck,
				state: null,
				_options.HealthCheckInterval,
				_options.HealthCheckInterval);
		}
	}

	/// <summary>
	/// Event raised when an unhealthy stream is detected.
	/// </summary>
	public event EventHandler<UnhealthyStreamEventArgs>? UnhealthyStreamDetected;

	/// <summary>
	/// Records a message received on a stream.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="messageSize"> The size of the message in bytes. </param>
	public void RecordMessageReceived(string streamId, long messageSize)
	{
		if (_disposed)
		{
			return;
		}

		var health = _streamHealth.GetOrAdd(streamId, _ => new StreamHealthInfo(streamId));
		health.LastMessageTime = DateTimeOffset.UtcNow;
		health.MessagesReceived++;
		health.BytesReceived += messageSize;
	}

	/// <summary>
	/// Records an error on a stream.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="error"> The error that occurred. </param>
	public void RecordError(string streamId, Exception error)
	{
		if (_disposed)
		{
			return;
		}

		var health = _streamHealth.GetOrAdd(streamId, _ => new StreamHealthInfo(streamId));
		health.LastErrorTime = DateTimeOffset.UtcNow;
		health.ErrorCount++;
		health.LastError = error;

		LogStreamError(streamId, (int)health.ErrorCount, error);
	}

	/// <summary>
	/// Records a successful acknowledgment.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void RecordAcknowledgment(string streamId)
	{
		if (_disposed)
		{
			return;
		}

		var health = _streamHealth.GetOrAdd(streamId, _ => new StreamHealthInfo(streamId));
		health.AcknowledgmentsSucceeded++;
	}

	/// <summary>
	/// Records a failed acknowledgment.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void RecordAcknowledgmentFailure(string streamId)
	{
		if (_disposed)
		{
			return;
		}

		var health = _streamHealth.GetOrAdd(streamId, _ => new StreamHealthInfo(streamId));
		health.AcknowledgmentsFailed++;
	}

	/// <summary>
	/// Marks a stream as connected.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void MarkConnected(string streamId)
	{
		if (_disposed)
		{
			return;
		}

		var health = _streamHealth.GetOrAdd(streamId, _ => new StreamHealthInfo(streamId));
		health.ConnectedTime = DateTimeOffset.UtcNow;
		health.IsConnected = true;
		health.ReconnectCount++;

		LogStreamConnected(streamId, health.ReconnectCount);
	}

	/// <summary>
	/// Marks a stream as disconnected.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void MarkDisconnected(string streamId)
	{
		if (_disposed)
		{
			return;
		}

		if (_streamHealth.TryGetValue(streamId, out var health))
		{
			health.IsConnected = false;
			health.DisconnectedTime = DateTimeOffset.UtcNow;

			LogStreamDisconnected(streamId);
		}
	}

	/// <summary>
	/// Checks if a stream is healthy.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <returns> True if the stream is healthy; otherwise, false. </returns>
	public bool IsHealthy(string streamId)
	{
		if (_disposed || !_streamHealth.TryGetValue(streamId, out var health))
		{
			return false;
		}

		if (!health.IsConnected)
		{
			return false;
		}

		var now = DateTimeOffset.UtcNow;
		var timeSinceLastMessage = now - health.LastMessageTime;

		// Check if stream is idle
		if (timeSinceLastMessage > _options.StreamIdleTimeout)
		{
			LogStreamIdle(streamId, timeSinceLastMessage);
			return false;
		}

		// Check error rate
		if (health is { ErrorCount: > 0, MessagesReceived: > 0 })
		{
			var errorRate = (double)health.ErrorCount / health.MessagesReceived;
			if (errorRate > 0.1) // More than 10% error rate
			{
				LogHighErrorRate(streamId, errorRate);
				return false;
			}
		}

		// Check acknowledgment failures
		var totalAcks = health.AcknowledgmentsSucceeded + health.AcknowledgmentsFailed;
		if (totalAcks > 0)
		{
			var failureRate = (double)health.AcknowledgmentsFailed / totalAcks;
			if (failureRate > 0.05) // More than 5% ack failure rate
			{
				LogHighAckFailureRate(streamId, failureRate);
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Gets the health information for a stream.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <returns> The health information, or null if not found. </returns>
	public StreamHealthInfo? GetHealthInfo(string streamId) => _streamHealth.GetValueOrDefault(streamId);

	/// <summary>
	/// Gets health information for all streams.
	/// </summary>
	/// <returns> A collection of health information for all streams. </returns>
	public StreamHealthInfo[] GetAllHealthInfo() => [.. _streamHealth.Values];

	/// <summary>
	/// Removes health information for a stream.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void RemoveStream(string streamId) => _ = _streamHealth.TryRemove(streamId, out _);

	/// <summary>
	/// Disposes the health monitor.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_healthCheckTimer?.Dispose();
		_checkSemaphore.Dispose();
		_streamHealth.Clear();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Performs a health check on all streams.
	/// </summary>
	private async void PerformHealthCheck(object? state)
	{
		if (_disposed || !await _checkSemaphore.WaitAsync(0).ConfigureAwait(false))
		{
			return;
		}

		try
		{
			var unhealthyStreams = 0;
			var totalStreams = 0;

			foreach (var kvp in _streamHealth)
			{
				totalStreams++;
				if (!IsHealthy(kvp.Key))
				{
					unhealthyStreams++;
					UnhealthyStreamDetected?.Invoke(this, new UnhealthyStreamEventArgs(kvp.Key));
				}
			}

			if (unhealthyStreams > 0)
			{
				LogUnhealthyStreamsFound(unhealthyStreams, totalStreams);
			}
		}
		catch (Exception ex)
		{
			LogHealthCheckError(ex);
		}
		finally
		{
			_ = _checkSemaphore.Release();
		}
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.StreamError, LogLevel.Warning,
		"Stream {StreamId} encountered error #{ErrorCount}")]
	private partial void LogStreamError(string streamId, int errorCount, Exception ex);

	[LoggerMessage(GooglePubSubEventId.StreamConnected, LogLevel.Information,
		"Stream {StreamId} connected (reconnect #{ReconnectCount})")]
	private partial void LogStreamConnected(string streamId, int reconnectCount);

	[LoggerMessage(GooglePubSubEventId.StreamDisconnected, LogLevel.Warning,
		"Stream {StreamId} disconnected")]
	private partial void LogStreamDisconnected(string streamId);

	[LoggerMessage(GooglePubSubEventId.StreamIdle, LogLevel.Warning,
		"Stream {StreamId} is idle (no messages for {IdleTime})")]
	private partial void LogStreamIdle(string streamId, TimeSpan idleTime);

	[LoggerMessage(GooglePubSubEventId.HighErrorRate, LogLevel.Warning,
		"Stream {StreamId} has high error rate: {ErrorRate:P}")]
	private partial void LogHighErrorRate(string streamId, double errorRate);

	[LoggerMessage(GooglePubSubEventId.HighAckFailureRate, LogLevel.Warning,
		"Stream {StreamId} has high ack failure rate: {FailureRate:P}")]
	private partial void LogHighAckFailureRate(string streamId, double failureRate);

	[LoggerMessage(GooglePubSubEventId.UnhealthyStreamsFound, LogLevel.Warning,
		"Health check found {UnhealthyCount}/{TotalCount} unhealthy streams")]
	private partial void LogUnhealthyStreamsFound(int unhealthyCount, int totalCount);

	[LoggerMessage(GooglePubSubEventId.HealthCheckError, LogLevel.Error,
		"Error during health check")]
	private partial void LogHealthCheckError(Exception ex);
}
