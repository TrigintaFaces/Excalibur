// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects resource exhaustion patterns.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ResourceExhaustionRule" /> class. </remarks>
public sealed class ResourceExhaustionRule(string name) : PoisonDetectionRuleBase(name)
{
	private static readonly string[] ResourceIndicators =
	[
		"outofmemory", "insufficient memory", "memory allocation", "stackoverflowexception", "thread pool", "too many open files",
		"socket exhaustion", "connection pool", "quota exceeded", "rate limit",
	];

	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history) =>
		history.Failures.Exists(IsResourceException);

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var resourceFailures = history.Failures.Count(IsResourceException);

		if (resourceFailures == 0)
		{
			return 0;
		}

		// Very high confidence for resource exhaustion
		return Math.Min(0.9 + ((resourceFailures - 1) * 0.05), 1.0);
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var resourceFailure = history.Failures.First(IsResourceException);
		return $"Message causes resource exhaustion: {resourceFailure.ExceptionType}";
	}

	private static bool IsResourceException(FailureRecord failure)
	{
		var exceptionInfo = $"{failure.ExceptionType} {failure.Message}".ToUpperInvariant();
		return ResourceIndicators.Any(i => exceptionInfo.Contains(i.ToUpperInvariant(), StringComparison.Ordinal));
	}
}
