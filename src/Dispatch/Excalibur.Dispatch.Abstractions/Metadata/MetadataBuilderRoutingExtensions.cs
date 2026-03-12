// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for setting routing properties on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderRoutingExtensions
{
	/// <summary>
	/// Sets the source system or component that originated the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="source"> The identifier of the source system. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithSource(this IMessageMetadataBuilder builder, string? source)
		=> builder.WithProperty(MetadataPropertyKeys.Source, source);

	/// <summary>
	/// Sets the destination system or component for the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="destination"> The identifier of the destination system. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDestination(this IMessageMetadataBuilder builder, string? destination)
		=> builder.WithProperty(MetadataPropertyKeys.Destination, destination);

	/// <summary>
	/// Sets the reply-to address for request-response patterns.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="replyTo"> The reply-to address or identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithReplyTo(this IMessageMetadataBuilder builder, string? replyTo)
		=> builder.WithProperty(MetadataPropertyKeys.ReplyTo, replyTo);

	/// <summary>
	/// Sets the session identifier for message grouping.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="sessionId"> The session identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithSessionId(this IMessageMetadataBuilder builder, string? sessionId)
		=> builder.WithProperty(MetadataPropertyKeys.SessionId, sessionId);

	/// <summary>
	/// Sets the partition key for message routing and ordering.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="partitionKey"> The partition key value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithPartitionKey(this IMessageMetadataBuilder builder, string? partitionKey)
		=> builder.WithProperty(MetadataPropertyKeys.PartitionKey, partitionKey);

	/// <summary>
	/// Sets the routing key for message routing.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="routingKey"> The key used for message routing decisions. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithRoutingKey(this IMessageMetadataBuilder builder, string? routingKey)
		=> builder.WithProperty(MetadataPropertyKeys.RoutingKey, routingKey);

	/// <summary>
	/// Sets the group identifier for message grouping.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="groupId"> The identifier for the message group. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithGroupId(this IMessageMetadataBuilder builder, string? groupId)
		=> builder.WithProperty(MetadataPropertyKeys.GroupId, groupId);

	/// <summary>
	/// Sets the sequence number within the message group.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="groupSequence"> The sequence number within the group. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithGroupSequence(this IMessageMetadataBuilder builder, long? groupSequence)
		=> builder.WithProperty(MetadataPropertyKeys.GroupSequence, groupSequence);

	/// <summary>
	/// Sets the message type identifier.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="messageType"> The type identifier for the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithMessageType(this IMessageMetadataBuilder builder, string messageType)
		=> builder.WithProperty(MetadataPropertyKeys.MessageType, messageType);

	/// <summary>
	/// Sets the content type of the message payload.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="contentType"> The MIME type or content type identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithContentType(this IMessageMetadataBuilder builder, string contentType)
		=> builder.WithProperty(MetadataPropertyKeys.ContentType, contentType);
}
