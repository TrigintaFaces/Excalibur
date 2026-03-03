// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

namespace Excalibur.Dispatch.Delivery;

internal static class PollingIntervalCalculator
{
	public static TimeSpan GetInitialInterval(bool enableAdaptivePolling, TimeSpan minInterval, TimeSpan defaultInterval)
	{
		return enableAdaptivePolling ? minInterval : defaultInterval;
	}

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
