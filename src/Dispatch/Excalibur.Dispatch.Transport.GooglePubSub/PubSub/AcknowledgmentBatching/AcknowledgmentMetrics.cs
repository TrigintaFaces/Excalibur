// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Metrics for acknowledgment batching.
/// </summary>
public sealed class AcknowledgmentMetrics
{
	private long _totalQueued;
	private long _totalAcknowledged;
	private long _totalErrors;
	private long _totalBatches;
	private long _totalDeadlineWarnings;
	private double _totalBatchTime;

	/// <summary>
	/// Gets the total number of acknowledgments queued.
	/// </summary>
	/// <value>
	/// The total number of acknowledgments queued.
	/// </value>
	public long TotalQueued => Interlocked.Read(ref _totalQueued);

	/// <summary>
	/// Gets the total number of acknowledgments sent.
	/// </summary>
	/// <value>
	/// The total number of acknowledgments sent.
	/// </value>
	public long TotalAcknowledged => Interlocked.Read(ref _totalAcknowledged);

	/// <summary>
	/// Gets the total number of errors.
	/// </summary>
	/// <value>
	/// The total number of errors.
	/// </value>
	public long TotalErrors => Interlocked.Read(ref _totalErrors);

	/// <summary>
	/// Gets the total number of batches sent.
	/// </summary>
	/// <value>
	/// The total number of batches sent.
	/// </value>
	public long TotalBatches => Interlocked.Read(ref _totalBatches);

	/// <summary>
	/// Gets the total number of deadline warnings.
	/// </summary>
	/// <value>
	/// The total number of deadline warnings.
	/// </value>
	public long TotalDeadlineWarnings => Interlocked.Read(ref _totalDeadlineWarnings);

	/// <summary>
	/// Gets the average batch send time.
	/// </summary>
	/// <value>
	/// The average batch send time.
	/// </value>
	public double AverageBatchTime =>
		TotalBatches > 0 ? _totalBatchTime / TotalBatches : 0;

	/// <inheritdoc />
	public override string ToString() =>
		$"Queued={TotalQueued}, Acknowledged={TotalAcknowledged}, " +
		$"Batches={TotalBatches}, AvgBatchTime={AverageBatchTime:F2}ms, " +
		$"DeadlineWarnings={TotalDeadlineWarnings}, Errors={TotalErrors}";

	internal void IncrementQueued() => Interlocked.Increment(ref _totalQueued);

	internal void IncrementErrors() => Interlocked.Increment(ref _totalErrors);

	internal void IncrementDeadlineWarnings(int count = 1) =>
		Interlocked.Add(ref _totalDeadlineWarnings, count);

	internal void RecordBatchSent(int count, TimeSpan elapsed)
	{
		_ = Interlocked.Add(ref _totalAcknowledged, count);
		_ = Interlocked.Increment(ref _totalBatches);

		var elapsedMs = elapsed.TotalMilliseconds;
		var currentTotal = _totalBatchTime;
		var newTotal = currentTotal + elapsedMs;

		while (Interlocked.CompareExchange(ref _totalBatchTime, newTotal, currentTotal) != currentTotal)
		{
			currentTotal = _totalBatchTime;
			newTotal = currentTotal + elapsedMs;
		}
	}

	internal AcknowledgmentMetrics Clone() => new()
	{
		_totalQueued = TotalQueued,
		_totalAcknowledged = TotalAcknowledged,
		_totalErrors = TotalErrors,
		_totalBatches = TotalBatches,
		_totalDeadlineWarnings = TotalDeadlineWarnings,
		_totalBatchTime = _totalBatchTime,
	};
}
