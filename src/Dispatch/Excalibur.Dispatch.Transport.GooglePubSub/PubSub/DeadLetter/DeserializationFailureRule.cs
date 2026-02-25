// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects deserialization failure patterns.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DeserializationFailureRule" /> class. </remarks>
public sealed class DeserializationFailureRule(string name) : PoisonDetectionRuleBase(name)
{
	private static readonly string[] DeserializationIndicators =
	[
		"jsonexception", "deserialize", "parse error", "invalid json", "unexpected character", "malformed", "invalid format",
		"serialization", "cannot convert", "type mismatch", "schema validation",
	];

	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history) =>

		// If any failure is deserialization-related, it's likely permanent
		history.Failures.Exists(IsDeserializationException);

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var deserializationFailures = history.Failures.Count(IsDeserializationException);

		if (deserializationFailures == 0)
		{
			return 0;
		}

		// Very high confidence - deserialization failures are typically permanent
		return 0.95;
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		var deserializationFailure = history.Failures.First(IsDeserializationException);
		return $"Message has invalid format: {deserializationFailure.Message}";
	}

	private static bool IsDeserializationException(FailureRecord failure)
	{
		var exceptionInfo = $"{failure.ExceptionType} {failure.Message}".ToUpperInvariant();
		return DeserializationIndicators.Any(i => exceptionInfo.Contains(i.ToUpperInvariant(), StringComparison.Ordinal));
	}
}
