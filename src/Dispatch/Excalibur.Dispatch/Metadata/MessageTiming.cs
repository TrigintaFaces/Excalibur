// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the temporal metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/> alongside the core
/// <see cref="MessageMetadata.CreatedTimestampUtc"/> which remains on the root for the
/// <see cref="IMessageMetadata"/> contract. Carries the sent/received/scheduled timestamps,
/// time-to-live and expiration fields. Holds at most ten properties to satisfy the Microsoft-first
/// focused-value-type design guideline.
/// </remarks>
public readonly record struct MessageTiming
{
	/// <summary>
	/// Gets the UTC timestamp when the message was sent.
	/// </summary>
	/// <value> The sent timestamp or <see langword="null"/>. </value>
	public DateTimeOffset? SentTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was received.
	/// </summary>
	/// <value> The received timestamp or <see langword="null"/>. </value>
	public DateTimeOffset? ReceivedTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message is scheduled to be enqueued.
	/// </summary>
	/// <value> The scheduled enqueue timestamp or <see langword="null"/>. </value>
	public DateTimeOffset? ScheduledEnqueueTimeUtc { get; init; }

	/// <summary>
	/// Gets the time-to-live duration for the message.
	/// </summary>
	/// <value> The time-to-live duration or <see langword="null"/>. </value>
	public TimeSpan? TimeToLive { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message expires.
	/// </summary>
	/// <value> The expiration timestamp or <see langword="null"/>. </value>
	public DateTimeOffset? ExpiresAtUtc { get; init; }
}
