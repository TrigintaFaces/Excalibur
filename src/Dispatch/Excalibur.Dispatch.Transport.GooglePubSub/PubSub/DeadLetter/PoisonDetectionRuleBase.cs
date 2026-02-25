// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Base class for poison detection rules.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PoisonDetectionRuleBase" /> class. </remarks>
public abstract class PoisonDetectionRuleBase(string name) : IPoisonDetectionRule
{
	/// <inheritdoc />
	public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <inheritdoc />
	public bool IsPoison(object message, Exception exception, object history)
	{
		if (message is PubsubMessage pubsubMessage && history is MessageFailureHistory failureHistory)
		{
			return IsPoison(pubsubMessage, exception, failureHistory);
		}

		return false;
	}

	/// <inheritdoc />
	public double GetConfidence(object message, Exception exception, object history)
	{
		if (message is PubsubMessage pubsubMessage && history is MessageFailureHistory failureHistory)
		{
			return GetConfidence(pubsubMessage, exception, failureHistory);
		}

		return 0;
	}

	/// <summary>
	/// Type-specific IsPoison method for Google PubSub messages.
	/// </summary>
	public abstract bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history);

	/// <summary>
	/// Type-specific GetConfidence method for Google PubSub messages.
	/// </summary>
	public abstract double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history);

	/// <summary>
	/// Gets the reason why a message is considered poison.
	/// </summary>
	public abstract string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history);
}
