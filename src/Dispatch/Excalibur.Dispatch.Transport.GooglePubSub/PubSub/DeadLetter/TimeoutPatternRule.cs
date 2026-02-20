// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects timeout-related poison messages.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimeoutPatternRule" /> class. </remarks>
public sealed class TimeoutPatternRule(string name, double timeoutThreshold) : PoisonDetectionRuleBase(name)
{
	private static readonly string[] TimeoutIndicators =
	[
		"timeout", "timed out", "operation canceled", "deadline exceeded", "request timeout", "taskCanceledexception",
	];

	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < 2)
		{
			return false;
		}

		var timeoutCount = history.Failures.Count(IsTimeoutException);
		var timeoutRatio = (double)timeoutCount / history.Failures.Count;

		return timeoutRatio >= timeoutThreshold;
	}

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < 2)
		{
			return 0;
		}

		var timeoutCount = history.Failures.Count(IsTimeoutException);
		var timeoutRatio = (double)timeoutCount / history.Failures.Count;

		if (timeoutRatio < timeoutThreshold)
		{
			return 0;
		}

		// High confidence for timeout patterns
		return Math.Min(0.8 + ((timeoutRatio - timeoutThreshold) * 0.5), 1.0);
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var timeoutCount = history.Failures.Count(IsTimeoutException);
		var timeoutRatio = (double)timeoutCount / history.Failures.Count;

		return $"Message frequently times out ({timeoutRatio:P0} of failures are timeout-related)";
	}

	private static bool IsTimeoutException(FailureRecord failure)
	{
		var exceptionInfo = $"{failure.ExceptionType} {failure.Message}".ToUpperInvariant();
		return TimeoutIndicators.Any(i => exceptionInfo.Contains(i.ToUpperInvariant(), StringComparison.Ordinal));
	}
}
