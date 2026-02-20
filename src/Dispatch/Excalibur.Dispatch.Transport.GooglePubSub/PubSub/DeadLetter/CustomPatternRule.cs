// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Custom rule for detecting specific business logic poison patterns.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CustomPatternRule" /> class. </remarks>
public sealed class CustomPatternRule(
	string name,
	Func<PubsubMessage, Exception, MessageFailureHistory, bool> predicate,
	Func<PubsubMessage, Exception, MessageFailureHistory, double> confidenceCalculator,
	Func<PubsubMessage, Exception, MessageFailureHistory, string> reasonProvider) : PoisonDetectionRuleBase(name)
{
	private readonly Func<PubsubMessage, Exception, MessageFailureHistory, bool> _predicate =
		predicate ?? throw new ArgumentNullException(nameof(predicate));

	private readonly Func<PubsubMessage, Exception, MessageFailureHistory, double> _confidenceCalculator =
		confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));

	private readonly Func<PubsubMessage, Exception, MessageFailureHistory, string> _reasonProvider =
		reasonProvider ?? throw new ArgumentNullException(nameof(reasonProvider));

	/// <inheritdoc />
	public override bool IsPoison(PubsubMessage message, Exception exception, MessageFailureHistory history) =>
		_predicate(message, exception, history);

	/// <inheritdoc />
	public override double GetConfidence(PubsubMessage message, Exception exception, MessageFailureHistory history) =>
		_confidenceCalculator(message, exception, history);

	/// <inheritdoc />
	public override string GetReason(PubsubMessage message, Exception exception, MessageFailureHistory history) =>
		_reasonProvider(message, exception, history);
}
