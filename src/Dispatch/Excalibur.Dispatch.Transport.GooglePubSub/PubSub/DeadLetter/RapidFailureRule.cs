// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects poison messages based on rapid failure rate.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RapidFailureRule" /> class. </remarks>
public sealed class RapidFailureRule(string name, int failureCount, TimeSpan timeWindow) : PoisonDetectionRuleBase(name)
{
	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < failureCount)
		{
			return false;
		}

		var cutoff = DateTimeOffset.UtcNow - timeWindow;
		var recentFailures = history.Failures.Count(f => f.Timestamp >= cutoff);

		return recentFailures >= failureCount;
	}

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var cutoff = DateTimeOffset.UtcNow - timeWindow;
		var recentFailures = history.Failures.Count(f => f.Timestamp >= cutoff);

		if (recentFailures < failureCount)
		{
			return 0;
		}

		// Very high confidence for rapid failures
		return Math.Min(0.85 + ((recentFailures - failureCount) * 0.05), 1.0);
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var cutoff = DateTimeOffset.UtcNow - timeWindow;
		var recentFailures = history.Failures.Count(f => f.Timestamp >= cutoff);

		return $"Message has failed {recentFailures} times within {timeWindow.TotalMinutes:F0} minutes";
	}
}
