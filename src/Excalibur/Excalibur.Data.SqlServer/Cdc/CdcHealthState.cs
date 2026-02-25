// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Thread-safe state tracker for CDC processor health monitoring.
/// </summary>
/// <remarks>
/// <para>
/// The CDC processor updates this state during processing.
/// Health checks read from it to determine processor health status.
/// </para>
/// <para>
/// Register as a singleton in DI:
/// <code>
/// services.AddSingleton&lt;CdcHealthState&gt;();
/// </code>
/// </para>
/// </remarks>
public sealed class CdcHealthState
{
	private long _totalProcessed;
	private long _totalFailed;
	private long _totalCycles;
	private long _lastActivityTicks;
	private int _isRunning;

	/// <summary>
	/// Gets the total number of events processed since the processor started.
	/// </summary>
	/// <value>The total processed count.</value>
	public long TotalProcessed => Interlocked.Read(ref _totalProcessed);

	/// <summary>
	/// Gets the total number of failed events since the processor started.
	/// </summary>
	/// <value>The total failed count.</value>
	public long TotalFailed => Interlocked.Read(ref _totalFailed);

	/// <summary>
	/// Gets the total number of processing cycles completed.
	/// </summary>
	/// <value>The total cycle count.</value>
	public long TotalCycles => Interlocked.Read(ref _totalCycles);

	/// <summary>
	/// Gets the time of the last processing activity.
	/// </summary>
	/// <value>The last activity time, or null if no activity has occurred.</value>
	public DateTimeOffset? LastActivityTime
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastActivityTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
	}

	/// <summary>
	/// Gets a value indicating whether the processor is currently running.
	/// </summary>
	/// <value><see langword="true"/> if the processor is running; otherwise, <see langword="false"/>.</value>
	public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

	/// <summary>
	/// Records processed events.
	/// </summary>
	/// <param name="processedCount">The number of events processed.</param>
	/// <param name="failedCount">The number of events that failed.</param>
	public void RecordCycle(int processedCount, int failedCount)
	{
		if (processedCount > 0)
		{
			_ = Interlocked.Add(ref _totalProcessed, processedCount);
		}

		if (failedCount > 0)
		{
			_ = Interlocked.Add(ref _totalFailed, failedCount);
		}

		_ = Interlocked.Increment(ref _totalCycles);
		_ = Interlocked.Exchange(ref _lastActivityTicks, DateTimeOffset.UtcNow.Ticks);
	}

	/// <summary>
	/// Marks the processor as started.
	/// </summary>
	public void MarkStarted()
	{
		_ = Interlocked.Exchange(ref _isRunning, 1);
		_ = Interlocked.Exchange(ref _lastActivityTicks, DateTimeOffset.UtcNow.Ticks);
	}

	/// <summary>
	/// Marks the processor as stopped.
	/// </summary>
	public void MarkStopped()
	{
		_ = Interlocked.Exchange(ref _isRunning, 0);
	}
}
