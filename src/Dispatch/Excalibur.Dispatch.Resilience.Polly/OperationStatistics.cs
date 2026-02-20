// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Thread-safe operation statistics for tracking using atomic increments.
/// </summary>
public sealed class OperationStatistics
{
	private long _totalAttempts;
	private long _successes;
	private long _failures;
	private long _fallbackExecutions;

	/// <summary>
	/// Gets the total number of operation attempts.
	/// </summary>
	/// <value>The current total attempts value.</value>
	public long TotalAttempts => Interlocked.Read(ref _totalAttempts);

	/// <summary>
	/// Gets the number of successful operations.
	/// </summary>
	/// <value>The current successes value.</value>
	public long Successes => Interlocked.Read(ref _successes);

	/// <summary>
	/// Gets the number of failed operations.
	/// </summary>
	/// <value>The current failures value.</value>
	public long Failures => Interlocked.Read(ref _failures);

	/// <summary>
	/// Gets the number of fallback executions.
	/// </summary>
	/// <value>The current fallback executions value.</value>
	public long FallbackExecutions => Interlocked.Read(ref _fallbackExecutions);

	/// <summary>
	/// Records a new operation attempt.
	/// </summary>
	public void RecordAttempt() => Interlocked.Increment(ref _totalAttempts);

	/// <summary>
	/// Records a successful operation.
	/// </summary>
	public void RecordSuccess() => Interlocked.Increment(ref _successes);

	/// <summary>
	/// Records a failed operation.
	/// </summary>
	public void RecordFailure() => Interlocked.Increment(ref _failures);

	/// <summary>
	/// Records a fallback execution.
	/// </summary>
	public void RecordFallback() => Interlocked.Increment(ref _fallbackExecutions);

	/// <summary>
	/// Creates a snapshot clone of the current statistics.
	/// </summary>
	/// <returns>A cloned instance of the statistics.</returns>
	public OperationStatistics Clone() => new()
	{
		_totalAttempts = Interlocked.Read(ref _totalAttempts),
		_successes = Interlocked.Read(ref _successes),
		_failures = Interlocked.Read(ref _failures),
		_fallbackExecutions = Interlocked.Read(ref _fallbackExecutions),
	};
}
