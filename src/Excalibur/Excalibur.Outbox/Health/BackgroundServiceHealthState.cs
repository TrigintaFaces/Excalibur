// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Health;

/// <summary>
/// Thread-safe state tracker for background service health monitoring.
/// </summary>
/// <remarks>
/// <para>
/// Background services update this state during processing cycles.
/// Health checks read from it to determine service health status.
/// </para>
/// <para>
/// Register as a singleton in DI:
/// <code>
/// services.AddSingleton&lt;BackgroundServiceHealthState&gt;();
/// </code>
/// </para>
/// </remarks>
public sealed class BackgroundServiceHealthState
{
	private long _totalProcessed;
	private long _totalFailed;
	private long _totalCycles;
	private long _lastActivityTicks;
	private int _isRunning;

	/// <summary>
	/// Gets the total number of messages processed since the service started.
	/// </summary>
	/// <value>The total processed count.</value>
	public long TotalProcessed => Interlocked.Read(ref _totalProcessed);

	/// <summary>
	/// Gets the total number of failed messages since the service started.
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
	/// Gets a value indicating whether the service is currently running.
	/// </summary>
	/// <value><see langword="true"/> if the service is running; otherwise, <see langword="false"/>.</value>
	public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

	/// <summary>
	/// Records a completed processing cycle.
	/// </summary>
	/// <param name="processedCount">The number of messages processed in this cycle.</param>
	/// <param name="failedCount">The number of messages that failed in this cycle.</param>
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
	/// Marks the service as started.
	/// </summary>
	public void MarkStarted()
	{
		_ = Interlocked.Exchange(ref _isRunning, 1);
		_ = Interlocked.Exchange(ref _lastActivityTicks, DateTimeOffset.UtcNow.Ticks);
	}

	/// <summary>
	/// Marks the service as stopped.
	/// </summary>
	public void MarkStopped()
	{
		_ = Interlocked.Exchange(ref _isRunning, 0);
	}
}
