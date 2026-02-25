// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Implements an adaptive flow control strategy that dynamically adjusts flow control limits based on processing performance and system resources.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AdaptiveFlowControlStrategy" /> class. </remarks>
/// <param name="options"> The flow control options. </param>
/// <param name="metrics"> The metrics tracker. </param>
/// <param name="logger"> The logger. </param>
public sealed class AdaptiveFlowControlStrategy(
	PubSubFlowControlOptions options,
	FlowControlMetrics metrics,
	ILogger<AdaptiveFlowControlStrategy> logger)
{
	private readonly PubSubFlowControlOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly FlowControlMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly ILogger<AdaptiveFlowControlStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#if NET9_0_OR_GREATER

	private readonly Lock _adjustmentLock = new();

#else

	private readonly object _adjustmentLock = new();

#endif

	private readonly Process _currentProcess = Process.GetCurrentProcess();
	private DateTimeOffset _lastAdjustmentTime = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the current maximum number of outstanding elements.
	/// </summary>
	/// <value>
	/// The current maximum number of outstanding elements.
	/// </value>
	public int CurrentMaxElements { get; private set; } = options.MaxOutstandingElementCount;

	/// <summary>
	/// Gets the current maximum number of outstanding bytes.
	/// </summary>
	/// <value>
	/// The current maximum number of outstanding bytes.
	/// </value>
	public long CurrentMaxBytes { get; private set; } = options.MaxOutstandingByteCount;

	/// <summary>
	/// Evaluates current metrics and adjusts flow control limits if necessary.
	/// </summary>
	/// <returns> True if adjustments were made; otherwise, false. </returns>
	public bool EvaluateAndAdjust()
	{
		if (!_options.EnableAdaptiveFlowControl)
		{
			return false;
		}

		var now = DateTimeOffset.UtcNow;
		if (now - _lastAdjustmentTime < _options.AdaptationInterval)
		{
			return false;
		}

		lock (_adjustmentLock)
		{
			// Double-check the time inside the lock
			if (now - _lastAdjustmentTime < _options.AdaptationInterval)
			{
				return false;
			}

			var snapshot = _metrics.GetSnapshot();
			var memoryPressure = GetMemoryPressurePercentage();

			_logger.LogDebug(
				"Evaluating flow control: Utilization={Utilization:F2}%, " +
				"ProcessingRate={Rate:F2} msg/s, ErrorRate={ErrorRate:F2}%, " +
				"MemoryPressure={Memory:F2}%",
				snapshot.UtilizationPercentage,
				snapshot.MessageProcessingRate,
				snapshot.ErrorRate * 100,
				memoryPressure);

			var adjustmentMade = false;

			// Check if we should scale down
			if (ShouldScaleDown(snapshot, memoryPressure))
			{
				ScaleDown();
				adjustmentMade = true;
			}

			// Check if we should scale up
			else if (ShouldScaleUp(snapshot, memoryPressure))
			{
				ScaleUp();
				adjustmentMade = true;
			}

			_lastAdjustmentTime = now;
			return adjustmentMade;
		}
	}

	/// <summary>
	/// Forces a scale down of flow control limits.
	/// </summary>
	public void ForceScaleDown()
	{
		lock (_adjustmentLock)
		{
			ScaleDown();
			_lastAdjustmentTime = DateTimeOffset.UtcNow;
		}
	}

	/// <summary>
	/// Resets flow control limits to their configured values.
	/// </summary>
	public void Reset()
	{
		lock (_adjustmentLock)
		{
			CurrentMaxElements = _options.MaxOutstandingElementCount;
			CurrentMaxBytes = _options.MaxOutstandingByteCount;
			_lastAdjustmentTime = DateTimeOffset.UtcNow;

			_logger.LogInformation(
				"Flow control limits reset to configured values: " +
				"MaxElements={MaxElements}, MaxBytes={MaxBytes}",
				CurrentMaxElements,
				CurrentMaxBytes);
		}
	}

	private bool ShouldScaleDown(FlowControlMetricsSnapshot snapshot, double memoryPressure)
	{
		// Scale down if:
		// 1. Memory pressure is high
		if (memoryPressure > _options.MemoryPressureThreshold)
		{
			_logger.LogWarning(
				"High memory pressure detected: {Pressure:F2}% > {Threshold:F2}%",
				memoryPressure,
				_options.MemoryPressureThreshold);
			return true;
		}

		// 2. Utilization is too high (messages backing up)
		if (snapshot.UtilizationPercentage > _options.TargetUtilizationPercentage)
		{
			_logger.LogDebug(
				"High utilization detected: {Utilization:F2}% > {Target:F2}%",
				snapshot.UtilizationPercentage,
				_options.TargetUtilizationPercentage);
			return true;
		}

		// 3. Error rate is significant (> 5%)
		if (snapshot.ErrorRate > 0.05)
		{
			_logger.LogWarning(
				"High error rate detected: {ErrorRate:F2}%",
				snapshot.ErrorRate * 100);
			return true;
		}

		// 4. Processing rate is declining (compare to previous snapshot) This would require storing previous snapshots - simplified for now
		return false;
	}

	private bool ShouldScaleUp(FlowControlMetricsSnapshot snapshot, double memoryPressure)
	{
		// Don't scale up if memory pressure is moderate
		if (memoryPressure > _options.MemoryPressureThreshold * 0.8)
		{
			return false;
		}

		// Scale up if:
		// 1. Utilization is low (we're keeping up easily)
		if (snapshot.UtilizationPercentage < _options.TargetUtilizationPercentage * 0.5)
		{
			_logger.LogDebug(
				"Low utilization detected: {Utilization:F2}% < {Target:F2}%",
				snapshot.UtilizationPercentage,
				_options.TargetUtilizationPercentage * 0.5);
			return true;
		}

		// 2. Error rate is very low (< 1%)
		if (snapshot is { ErrorRate: < 0.01, MessagesProcessed: > 100 })
		{
			return true;
		}

		// 3. We haven't hit flow control pauses recently
		if (snapshot is { FlowControlPauses: 0, MessagesProcessed: > 1000 })
		{
			return true;
		}

		return false;
	}

	private void ScaleDown()
	{
		var newMaxElements = (int)(CurrentMaxElements * _options.ScaleDownFactor);
		var newMaxBytes = (long)(CurrentMaxBytes * _options.ScaleDownFactor);

		// Ensure we don't go below minimums
		newMaxElements = Math.Max(newMaxElements, _options.MinOutstandingElementCount);
		newMaxBytes = Math.Max(newMaxBytes, _options.MinOutstandingByteCount);

		_logger.LogInformation(
			"Scaling down flow control limits: Elements {OldElements} -> {NewElements}, " +
			"Bytes {OldBytes} -> {NewBytes}",
			CurrentMaxElements, newMaxElements,
			CurrentMaxBytes, newMaxBytes);

		CurrentMaxElements = newMaxElements;
		CurrentMaxBytes = newMaxBytes;
	}

	private void ScaleUp()
	{
		var newMaxElements = (int)(CurrentMaxElements * _options.ScaleUpFactor);
		var newMaxBytes = (long)(CurrentMaxBytes * _options.ScaleUpFactor);

		// Ensure we don't exceed configured maximums
		newMaxElements = Math.Min(newMaxElements, _options.MaxOutstandingElementCount);
		newMaxBytes = Math.Min(newMaxBytes, _options.MaxOutstandingByteCount);

		// Don't scale up if we're already at max
		if (newMaxElements == CurrentMaxElements && newMaxBytes == CurrentMaxBytes)
		{
			return;
		}

		_logger.LogInformation(
			"Scaling up flow control limits: Elements {OldElements} -> {NewElements}, " +
			"Bytes {OldBytes} -> {NewBytes}",
			CurrentMaxElements, newMaxElements,
			CurrentMaxBytes, newMaxBytes);

		CurrentMaxElements = newMaxElements;
		CurrentMaxBytes = newMaxBytes;
	}

	private double GetMemoryPressurePercentage()
	{
		try
		{
			_currentProcess.Refresh();
			var workingSet = _currentProcess.WorkingSet64;
			var totalMemory = GC.GetTotalMemory(forceFullCollection: false);

			// Get available physical memory (this is platform-specific) For now, use a simple heuristic based on GC pressure
			var gcPressure = (double)totalMemory / (1024 * 1024 * 1024); // GB

			// Assume 4GB is 100% pressure (adjust based on your environment)
			var pressure = gcPressure / 4.0 * 100;

			return Math.Min(pressure, 100);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to calculate memory pressure, assuming 50%");
			return 50.0; // Conservative default
		}
	}
}
