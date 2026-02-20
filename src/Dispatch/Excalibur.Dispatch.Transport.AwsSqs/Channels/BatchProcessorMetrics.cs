// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Metrics for batch processor.
/// </summary>
public sealed class BatchProcessorMetrics
{
	private long _messagesReceived;
	private long _messagesSent;
	private long _messagesDeleted;
	private long _sendErrors;
	private long _deleteErrors;
	private double _totalReceiveTime;
	private double _totalSendTime;
	private double _totalDeleteTime;

	public long MessagesReceived => _messagesReceived;

	public long MessagesSent => _messagesSent;

	public long MessagesDeleted => _messagesDeleted;

	public long SendErrors => _sendErrors;

	public long DeleteErrors => _deleteErrors;

	public double AverageReceiveTime => _messagesReceived > 0 ? _totalReceiveTime / _messagesReceived : 0;

	public double AverageSendTime => _messagesSent > 0 ? _totalSendTime / _messagesSent : 0;

	public double AverageDeleteTime => _messagesDeleted > 0 ? _totalDeleteTime / _messagesDeleted : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordReceiveBatch(int count, TimeSpan elapsed)
	{
		_ = Interlocked.Add(ref _messagesReceived, count);
		InterlockedAddDouble(ref _totalReceiveTime, elapsed.TotalMilliseconds);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordSendBatch(int count, TimeSpan elapsed)
	{
		_ = Interlocked.Add(ref _messagesSent, count);
		InterlockedAddDouble(ref _totalSendTime, elapsed.TotalMilliseconds);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordDeleteBatch(int count, TimeSpan elapsed)
	{
		_ = Interlocked.Add(ref _messagesDeleted, count);
		InterlockedAddDouble(ref _totalDeleteTime, elapsed.TotalMilliseconds);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordSendErrors(int count) => Interlocked.Add(ref _sendErrors, count);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordDeleteErrors(int count) => Interlocked.Add(ref _deleteErrors, count);

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
