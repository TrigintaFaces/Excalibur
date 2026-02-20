// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Interface for poison detection rules.
/// </summary>
public interface IPoisonDetectionRule
{
	/// <summary>
	/// Gets the name of the rule.
	/// </summary>
	/// <value>
	/// The name of the rule.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Determines if a message is poison based on the rule.
	/// </summary>
	bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history);

	/// <summary>
	/// Gets the confidence level of the detection (0-1).
	/// </summary>
	double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history);

	/// <summary>
	/// Gets the reason for the detection.
	/// </summary>
	string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history);
}
