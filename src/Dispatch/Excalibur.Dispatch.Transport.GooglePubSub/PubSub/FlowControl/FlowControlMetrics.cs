// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Tracks metrics for Pub/Sub flow control to enable adaptive behavior and monitoring.
/// </summary>
public sealed class FlowControlMetrics
{
	private long _messagesReceived;
	private long _messagesProcessed;
	private long _bytesReceived;
	private long _bytesProcessed;
	private long _processingErrors;
	private long _flowControlPauses;
	private long _currentOutstandingMessages;
	private long _currentOutstandingBytes;
	private DateTimeOffset _lastResetTime = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the total number of messages received since the last reset.
	/// </summary>
	/// <value>
	/// The total number of messages received since the last reset.
	/// </value>
	public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

	/// <summary>
	/// Gets the total number of messages successfully processed since the last reset.
	/// </summary>
	/// <value>
	/// The total number of messages successfully processed since the last reset.
	/// </value>
	public long MessagesProcessed => Interlocked.Read(ref _messagesProcessed);

	/// <summary>
	/// Gets the total number of bytes received since the last reset.
	/// </summary>
	/// <value>
	/// The total number of bytes received since the last reset.
	/// </value>
	public long BytesReceived => Interlocked.Read(ref _bytesReceived);

	/// <summary>
	/// Gets the total number of bytes successfully processed since the last reset.
	/// </summary>
	/// <value>
	/// The total number of bytes successfully processed since the last reset.
	/// </value>
	public long BytesProcessed => Interlocked.Read(ref _bytesProcessed);

	/// <summary>
	/// Gets the total number of processing errors since the last reset.
	/// </summary>
	/// <value>
	/// The total number of processing errors since the last reset.
	/// </value>
	public long ProcessingErrors => Interlocked.Read(ref _processingErrors);

	/// <summary>
	/// Gets the number of times flow control has paused message pulling since the last reset.
	/// </summary>
	/// <value>
	/// The number of times flow control has paused message pulling since the last reset.
	/// </value>
	public long FlowControlPauses => Interlocked.Read(ref _flowControlPauses);

	/// <summary>
	/// Gets the current number of outstanding (unprocessed) messages.
	/// </summary>
	/// <value>
	/// The current number of outstanding (unprocessed) messages.
	/// </value>
	public long CurrentOutstandingMessages => Interlocked.Read(ref _currentOutstandingMessages);

	/// <summary>
	/// Gets the current total size in bytes of outstanding (unprocessed) messages.
	/// </summary>
	/// <value>
	/// The current total size in bytes of outstanding (unprocessed) messages.
	/// </value>
	public long CurrentOutstandingBytes => Interlocked.Read(ref _currentOutstandingBytes);

	/// <summary>
	/// Gets the time elapsed since the last metrics reset.
	/// </summary>
	/// <value>
	/// The time elapsed since the last metrics reset.
	/// </value>
	public TimeSpan TimeSinceReset => DateTimeOffset.UtcNow - _lastResetTime;

	/// <summary>
	/// Gets the message processing rate (messages per second) since the last reset.
	/// </summary>
	/// <value>
	/// The message processing rate (messages per second) since the last reset.
	/// </value>
	public double MessageProcessingRate
	{
		get
		{
			var elapsed = TimeSinceReset.TotalSeconds;
			return elapsed > 0 ? MessagesProcessed / elapsed : 0;
		}
	}

	/// <summary>
	/// Gets the byte processing rate (bytes per second) since the last reset.
	/// </summary>
	/// <value>
	/// The byte processing rate (bytes per second) since the last reset.
	/// </value>
	public double ByteProcessingRate
	{
		get
		{
			var elapsed = TimeSinceReset.TotalSeconds;
			return elapsed > 0 ? BytesProcessed / elapsed : 0;
		}
	}

	/// <summary>
	/// Gets the error rate (errors per message) since the last reset.
	/// </summary>
	/// <value>
	/// The error rate (errors per message) since the last reset.
	/// </value>
	public double ErrorRate
	{
		get
		{
			var processed = MessagesProcessed;
			return processed > 0 ? (double)ProcessingErrors / processed : 0;
		}
	}

	/// <summary>
	/// Gets the utilization percentage based on outstanding messages vs processed messages.
	/// </summary>
	/// <value>
	/// The utilization percentage based on outstanding messages vs processed messages.
	/// </value>
	public double UtilizationPercentage
	{
		get
		{
			var total = MessagesReceived;
			if (total == 0)
			{
				return 0;
			}

			var outstanding = CurrentOutstandingMessages;
			return (double)outstanding / total * 100;
		}
	}

