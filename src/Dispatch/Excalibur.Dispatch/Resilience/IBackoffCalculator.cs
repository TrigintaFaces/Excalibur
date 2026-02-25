// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Interface for calculating retry backoff delays.
/// </summary>
public interface IBackoffCalculator
{
	/// <summary>
	/// Calculates the delay before the next retry attempt.
	/// </summary>
	/// <param name="attempt">The current attempt number (1-based).</param>
	/// <returns>The delay to wait before the next attempt.</returns>
	TimeSpan CalculateDelay(int attempt);
}
