// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Monitors system load for adaptive caching decisions.
/// </summary>
public interface ISystemLoadMonitor
{
	/// <summary>
	/// Gets the current system load as a value between 0.0 and 1.0.
	/// </summary>
	/// <returns> The current system load. </returns>
	Task<double> GetCurrentLoadAsync();
}
