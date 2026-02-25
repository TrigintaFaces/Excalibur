// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects poison messages based on failure count threshold.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ThresholdRule" /> class. </remarks>
public sealed class ThresholdRule(string name, int threshold) : PoisonDetectionRuleBase(name)
{
	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history) =>
		history.Failures.Count >= threshold;

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history)
	{
		if (history.Failures.Count < threshold)
		{
			return 0;
		}

		// Higher confidence with more failures beyond threshold
		var excessFailures = history.Failures.Count - threshold;
		return Math.Min(0.7 + (excessFailures * 0.05), 1.0);
	}

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history) =>
		$"Message has failed {history.Failures.Count} times, exceeding threshold of {threshold}";
}
