// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing routing metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
public static class MetadataRoutingExtensions
{
	/// <summary>
	/// Gets the intended destination for this message.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The destination, or null if not set. </returns>
	public static string? GetDestination(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Destination, out var value) ? value as string : null;

	/// <summary>
	/// Gets the address where responses to this message should be sent.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The reply-to address, or null if not set. </returns>
	public static string? GetReplyTo(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ReplyTo, out var value) ? value as string : null;

	/// <summary>
	/// Gets the session identifier for ordered message processing.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The session identifier, or null if not set. </returns>
	public static string? GetSessionId(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.SessionId, out var value) ? value as string : null;

	/// <summary>
	/// Gets the partition key for message ordering and distribution.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The partition key, or null if not set. </returns>
	public static string? GetPartitionKey(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.PartitionKey, out var value) ? value as string : null;

	/// <summary>
	/// Gets the routing key for topic-based routing.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The routing key, or null if not set. </returns>
	public static string? GetRoutingKey(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.RoutingKey, out var value) ? value as string : null;

	/// <summary>
	/// Gets the group identifier for message grouping.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The group identifier, or null if not set. </returns>
	public static string? GetGroupId(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.GroupId, out var value) ? value as string : null;

	/// <summary>
	/// Gets the sequence number within a group or session.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The group sequence number, or null if not set. </returns>
	public static long? GetGroupSequence(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.GroupSequence, out var value) && value is long seq ? seq : null;
}