	/// <summary>
	/// Gets the current number of outstanding messages (alias for CurrentOutstandingMessages).
	/// </summary>
	/// <value>
	/// The current number of outstanding messages (alias for CurrentOutstandingMessages).
	/// </value>
	public long OutstandingMessages => CurrentOutstandingMessages;

	/// <summary>
	/// Gets or sets the maximum concurrency allowed for processing.
	/// </summary>
	/// <value>
	/// The maximum concurrency allowed for processing.
	/// </value>
	public long MaxConcurrency { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of outstanding messages allowed.
	/// </summary>
	/// <value>
	/// The maximum number of outstanding messages allowed.
	/// </value>
	public long MaxOutstandingMessages { get; set; } = 1000;

	/// <summary>
	/// Gets the available capacity (remaining slots) for processing.
	/// </summary>
	/// <value>
	/// The available capacity (remaining slots) for processing.
	/// </value>
	public long AvailableCapacity => Math.Max(0, MaxOutstandingMessages - CurrentOutstandingMessages);

	/// <summary>
	/// Records that a message was received.
	/// </summary>
	/// <param name="sizeInBytes"> The size of the message in bytes. </param>
	public void RecordMessageReceived(long sizeInBytes)
	{
		_ = Interlocked.Increment(ref _messagesReceived);
		_ = Interlocked.Add(ref _bytesReceived, sizeInBytes);
		_ = Interlocked.Increment(ref _currentOutstandingMessages);
		_ = Interlocked.Add(ref _currentOutstandingBytes, sizeInBytes);
	}

	/// <summary>
	/// Records that a message was successfully processed.
	/// </summary>
	/// <param name="sizeInBytes"> The size of the message in bytes. </param>
	public void RecordMessageProcessed(long sizeInBytes)
	{
		_ = Interlocked.Increment(ref _messagesProcessed);
		_ = Interlocked.Add(ref _bytesProcessed, sizeInBytes);
		_ = Interlocked.Decrement(ref _currentOutstandingMessages);
		_ = Interlocked.Add(ref _currentOutstandingBytes, -sizeInBytes);
	}

	/// <summary>
	/// Records that a message processing error occurred.
	/// </summary>
	/// <param name="sizeInBytes"> The size of the message in bytes. </param>
	public void RecordProcessingError(long sizeInBytes)
	{
		_ = Interlocked.Increment(ref _processingErrors);

		// Still need to decrement outstanding counts on error
		_ = Interlocked.Decrement(ref _currentOutstandingMessages);
		_ = Interlocked.Add(ref _currentOutstandingBytes, -sizeInBytes);
	}

	/// <summary>
	/// Records that flow control has paused message pulling.
	/// </summary>
	public void RecordFlowControlPause() => _ = Interlocked.Increment(ref _flowControlPauses);

	/// <summary>
	/// Gets a snapshot of the current metrics.
	/// </summary>
	/// <returns> A snapshot of the current metrics state. </returns>
	public FlowControlMetricsSnapshot GetSnapshot() =>
		new()
		{
			MessagesReceived = MessagesReceived,
			MessagesProcessed = MessagesProcessed,
			BytesReceived = BytesReceived,
			BytesProcessed = BytesProcessed,
			ProcessingErrors = ProcessingErrors,
			FlowControlPauses = FlowControlPauses,
			CurrentOutstandingMessages = CurrentOutstandingMessages,
			CurrentOutstandingBytes = CurrentOutstandingBytes,
			MessageProcessingRate = MessageProcessingRate,
			ByteProcessingRate = ByteProcessingRate,
			ErrorRate = ErrorRate,
			UtilizationPercentage = UtilizationPercentage,
			SnapshotTime = DateTimeOffset.UtcNow,
		};

	/// <summary>
	/// Resets all metrics to zero.
	/// </summary>
	public void Reset()
	{
		_ = Interlocked.Exchange(ref _messagesReceived, 0);
		_ = Interlocked.Exchange(ref _messagesProcessed, 0);
		_ = Interlocked.Exchange(ref _bytesReceived, 0);
		_ = Interlocked.Exchange(ref _bytesProcessed, 0);
		_ = Interlocked.Exchange(ref _processingErrors, 0);
		_ = Interlocked.Exchange(ref _flowControlPauses, 0);

		// Note: We don't reset outstanding counts as they represent current state
		_lastResetTime = DateTimeOffset.UtcNow;
	}
}
