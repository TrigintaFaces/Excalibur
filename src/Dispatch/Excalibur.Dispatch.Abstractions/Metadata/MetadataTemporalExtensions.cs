// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing temporal metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
public static class MetadataTemporalExtensions
{
	/// <summary>
	/// Gets the UTC timestamp when this message was sent.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The sent timestamp, or null if not set. </returns>
	public static DateTimeOffset? GetSentTimestampUtc(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.SentTimestampUtc, out var value) && value is DateTimeOffset ts ? ts : null;

	/// <summary>
	/// Gets the UTC timestamp when this message was received for processing.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The received timestamp, or null if not set. </returns>
	public static DateTimeOffset? GetReceivedTimestampUtc(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ReceivedTimestampUtc, out var value) && value is DateTimeOffset ts ? ts : null;

	/// <summary>
	/// Gets the UTC timestamp when this message should be delivered.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The scheduled enqueue time, or null if not set. </returns>
	public static DateTimeOffset? GetScheduledEnqueueTimeUtc(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ScheduledEnqueueTimeUtc, out var value) && value is DateTimeOffset ts ? ts : null;

	/// <summary>
	/// Gets the time-to-live for this message.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The time-to-live, or null if not set. </returns>
	public static TimeSpan? GetTimeToLive(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.TimeToLive, out var value) && value is TimeSpan ttl ? ttl : null;

	/// <summary>
	/// Gets the UTC timestamp when this message expires.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The expiration timestamp, or null if not set. </returns>
	public static DateTimeOffset? GetExpiresAtUtc(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ExpiresAtUtc, out var value) && value is DateTimeOffset ts ? ts : null;
}
