// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for setting temporal properties on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderTemporalExtensions
{
	/// <summary>
	/// Sets the UTC timestamp when the message was created.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="createdTimestampUtc"> The UTC creation timestamp. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithCreatedTimestampUtc(this IMessageMetadataBuilder builder, DateTimeOffset createdTimestampUtc)
		=> builder.WithProperty(MetadataPropertyKeys.CreatedTimestampUtc, createdTimestampUtc);

	/// <summary>
	/// Sets the UTC timestamp when the message was sent.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="sentTimestampUtc"> The UTC sent timestamp, or null to clear. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithSentTimestampUtc(this IMessageMetadataBuilder builder, DateTimeOffset? sentTimestampUtc)
		=> builder.WithProperty(MetadataPropertyKeys.SentTimestampUtc, sentTimestampUtc);

	/// <summary>
	/// Sets the UTC timestamp when the message was received.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="receivedTimestampUtc"> The UTC received timestamp, or null to clear. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithReceivedTimestampUtc(this IMessageMetadataBuilder builder, DateTimeOffset? receivedTimestampUtc)
		=> builder.WithProperty(MetadataPropertyKeys.ReceivedTimestampUtc, receivedTimestampUtc);

	/// <summary>
	/// Sets the UTC timestamp when the message should be enqueued.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="scheduledEnqueueTimeUtc"> The UTC scheduled enqueue time, or null to clear. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithScheduledEnqueueTimeUtc(this IMessageMetadataBuilder builder, DateTimeOffset? scheduledEnqueueTimeUtc)
		=> builder.WithProperty(MetadataPropertyKeys.ScheduledEnqueueTimeUtc, scheduledEnqueueTimeUtc);

	/// <summary>
	/// Sets the time-to-live duration for the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="timeToLive"> The time-to-live duration, or null to clear. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithTimeToLive(this IMessageMetadataBuilder builder, TimeSpan? timeToLive)
		=> builder.WithProperty(MetadataPropertyKeys.TimeToLive, timeToLive);

	/// <summary>
	/// Sets the UTC timestamp when the message expires.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="expiresAtUtc"> The UTC expiration timestamp, or null to clear. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithExpiresAtUtc(this IMessageMetadataBuilder builder, DateTimeOffset? expiresAtUtc)
		=> builder.WithProperty(MetadataPropertyKeys.ExpiresAtUtc, expiresAtUtc);

	/// <summary>
	/// Sets common timing metadata in a single call.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="sentTimestampUtc"> The UTC sent timestamp. </param>
	/// <param name="receivedTimestampUtc"> The UTC received timestamp. </param>
	/// <param name="timeToLive"> Optional time-to-live duration. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithTiming(
		this IMessageMetadataBuilder builder,
		DateTimeOffset sentTimestampUtc,
		DateTimeOffset receivedTimestampUtc,
		TimeSpan? timeToLive = null)
	{
		builder
			.WithProperty(MetadataPropertyKeys.SentTimestampUtc, sentTimestampUtc)
			.WithProperty(MetadataPropertyKeys.ReceivedTimestampUtc, receivedTimestampUtc);

		if (timeToLive.HasValue)
		{
			builder.WithProperty(MetadataPropertyKeys.TimeToLive, timeToLive.Value);
		}

		return builder;
	}
}
