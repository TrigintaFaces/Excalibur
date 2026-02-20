// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Fixed-size message header for zero-copy serialization.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MessageHeader : IEquatable<MessageHeader>
{
	/// <summary>
	/// Magic value used to identify valid message headers ("EXMS" in ASCII).
	/// </summary>
	public const uint MagicValue = 0x45584D53; // "EXMS" in ASCII

	/// <summary>
	/// Magic value at the beginning of the header for format validation.
	/// </summary>
	public uint Magic;

	/// <summary>
	/// Message format version for compatibility and evolution support.
	/// </summary>
	public byte Version;

	/// <summary>
	/// Unique identifier for the message type to enable fast routing and deserialization.
	/// </summary>
	public uint TypeId;

	/// <summary>
	/// Size of the serialized payload in bytes, excluding header size.
	/// </summary>
	public int PayloadSize;

	/// <summary>
	/// Unix timestamp indicating when the message was created or serialized.
	/// </summary>
	public long Timestamp;

	/// <summary>
	/// Checksum value for data integrity verification of the payload.
	/// </summary>
	public uint Checksum;

	/// <summary>
	/// Determines whether the specified message header is equal to the current message header.
	/// </summary>
	/// <param name="other"> The message header to compare with the current message header. </param>
	/// <returns> true if the specified message header is equal to the current message header; otherwise, false. </returns>
	public readonly bool Equals(MessageHeader other) =>
		Magic == other.Magic &&
		Version == other.Version &&
		TypeId == other.TypeId &&
		PayloadSize == other.PayloadSize &&
		Timestamp == other.Timestamp &&
		Checksum == other.Checksum;

	/// <summary>
	/// Determines whether the specified object is equal to the current message header.
	/// </summary>
	/// <param name="obj"> The object to compare with the current message header. </param>
	/// <returns> true if the specified object is equal to the current message header; otherwise, false. </returns>
	public override readonly bool Equals(object? obj) => obj is MessageHeader other && Equals(other);

	/// <summary>
	/// Returns the hash code for this message header.
	/// </summary>
	/// <returns> A hash code for the current message header. </returns>
	public override readonly int GetHashCode() => HashCode.Combine(Magic, Version, TypeId, PayloadSize, Timestamp, Checksum);

	/// <summary>
	/// Determines whether two message headers are equal.
	/// </summary>
	/// <param name="left"> The first message header to compare. </param>
	/// <param name="right"> The second message header to compare. </param>
	/// <returns> true if the message headers are equal; otherwise, false. </returns>
	public static bool operator ==(MessageHeader left, MessageHeader right) => left.Equals(right);

	/// <summary>
	/// Determines whether two message headers are not equal.
	/// </summary>
	/// <param name="left"> The first message header to compare. </param>
	/// <param name="right"> The second message header to compare. </param>
	/// <returns> true if the message headers are not equal; otherwise, false. </returns>
	public static bool operator !=(MessageHeader left, MessageHeader right) => !left.Equals(right);
}
