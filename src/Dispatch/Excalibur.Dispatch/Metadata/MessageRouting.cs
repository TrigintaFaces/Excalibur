// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the routing and addressing metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/>. Carries destination, reply, session, partition,
/// routing-key and grouping fields. Holds at most ten properties to satisfy the Microsoft-first
/// focused-value-type design guideline.
/// </remarks>
public readonly record struct MessageRouting
{
	/// <summary>
	/// Gets the destination for the message.
	/// </summary>
	/// <value> The destination or <see langword="null"/>. </value>
	public string? Destination { get; init; }

	/// <summary>
	/// Gets the reply-to address for the message.
	/// </summary>
	/// <value> The reply-to address or <see langword="null"/>. </value>
	public string? ReplyTo { get; init; }

	/// <summary>
	/// Gets the session identifier for the message.
	/// </summary>
	/// <value> The session identifier or <see langword="null"/>. </value>
	public string? SessionId { get; init; }

	/// <summary>
	/// Gets the partition key for message distribution.
	/// </summary>
	/// <value> The partition key or <see langword="null"/>. </value>
	public string? PartitionKey { get; init; }

	/// <summary>
	/// Gets the routing key for message routing decisions.
	/// </summary>
	/// <value> The routing key or <see langword="null"/>. </value>
	public string? RoutingKey { get; init; }

	/// <summary>
	/// Gets the group identifier for message grouping.
	/// </summary>
	/// <value> The group identifier or <see langword="null"/>. </value>
	public string? GroupId { get; init; }

	/// <summary>
	/// Gets the sequence number within the message group.
	/// </summary>
	/// <value> The group sequence number or <see langword="null"/>. </value>
	public long? GroupSequence { get; init; }
}
