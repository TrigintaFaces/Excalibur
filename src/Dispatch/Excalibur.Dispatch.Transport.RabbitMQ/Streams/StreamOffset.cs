// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Specifies the starting offset for consuming from a RabbitMQ stream.
/// </summary>
/// <remarks>
/// <para>
/// RabbitMQ streams support multiple offset types for flexible replay and consumption:
/// <list type="bullet">
///   <item><description><see cref="First"/> - Start from the first available message.</description></item>
///   <item><description><see cref="Last"/> - Start from the last chunk (most recent).</description></item>
///   <item><description><see cref="Next"/> - Start from new messages only (after subscription).</description></item>
///   <item><description><see cref="FromOffset"/> - Start from a specific numeric offset.</description></item>
///   <item><description><see cref="FromTimestamp"/> - Start from a specific timestamp.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class StreamOffset
{
	private StreamOffset(StreamOffsetType type, long? offset = null, DateTimeOffset? timestamp = null)
	{
		Type = type;
		Offset = offset;
		Timestamp = timestamp;
	}

	/// <summary>
	/// Gets the offset type.
	/// </summary>
	public StreamOffsetType Type { get; }

	/// <summary>
	/// Gets the numeric offset value when <see cref="Type"/> is <see cref="StreamOffsetType.Offset"/>.
	/// </summary>
	/// <value>The numeric offset, or <c>null</c> if not applicable.</value>
	public long? Offset { get; }

	/// <summary>
	/// Gets the timestamp value when <see cref="Type"/> is <see cref="StreamOffsetType.Timestamp"/>.
	/// </summary>
	/// <value>The timestamp, or <c>null</c> if not applicable.</value>
	public DateTimeOffset? Timestamp { get; }

	/// <summary>
	/// Creates an offset starting from the first available message in the stream.
	/// </summary>
	/// <returns>A <see cref="StreamOffset"/> representing the first position.</returns>
	public static StreamOffset First() => new(StreamOffsetType.First);

	/// <summary>
	/// Creates an offset starting from the last chunk in the stream.
	/// </summary>
	/// <returns>A <see cref="StreamOffset"/> representing the last position.</returns>
	public static StreamOffset Last() => new(StreamOffsetType.Last);

	/// <summary>
	/// Creates an offset starting from new messages only (after subscription).
	/// </summary>
	/// <returns>A <see cref="StreamOffset"/> representing the next position.</returns>
	public static StreamOffset Next() => new(StreamOffsetType.Next);

	/// <summary>
	/// Creates an offset starting from a specific numeric offset.
	/// </summary>
	/// <param name="offset">The numeric offset to start from.</param>
	/// <returns>A <see cref="StreamOffset"/> for the specified offset.</returns>
	public static StreamOffset FromOffset(long offset) => new(StreamOffsetType.Offset, offset: offset);

	/// <summary>
	/// Creates an offset starting from a specific timestamp.
	/// </summary>
	/// <param name="timestamp">The timestamp to start from.</param>
	/// <returns>A <see cref="StreamOffset"/> for the specified timestamp.</returns>
	public static StreamOffset FromTimestamp(DateTimeOffset timestamp) => new(StreamOffsetType.Timestamp, timestamp: timestamp);
}
