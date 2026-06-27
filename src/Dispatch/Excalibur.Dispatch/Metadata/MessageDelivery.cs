// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the delivery and transport-reliability metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/>. Carries delivery counters, dead-letter details,
/// priority, durability and duplicate-detection fields. Holds at most ten properties to satisfy the
/// Microsoft-first focused-value-type design guideline.
/// </remarks>
public readonly record struct MessageDelivery
{
	/// <summary>
	/// Gets the number of delivery attempts for the message.
	/// </summary>
	/// <value> The delivery attempt count. </value>
	public int DeliveryCount { get; init; }

	/// <summary>
	/// Gets the maximum number of delivery attempts allowed.
	/// </summary>
	/// <value> The maximum delivery count or <see langword="null"/>. </value>
	public int? MaxDeliveryCount { get; init; }

	/// <summary>
	/// Gets the error message from the last delivery attempt.
	/// </summary>
	/// <value> The last delivery error or <see langword="null"/>. </value>
	public string? LastDeliveryError { get; init; }

	/// <summary>
	/// Gets the name of the dead letter queue for failed messages.
	/// </summary>
	/// <value> The dead letter queue name or <see langword="null"/>. </value>
	public string? DeadLetterQueue { get; init; }

	/// <summary>
	/// Gets the reason why the message was sent to the dead letter queue.
	/// </summary>
	/// <value> The dead letter reason or <see langword="null"/>. </value>
	public string? DeadLetterReason { get; init; }

	/// <summary>
	/// Gets the detailed error description for dead letter messages.
	/// </summary>
	/// <value> The dead letter error description or <see langword="null"/>. </value>
	public string? DeadLetterErrorDescription { get; init; }

	/// <summary>
	/// Gets the priority level of the message.
	/// </summary>
	/// <value> The priority level or <see langword="null"/>. </value>
	public int? Priority { get; init; }

	/// <summary>
	/// Gets a value indicating whether the message is durable and should survive broker restarts.
	/// </summary>
	/// <value> <see langword="true"/> if durable; <see langword="false"/> if not; <see langword="null"/> if unspecified. </value>
	public bool? Durable { get; init; }

	/// <summary>
	/// Gets a value indicating whether the message requires duplicate detection.
	/// </summary>
	/// <value> <see langword="true"/> if duplicate detection is required; <see langword="false"/> if not; <see langword="null"/> if unspecified. </value>
	public bool? RequiresDuplicateDetection { get; init; }

	/// <summary>
	/// Gets the time window for duplicate detection.
	/// </summary>
	/// <value> The duplicate detection window or <see langword="null"/>. </value>
	public TimeSpan? DuplicateDetectionWindow { get; init; }
}
