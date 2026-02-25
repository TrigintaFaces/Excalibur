// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Interface for observing pattern state changes.
/// </summary>
public interface IPatternObserver
{
	/// <summary>
	/// Called when a pattern's state changes.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task OnPatternStateChangedAsync(ICloudNativePattern pattern, PatternStateChange stateChange);

	/// <summary>
	/// Called when a pattern's metrics are updated.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task OnPatternMetricsUpdatedAsync(ICloudNativePattern pattern, PatternMetrics metrics);

	/// <summary>
	/// Called when a pattern experiences an error.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task OnPatternErrorAsync(ICloudNativePattern pattern, Exception exception);
}
