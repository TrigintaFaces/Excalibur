// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for setting transport and delivery properties on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderTransportExtensions
{
	/// <summary>
	/// Sets the delivery count for the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="deliveryCount"> The number of delivery attempts. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDeliveryCount(this IMessageMetadataBuilder builder, int deliveryCount)
		=> builder.WithProperty(MetadataPropertyKeys.DeliveryCount, deliveryCount);

	/// <summary>
	/// Sets the maximum delivery count for the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="maxDeliveryCount"> The maximum number of delivery attempts allowed. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithMaxDeliveryCount(this IMessageMetadataBuilder builder, int? maxDeliveryCount)
		=> builder.WithProperty(MetadataPropertyKeys.MaxDeliveryCount, maxDeliveryCount);

	/// <summary>
	/// Sets the last delivery error message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="lastDeliveryError"> The error message from the last delivery attempt. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithLastDeliveryError(this IMessageMetadataBuilder builder, string? lastDeliveryError)
		=> builder.WithProperty(MetadataPropertyKeys.LastDeliveryError, lastDeliveryError);

	/// <summary>
	/// Sets the dead letter queue name.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="deadLetterQueue"> The name of the dead letter queue. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDeadLetterQueue(this IMessageMetadataBuilder builder, string? deadLetterQueue)
		=> builder.WithProperty(MetadataPropertyKeys.DeadLetterQueue, deadLetterQueue);

	/// <summary>
	/// Sets the dead letter reason.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="deadLetterReason"> The reason the message was dead-lettered. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDeadLetterReason(this IMessageMetadataBuilder builder, string? deadLetterReason)
		=> builder.WithProperty(MetadataPropertyKeys.DeadLetterReason, deadLetterReason);

	/// <summary>
	/// Sets the dead letter error description.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="deadLetterErrorDescription"> The detailed error description for dead-lettered messages. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDeadLetterErrorDescription(this IMessageMetadataBuilder builder, string? deadLetterErrorDescription)
		=> builder.WithProperty(MetadataPropertyKeys.DeadLetterErrorDescription, deadLetterErrorDescription);

	/// <summary>
	/// Sets the message priority.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="priority"> The priority level of the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithPriority(this IMessageMetadataBuilder builder, int? priority)
		=> builder.WithProperty(MetadataPropertyKeys.Priority, priority);

	/// <summary>
	/// Sets the durable flag indicating whether the message should survive broker restarts.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="durable"> True if the message should be durable. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDurable(this IMessageMetadataBuilder builder, bool? durable)
		=> builder.WithProperty(MetadataPropertyKeys.Durable, durable);

	/// <summary>
	/// Sets whether the message requires duplicate detection.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="requiresDuplicateDetection"> True if duplicate detection is required. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithRequiresDuplicateDetection(this IMessageMetadataBuilder builder, bool? requiresDuplicateDetection)
		=> builder.WithProperty(MetadataPropertyKeys.RequiresDuplicateDetection, requiresDuplicateDetection);

	/// <summary>
	/// Sets the duplicate detection window.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="duplicateDetectionWindow"> The time window for duplicate detection. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithDuplicateDetectionWindow(this IMessageMetadataBuilder builder, TimeSpan? duplicateDetectionWindow)
		=> builder.WithProperty(MetadataPropertyKeys.DuplicateDetectionWindow, duplicateDetectionWindow);
}
