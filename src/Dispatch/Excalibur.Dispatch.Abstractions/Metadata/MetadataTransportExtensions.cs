// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing transport and delivery metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
public static class MetadataTransportExtensions
{
	/// <summary>
	/// Gets the number of times this message has been delivered.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The delivery count, or 0 if not set. </returns>
	public static int GetDeliveryCount(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.DeliveryCount, out var value) && value is int count ? count : 0;

	/// <summary>
	/// Gets the maximum number of delivery attempts allowed.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The maximum delivery count, or null if not set. </returns>
	public static int? GetMaxDeliveryCount(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.MaxDeliveryCount, out var value) && value is int count ? count : null;

	/// <summary>
	/// Gets the reason for the last delivery failure.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The last delivery error, or null if not set. </returns>
	public static string? GetLastDeliveryError(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.LastDeliveryError, out var value) ? value as string : null;

	/// <summary>
	/// Gets the name of the dead-letter queue for this message.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The dead letter queue name, or null if not set. </returns>
	public static string? GetDeadLetterQueue(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.DeadLetterQueue, out var value) ? value as string : null;

	/// <summary>
	/// Gets the reason why the message was dead-lettered.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The dead letter reason, or null if not set. </returns>
	public static string? GetDeadLetterReason(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.DeadLetterReason, out var value) ? value as string : null;

	/// <summary>
	/// Gets the error description for dead-lettered messages.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The dead letter error description, or null if not set. </returns>
	public static string? GetDeadLetterErrorDescription(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.DeadLetterErrorDescription, out var value) ? value as string : null;

	/// <summary>
	/// Gets the priority level of this message.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The priority level, or null if not set. </returns>
	public static int? GetPriority(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Priority, out var value) && value is int priority ? priority : null;

	/// <summary>
	/// Gets a value indicating whether this message requires durable storage.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The durable flag, or null if not set. </returns>
	public static bool? GetDurable(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Durable, out var value) && value is bool durable ? durable : null;

	/// <summary>
	/// Gets a value indicating whether duplicate detection should be applied.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The duplicate detection flag, or null if not set. </returns>
	public static bool? GetRequiresDuplicateDetection(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.RequiresDuplicateDetection, out var value) && value is bool flag ? flag : null;

	/// <summary>
	/// Gets the window for duplicate detection.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The duplicate detection window, or null if not set. </returns>
	public static TimeSpan? GetDuplicateDetectionWindow(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.DuplicateDetectionWindow, out var value) && value is TimeSpan window ? window : null;
}
