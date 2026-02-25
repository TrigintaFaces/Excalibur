// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Metrics for long polling receiver.
/// </summary>
public sealed class LongPollingMetrics
{
	private readonly ValueStopwatch _rateStopwatch = ValueStopwatch.StartNew();
	private long _messagesReceived;
	private long _emptyPolls;
	private long _errors;
	private double _totalPollTime;
	private long _pollCount;

	public long MessagesReceived => _messagesReceived;

	public long EmptyPolls => _emptyPolls;

	public long Errors => _errors;

	public double AveragePollTime => _pollCount > 0 ? _totalPollTime / _pollCount : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordMessagesReceived(int count) => Interlocked.Add(ref _messagesReceived, count);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordEmptyPoll() => Interlocked.Increment(ref _emptyPolls);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordError() => Interlocked.Increment(ref _errors);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordPollDuration(TimeSpan duration)
	{
		_ = Interlocked.Increment(ref _pollCount);
		InterlockedAddDouble(ref _totalPollTime, duration.TotalMilliseconds);
	}

	public LongPollingSnapshot GetSnapshot()
	{
		var elapsed = _rateStopwatch.Elapsed.TotalSeconds;
		return new LongPollingSnapshot
		{
			MessagesReceived = _messagesReceived,
			EmptyPolls = _emptyPolls,
			Errors = _errors,
			MessageRate = elapsed > 0 ? _messagesReceived / elapsed : 0,
			EmptyPollRate = _pollCount > 0 ? (double)_emptyPolls / _pollCount : 0,
		};
	}

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
