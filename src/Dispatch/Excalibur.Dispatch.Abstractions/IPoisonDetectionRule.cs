// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Interface for custom poison detection rules.
/// </summary>
public interface IPoisonDetectionRule
{
	/// <summary>
	/// Gets the name of the rule.
	/// </summary>
	/// <value> The unique identifier of the poison detection rule. </value>
	string Name { get; }

	/// <summary>
	/// Determines if a message is poison based on this rule.
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <param name="exception"> The exception that occurred, if any. </param>
	/// <param name="history"> The message failure history. </param>
	/// <returns> True if the message is poison; otherwise, false. </returns>
	bool IsPoison(object message, Exception exception, object history);

	/// <summary>
	/// Gets the confidence level of the detection (0-1).
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <param name="exception"> The exception that occurred, if any. </param>
	/// <param name="history"> The message failure history. </param>
	/// <returns> The confidence level between 0 and 1. </returns>
	double GetConfidence(object message, Exception exception, object history);
}
