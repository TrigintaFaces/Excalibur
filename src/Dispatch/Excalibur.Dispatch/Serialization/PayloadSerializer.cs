// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Default implementation of <see cref="IPayloadSerializer"/> using magic byte format detection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the primary serialization facade for internal persistence stores
/// (Outbox, Inbox, Event Store). It handles:
/// </para>
/// <list type="bullet">
///   <item>Prepending the serializer ID as a magic byte during serialization</item>
///   <item>Detecting the format via magic byte during deserialization</item>
///   <item>Routing to the appropriate <see cref="IPluggableSerializer"/> implementation</item>
/// </list>
/// <para>
/// <b>Performance Characteristics:</b>
/// </para>
/// <list type="bullet">
///   <item><b>Serialize:</b> ~2ns overhead (magic byte prepend via Buffer.BlockCopy)</item>
///   <item><b>Deserialize (fast path):</b> &lt;1ns overhead (current serializer match)</item>
///   <item><b>Deserialize (migration path):</b> ~5ns overhead (registry lookup)</item>
/// </list>
/// <para>
/// See the pluggable serialization architecture documentation for the magic byte format.
/// </para>
/// </remarks>
public sealed partial class PayloadSerializer : IPayloadSerializer
{
	/// <summary>
	/// Default JSON options for fallback deserialization of raw JSON from external systems.
	/// </summary>
	/// <remarks>
	/// Uses PropertyNameCaseInsensitive = true to handle both camelCase and PascalCase
	/// property naming from various external systems.
	/// </remarks>
	private static readonly JsonSerializerOptions DefaultJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly ISerializerRegistry _registry;
	private readonly ILogger<PayloadSerializer> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PayloadSerializer"/> class.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <param name="logger">The logger instance.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when registry or logger is null.
	/// </exception>
	public PayloadSerializer(
		ISerializerRegistry registry,
		ILogger<PayloadSerializer> logger)
		: this(registry, logger, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PayloadSerializer"/> class
	/// with custom JSON options for fallback deserialization.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="jsonOptions">
	/// Custom JSON serializer options for fallback deserialization of raw JSON
	/// from external systems. If null, defaults with PropertyNameCaseInsensitive
	/// and camelCase naming policy are used.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when registry or logger is null.
	/// </exception>
	public PayloadSerializer(
		ISerializerRegistry registry,
		ILogger<PayloadSerializer> logger,
		JsonSerializerOptions? jsonOptions)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_jsonOptions = jsonOptions ?? DefaultJsonOptions;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "Serialization is opt-in via configured serializers; trimming-safe usage requires explicit registration.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Serializer implementations may require dynamic code generation; AOT users must select compatible serializers.")]
	public byte[] Serialize<T>(T value)
	{
		ArgumentNullException.ThrowIfNull(value);

		var (currentId, currentSerializer) = _registry.GetCurrent();

		byte[] payload;
		try
		{
			payload = currentSerializer.Serialize(value);
		}
		catch (Exception ex) when (ex is not SerializationException)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}

		// Prepend magic byte
		var result = new byte[payload.Length + 1];
		result[0] = currentId;
		Buffer.BlockCopy(payload, 0, result, 1, payload.Length);

		return result;
	}

	/// <inheritdoc />
	public T Deserialize<T>(byte[] data)
	{
		// Validation
		ArgumentNullException.ThrowIfNull(data);
		if (data.Length == 0)
		{
			throw SerializationException.EmptyPayload();
		}

		// Extract magic byte
		var serializerId = data[0];
		var payload = data.AsSpan(1); // Zero-allocation slice

		// Fast path: current serializer (99.9% of cases)
		var (currentId, currentSerializer) = _registry.GetCurrent();
		if (serializerId == currentId)
		{
			return DeserializeWithSerializer<T>(currentSerializer, payload);
		}

		// Migration path: registered legacy serializer
		var legacySerializer = _registry.GetById(serializerId)
			?? throw SerializationException.UnknownSerializerId(
				serializerId,
				GetRegisteredSerializerNames());

		LogLegacySerializerSelected(
				legacySerializer.Name,
				serializerId,
				currentSerializer.Name);

		return DeserializeWithSerializer<T>(legacySerializer, payload);
	}

	/// <inheritdoc />
	public byte GetCurrentSerializerId()
	{
		var (id, _) = _registry.GetCurrent();
		return id;
	}

	/// <inheritdoc />
	public string GetCurrentSerializerName()
	{
		var (_, serializer) = _registry.GetCurrent();
		return serializer.Name;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "Runtime type serialization is required for migration and persistence scenarios.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Runtime type serialization may require dynamic code generation; AOT users must select compatible serializers.")]
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		var (currentId, currentSerializer) = _registry.GetCurrent();

		byte[] payload;
		try
		{
			payload = currentSerializer.SerializeObject(value, type);
		}
		catch (Exception ex) when (ex is not SerializationException)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}

		// Prepend magic byte
		var result = new byte[payload.Length + 1];
		result[0] = currentId;
		Buffer.BlockCopy(payload, 0, result, 1, payload.Length);

		return result;
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b>Hybrid Format Detection Strategy:</b>
	/// </para>
	/// <list type="number">
	///   <item>
	///     <b>Our format (internal):</b> Magic byte 1-254 with a registered serializer.
	///     Uses <see cref="ISerializerRegistry.IsRegistered(byte)"/> to confirm.
	///   </item>
	///   <item>
	///     <b>Confluent Schema Registry:</b> Byte 0x00 followed by 4-byte schema ID.
	///     Falls back to JSON if payload starts with '{' or '['.
	///   </item>
	///   <item>
	///     <b>Raw JSON (external):</b> Payload starts with '{' (0x7B) or '[' (0x5B).
	///     Used for interoperability with external Kafka/RabbitMQ producers.
	///   </item>
	///   <item>
	///     <b>Unknown format:</b> Throws <see cref="SerializationException"/>.
	///   </item>
	/// </list>
	/// <para>
	/// <b>Important:</b> Raw JSON detection works because we check if a serializer is
	/// actually registered for the ID, not just if the ID is in the valid range (1-254).
	/// This allows bytes like 0x7B (123 = '{') and 0x5B (91 = '[') to fall through to
	/// JSON detection when no serializer is registered at those IDs.
	/// </para>
	/// </remarks>
	public T DeserializeTransportMessage<T>(byte[] data)
	{
		// Validation
		ArgumentNullException.ThrowIfNull(data);
		if (data.Length == 0)
		{
			throw SerializationException.EmptyPayload();
		}

		var firstByte = data[0];

		// 1. Our format (internal messages) - magic byte 1-254 with REGISTERED serializer
		// Key fix: Check IsRegistered() to allow raw JSON bytes (0x7B='{', 0x5B='[') to fall through
		if (SerializerIds.IsValidSerializerId(firstByte) && _registry.IsRegistered(firstByte))
		{
			return Deserialize<T>(data);
		}

		// 2. Confluent Schema Registry format - 0x00 + 4-byte schema ID
		if (firstByte == SerializerIds.Invalid && data.Length >= 5)
		{
			// Skip 5-byte header [0x00][4-byte schema ID]
			var payload = data.AsSpan(5);
			if (payload.Length > 0 && (payload[0] == 0x7B || payload[0] == 0x5B)) // { or [
			{
				LogSchemaRegistryFormatDetected();
				return DeserializeJson<T>(payload);
			}

			// Non-JSON Schema Registry payload (Avro/Protobuf)
			throw SerializationException.FormatNotSupported(
				"Confluent Schema Registry Avro/Protobuf",
				"Install Confluent Schema Registry integration (future feature).");
		}

		// 3. Raw JSON (external systems) - { or [
		// Now reachable! Bytes 0x7B (123) and 0x5B (91) fall through when no serializer registered
		if (firstByte is 0x7B or 0x5B)
		{
			LogRawJsonFormatDetected();
			return DeserializeJson<T>(data.AsSpan());
		}

		// 4. Unknown format - unregistered serializer ID or unrecognized byte
		throw SerializationException.UnknownFormat(firstByte);
	}

	/// <summary>
	/// Deserializes using the specified serializer with proper exception handling.
	/// </summary>
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "Deserializer selection is registry-driven; trimming-safe usage requires explicit serializer registration.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Deserializer implementations may require dynamic code generation; AOT users must select compatible serializers.")]
	private static T DeserializeWithSerializer<T>(IPluggableSerializer serializer, ReadOnlySpan<byte> payload)
	{
		try
		{
			return serializer.Deserialize<T>(payload)
				?? throw SerializationException.NullResult<T>();
		}
		catch (Exception ex) when (ex is not SerializationException)
		{
			throw SerializationException.Wrap<T>("deserialize", ex);
		}
	}

	/// <summary>
	/// Deserializes JSON data with proper exception handling.
	/// </summary>
	/// <remarks>
	/// Uses the configured <see cref="JsonSerializerOptions"/> for fallback deserialization
	/// of raw JSON from external systems. By default, this uses PropertyNameCaseInsensitive
	/// and camelCase naming policy to handle various external system conventions.
	/// </remarks>
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "JSON fallback is for interoperability; AOT users should configure source-generated contexts.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "JSON fallback uses reflection-based serialization; AOT users should configure source-generated contexts.")]
	private T DeserializeJson<T>(ReadOnlySpan<byte> payload)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(payload, _jsonOptions)
					?? throw SerializationException.NullResult<T>();
		}
		catch (JsonException ex)
		{
			throw SerializationException.Wrap<T>("deserialize JSON", ex);
		}
	}

	#region LoggerMessage Definitions

	[LoggerMessage(LogLevel.Debug,
			"Deserializing with legacy serializer '{LegacySerializer}' (ID: 0x{SerializerId:X2}). " +
			"Current serializer: '{CurrentSerializer}'")]
	private partial void LogLegacySerializerSelected(
			string legacySerializer,
			byte serializerId,
			string currentSerializer);

	[LoggerMessage(LogLevel.Debug,
			"Confluent Schema Registry format detected. Skipping 5-byte header and deserializing as JSON.")]
	private partial void LogSchemaRegistryFormatDetected();

	[LoggerMessage(LogLevel.Debug,
			"Raw JSON format detected from external system. Deserializing as JSON.")]
	private partial void LogRawJsonFormatDetected();

	#endregion

	/// <summary>
	/// Gets a formatted string of all registered serializer names and IDs.
	/// </summary>
	private string GetRegisteredSerializerNames()
	{
		var all = _registry.GetAll();
		return string.Join(", ", all.Select(s => $"{s.Name} (0x{s.Id:X2})"));
	}
}
