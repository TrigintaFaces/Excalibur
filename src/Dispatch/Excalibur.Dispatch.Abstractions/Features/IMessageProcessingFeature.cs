// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for message processing state tracked across retries and redeliveries.
/// </summary>
/// <remarks>
/// Access via <c>context.Features</c> dictionary keyed by <c>typeof(IMessageProcessingFeature)</c>,
/// or use the <see cref="MessageContextFeatureExtensions.GetProcessingFeature"/> extension method.
/// </remarks>
public interface IMessageProcessingFeature
{
	/// <summary>
	/// Gets or sets the number of processing attempts for this message.
	/// </summary>
	/// <value>The number of processing attempts (0 for first attempt).</value>
	int ProcessingAttempts { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a retry attempt.
	/// </summary>
	/// <value><see langword="true"/> if this is a retry; otherwise, <see langword="false"/>.</value>
	bool IsRetry { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the first processing attempt.
	/// </summary>
	/// <value>The timestamp of the first processing attempt, or <see langword="null"/> if not yet processed.</value>
	DateTimeOffset? FirstAttemptTime { get; set; }

	/// <summary>
	/// Gets or sets the number of times this message has been delivered.
	/// </summary>
	/// <value>The number of delivery attempts.</value>
	int DeliveryCount { get; set; }
}
