// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Metrics for SQS message processor.
/// </summary>
public sealed class SqsProcessorMetrics
{
	private long _messagesProcessed;
	private long _processingErrors;
	private long _messagesDeleted;
	private long _deleteErrors;
	private double _totalProcessingTime;

	public long MessagesProcessed => _messagesProcessed;

	public long ProcessingErrors => _processingErrors;

	public long MessagesDeleted => _messagesDeleted;

	public long DeleteErrors => _deleteErrors;

	public double AverageProcessingTime => _messagesProcessed > 0 ? _totalProcessingTime / _messagesProcessed : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordSuccess(TimeSpan processingTime)
	{
		_ = Interlocked.Increment(ref _messagesProcessed);
		InterlockedAddDouble(ref _totalProcessingTime, processingTime.TotalMilliseconds);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordFailure(TimeSpan processingTime)
	{
		_ = Interlocked.Increment(ref _messagesProcessed);
		InterlockedAddDouble(ref _totalProcessingTime, processingTime.TotalMilliseconds);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordError(TimeSpan processingTime)
	{
		_ = Interlocked.Increment(ref _processingErrors);
		InterlockedAddDouble(ref _totalProcessingTime, processingTime.TotalMilliseconds);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordDeletes(int count) => Interlocked.Add(ref _messagesDeleted, count);

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
