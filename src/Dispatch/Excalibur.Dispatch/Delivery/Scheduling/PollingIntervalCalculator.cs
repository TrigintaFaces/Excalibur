// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;

namespace Excalibur.Dispatch.Delivery;

internal static class PollingIntervalCalculator
{
	public static TimeSpan GetInitialInterval(bool enableAdaptivePolling, TimeSpan minInterval, TimeSpan defaultInterval)
	{
		return enableAdaptivePolling ? minInterval : defaultInterval;
	}

	/// <summary>
	/// Computes the interval to wait before the next poll.
	/// </summary>
	/// <remarks>
	/// <paramref name="maxInterval"/> is the STEADY-STATE interval, and it is named for its role in
	/// adaptive mode, where it caps the backoff. With adaptive polling off there is no backoff to cap, so
	/// it is simply the interval the caller polls at — the scheduler passes its configured poll interval
	/// for this argument, which is also what the initial interval resolves to. The two agree.
	/// <para>
	/// Stated because reading this method alone invites the opposite conclusion: the non-adaptive return
	/// looks like it substitutes some unrelated maximum for the configured interval, which would mean one
	/// prompt poll followed by a much slower one forever. It does not, and there is no such separate
	/// maximum setting to substitute. A reader who checks only this file has been misled by the name.
	/// </para>
	/// </remarks>
	public static TimeSpan GetNextInterval(
		TimeSpan currentInterval,
		bool hadWork,
		bool enableAdaptivePolling,
		TimeSpan minInterval,
		TimeSpan maxInterval,
		double backoffMultiplier)
	{
		if (!enableAdaptivePolling)
		{
			return maxInterval;
		}

		if (hadWork)
		{
			return minInterval;
		}

		var safeMultiplier = backoffMultiplier > 1.0 ? backoffMultiplier : 1.0;
		var nextInterval = TimeSpan.FromMilliseconds(currentInterval.TotalMilliseconds * safeMultiplier);
		return nextInterval < maxInterval ? nextInterval : maxInterval;
	}

	public static TimeSpan ApplyJitter(TimeSpan interval, double jitterRatio)
	{
		if (jitterRatio <= 0)
		{
			return interval;
		}

		var normalizedJitter = jitterRatio > 1.0 ? 1.0 : jitterRatio;
		var jitterSample = RandomNumberGenerator.GetInt32(-1_000_000, 1_000_001) / 1_000_000.0;
		var jitterFactor = 1.0 + (jitterSample * normalizedJitter);
		var jitteredMs = interval.TotalMilliseconds * jitterFactor;
		if (jitteredMs < 1)
		{
			jitteredMs = 1;
		}

		return TimeSpan.FromMilliseconds(jitteredMs);
	}
}
