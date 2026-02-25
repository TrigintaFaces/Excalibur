// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects business logic loop patterns.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BusinessLogicLoopRule" /> class. </remarks>
public sealed class BusinessLogicLoopRule(string name, int loopThreshold) : PoisonDetectionRuleBase(name)
{
	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < loopThreshold)
		{
			return false;
		}

		// Check for repeating stack trace patterns
		var stackTraceGroups = history.Failures
			.Where(f => !string.IsNullOrEmpty(f.StackTraceHash))
			.GroupBy(f => f.StackTraceHash, StringComparer.Ordinal)
			.Where(g => g.Skip(2).Any())
			.ToList();

		if (stackTraceGroups.Count != 0)
		{
			return true;
		}

		// Check for rapid repeated failures with same exception
		var recentWindow = TimeSpan.FromMinutes(5);
		var cutoff = DateTimeOffset.UtcNow - recentWindow;
		var recentSameExceptions = history.Failures
			.Where(f => f.Timestamp >= cutoff)
			.GroupBy(f => f.ExceptionType, StringComparer.Ordinal)
			.Where(g => g.Skip((loopThreshold / 2) - 1).Any())
			.ToList();

		return recentSameExceptions.Count != 0;
	}

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < loopThreshold)
		{
			return 0;
		}

		// Check for repeating patterns
		var maxRepetitions = history.Failures
			.Where(f => !string.IsNullOrEmpty(f.StackTraceHash))
			.GroupBy(f => f.StackTraceHash, StringComparer.Ordinal)
			.Select(g => g.Count())
			.DefaultIfEmpty(0)
			.Max();

		if (maxRepetitions >= 3)
		{
			return Math.Min(0.8 + ((maxRepetitions - 3) * 0.05), 1.0);
		}

		return 0.7;
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var maxRepetitionGroup = history.Failures
			.Where(static f => !string.IsNullOrEmpty(f.StackTraceHash))
			.GroupBy(static f => f.StackTraceHash, StringComparer.Ordinal)
			.OrderByDescending(static g => g.Count())
			.FirstOrDefault();

		if (maxRepetitionGroup?.Skip(2).Any() == true)
		{
			return $"Message causes repeated failures with identical stack trace ({maxRepetitionGroup.Count()} occurrences)";
		}

		return $"Message appears to be stuck in a processing loop with {history.Failures.Count} failures";
	}
}
