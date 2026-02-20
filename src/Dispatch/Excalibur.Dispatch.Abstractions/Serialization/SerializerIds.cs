// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Well-known serializer IDs assigned by the framework for pluggable serialization.
/// </summary>
/// <remarks>
/// <para>
/// This class defines the magic byte values used to identify serializers in persisted payloads.
/// Each payload is prefixed with a single byte identifying the serializer used, enabling:
/// </para>
/// <list type="bullet">
///   <item>Deterministic format detection without exception-based sniffing</item>
///   <item>Graceful migration between serializers</item>
///   <item>Support for multiple serializers in the same system</item>
/// </list>
/// <para>
/// <b>ID Ranges:</b>
/// </para>
/// <list type="bullet">
///   <item><b>1-199:</b> Framework-assigned serializers (reserved by Excalibur.Dispatch)</item>
///   <item><b>200-254:</b> Custom consumer-defined serializers</item>
///   <item><b>0:</b> Invalid/uninitialized (reserved for error detection)</item>
///   <item><b>255:</b> Unknown/fallback (reserved for future use)</item>
/// </list>
/// <para>
/// See the serialization architecture documentation for the full design rationale.
/// </para>
/// </remarks>
public static class SerializerIds
{
	// ===== Built-in Serializers (Framework-Assigned) =====

	/// <summary>
	/// MemoryPack - High-performance binary serializer (default).
	/// </summary>
	/// <remarks>
	/// MemoryPack provides the fastest serialization for .NET-only environments.
	/// This is the default serializer when no explicit configuration is provided.
	/// </remarks>
	public const byte MemoryPack = 1;

	/// <summary>
	/// System.Text.Json - Human-readable JSON serializer.
	/// </summary>
	/// <remarks>
	/// Useful for debugging and development scenarios where human-readable
	/// payloads are preferred over performance.
	/// </remarks>
	public const byte SystemTextJson = 2;

	/// <summary>
	/// MessagePack - Compact binary serializer (cross-language).
	/// </summary>
	/// <remarks>
	/// MessagePack provides good performance with cross-language compatibility.
	/// Suitable for polyglot environments with Python, Go, or other language consumers.
	/// </remarks>
	public const byte MessagePack = 3;

	/// <summary>
	/// Protobuf - Protocol Buffers (schema-based, cross-language).
	/// </summary>
	/// <remarks>
	/// Protocol Buffers provide schema-based serialization with excellent
	/// cross-language support and backward compatibility guarantees.
	/// </remarks>
	public const byte Protobuf = 4;

	// ===== Reserved Range =====

	/// <summary>
	/// Start of framework-reserved ID range.
	/// </summary>
	/// <remarks>
	/// IDs 5-199 are reserved for future framework-assigned serializers.
	/// Do not use these IDs for custom serializers.
	/// </remarks>
	public const byte FrameworkReservedStart = 5;

	/// <summary>
	/// End of framework-reserved ID range.
	/// </summary>
	public const byte FrameworkReservedEnd = 199;

	// ===== Custom Serializer Range =====

	/// <summary>
	/// Start of custom serializer ID range (consumer-assigned).
	/// </summary>
	/// <remarks>
	/// IDs 200-254 are available for custom consumer-defined serializers.
	/// When registering a custom serializer, use an ID in this range.
	/// </remarks>
	public const byte CustomRangeStart = 200;

	/// <summary>
	/// End of custom serializer ID range (consumer-assigned).
	/// </summary>
	public const byte CustomRangeEnd = 254;

	// ===== Special Values =====

	/// <summary>
	/// Invalid/uninitialized serializer ID.
	/// </summary>
	/// <remarks>
	/// This value indicates an error condition or uninitialized state.
	/// Payloads with this ID should be rejected.
	/// </remarks>
	public const byte Invalid = 0;

	/// <summary>
	/// Unknown serializer (fallback/error indicator).
	/// </summary>
	/// <remarks>
	/// Reserved for future use as a fallback or error indicator.
	/// Payloads with this ID should be rejected.
	/// </remarks>
	public const byte Unknown = 255;

	/// <summary>
	/// Validates whether a serializer ID is in the valid range for registration.
	/// </summary>
	/// <param name="id">The serializer ID to validate.</param>
	/// <returns>True if the ID is valid for registration (1-254), false otherwise.</returns>
	public static bool IsValidId(byte id) => id is > Invalid and < Unknown;

	/// <summary>
	/// Validates whether a serializer ID is in the framework-assigned range.
	/// </summary>
	/// <param name="id">The serializer ID to validate.</param>
	/// <returns>True if the ID is framework-assigned (1-199), false otherwise.</returns>
	public static bool IsFrameworkId(byte id) => id is >= MemoryPack and <= FrameworkReservedEnd;

	/// <summary>
	/// Validates whether a serializer ID is in the custom range.
	/// </summary>
	/// <param name="id">The serializer ID to validate.</param>
	/// <returns>True if the ID is in the custom range (200-254), false otherwise.</returns>
	public static bool IsCustomId(byte id) => id is >= CustomRangeStart and <= CustomRangeEnd;

	/// <summary>
	/// Checks if a byte represents a valid magic byte for our serialization format.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method is used for hybrid format detection in transport deserialization.
	/// Valid magic bytes are in the range 1-254 (our serializer IDs).
	/// </para>
	/// <para>
	/// <b>Detection Order for Transport Messages:</b>
	/// </para>
	/// <list type="number">
	///   <item>Our magic bytes (1-254) → Use IPayloadSerializer</item>
	///   <item>Confluent format (0x00 + 4 bytes) → Skip header, try JSON</item>
	///   <item>Raw JSON (0x7B or 0x5B) → Direct JSON deserialize</item>
	///   <item>Unknown → Throw with diagnostic info</item>
	/// </list>
	/// <para>
	/// Note: 0x00 is reserved for Confluent Schema Registry format detection
	/// The transport layer detects this format and
	/// handles it specially for Kafka interoperability.
	/// </para>
	/// </remarks>
	/// <param name="firstByte">The first byte of the payload.</param>
	/// <returns>True if this byte represents one of our serializer IDs.</returns>
	public static bool IsValidSerializerId(byte firstByte) => IsValidId(firstByte);
}
