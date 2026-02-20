// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.GooglePubSub;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// High-performance logging extensions for Google Pub/Sub channel receiver.
/// </summary>
internal static partial class GooglePubSubChannelReceiverLogging
{
	// Source-generated logging methods
	[LoggerMessage(GooglePubSubEventId.BatchProduced, LogLevel.Debug,
		"Produced batch of {Count} messages from Google Pub/Sub")]
	public static partial void LogBatchProduced(this ILogger logger, int count);

	[LoggerMessage(GooglePubSubEventId.AckDeadlineExtended, LogLevel.Debug,
		"Extended acknowledgment deadline for {Count} messages")]
	public static partial void LogAckDeadlineExtended(this ILogger logger, int count);

	[LoggerMessage(GooglePubSubEventId.AckDeadlineExtensionFailed, LogLevel.Warning,
		"Failed to extend acknowledgment deadline for {Count} messages")]
	public static partial void LogAckDeadlineExtensionFailed(this ILogger logger, int count, Exception exception);

	[LoggerMessage(GooglePubSubEventId.StreamingPullConnectionStarted, LogLevel.Information,
		"Google Pub/Sub streaming pull connection established for subscription '{SubscriptionId}'")]
	public static partial void LogStreamingPullStarted(this ILogger logger, string subscriptionId);

	[LoggerMessage(GooglePubSubEventId.StreamingPullReconnecting, LogLevel.Warning,
		"Google Pub/Sub streaming pull connection lost for subscription '{SubscriptionId}', reconnecting...")]
	public static partial void LogStreamingPullReconnecting(this ILogger logger, string subscriptionId);
}
