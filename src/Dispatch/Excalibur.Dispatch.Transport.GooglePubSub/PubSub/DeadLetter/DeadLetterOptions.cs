// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for dead letter queue handling.
/// </summary>
public sealed class DeadLetterOptions
{
	/// <summary>
	/// Gets or sets the dead letter topic name.
	/// </summary>
	/// <value>
	/// The dead letter topic name.
	/// </value>
	public TopicName? DeadLetterTopicName { get; set; }

	/// <summary>
	/// Gets or sets the dead letter subscription name used for reading DLQ messages and statistics.
	/// </summary>
	/// <value>
	/// The dead letter subscription name.
	/// </value>
	public SubscriptionName? DeadLetterSubscriptionName { get; set; }

	/// <summary>
	/// Gets custom dead letter reasons that should skip retry.
	/// </summary>
	/// <value>
	/// Custom dead letter reasons that should skip retry.
	/// </value>
	public HashSet<string> NonRetryableReasons { get; } =
	[
		"INVALID_MESSAGE_FORMAT",
		"UNAUTHORIZED",
		"MESSAGE_TOO_LARGE",
		"UNSUPPORTED_OPERATION",
	];
}
