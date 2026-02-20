// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects poison messages based on consistent exception patterns.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ConsistentExceptionRule" /> class. </remarks>
public sealed class ConsistentExceptionRule(string name, double consistencyThreshold) : PoisonDetectionRuleBase(name)
{
	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < 3)
		{
			return false;
		}

		var mostCommonException = history.Failures
			.GroupBy(f => f.ExceptionType, StringComparer.Ordinal)
			.OrderByDescending(g => g.Count())
			.First();

		var consistency = (double)mostCommonException.Count() / history.Failures.Count;

		return consistency >= consistencyThreshold;
	}

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < 3)
		{
			return 0;
		}

		var mostCommonException = history.Failures
			.GroupBy(f => f.ExceptionType, StringComparer.Ordinal)
			.OrderByDescending(g => g.Count())
			.First();

		var consistency = (double)mostCommonException.Count() / history.Failures.Count;

		if (consistency < consistencyThreshold)
		{
			return 0;
		}

		// Higher confidence with more consistent failures
		return Math.Min(0.6 + (consistency * 0.4), 1.0);
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var mostCommonException = history.Failures
			.GroupBy(static f => f.ExceptionType, StringComparer.Ordinal)
			.OrderByDescending(static g => g.Count())
			.First();

		var consistency = (double)mostCommonException.Count() / history.Failures.Count;

		return $"Message consistently fails with {mostCommonException.Key} ({consistency:P0} of failures)";
	}
}
