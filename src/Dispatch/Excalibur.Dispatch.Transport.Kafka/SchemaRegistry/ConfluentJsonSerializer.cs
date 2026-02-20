// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Serializes messages to Confluent wire format using JSON encoding.
/// </summary>
/// <remarks>
/// <para>
/// This serializer:
/// </para>
/// <list type="bullet">
///   <item><description>Generates JSON Schema from the message type</description></item>
///   <item><description>Registers/retrieves schema ID via <see cref="ISchemaRegistryClient"/></description></item>
///   <item><description>Serializes message to JSON</description></item>
///   <item><description>Prepends 5-byte Confluent wire format header</description></item>
/// </list>
/// <para>
/// Schema ID caching is handled by the <see cref="ISchemaRegistryClient"/> implementation
/// (typically <see cref="CachingSchemaRegistryClient"/>). No additional caching is performed here.
/// </para>
/// </remarks>
public sealed partial class ConfluentJsonSerializer : IConfluentFormatSerializer
{
	private readonly ISchemaRegistryClient _schemaRegistry;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ILogger<ConfluentJsonSerializer> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentJsonSerializer"/> class.
	/// </summary>
	/// <param name="schemaRegistry">The schema registry client.</param>
	/// <param name="logger">The logger instance.</param>
	public ConfluentJsonSerializer(
		ISchemaRegistryClient schemaRegistry,
		ILogger<ConfluentJsonSerializer> logger)
		: this(schemaRegistry, CreateDefaultJsonOptions(), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentJsonSerializer"/> class
	/// with custom JSON serializer options.
	/// </summary>
	/// <param name="schemaRegistry">The schema registry client.</param>
	/// <param name="jsonOptions">The JSON serializer options.</param>
	/// <param name="logger">The logger instance.</param>
	public ConfluentJsonSerializer(
		ISchemaRegistryClient schemaRegistry,
		JsonSerializerOptions jsonOptions,
		ILogger<ConfluentJsonSerializer> logger)
	{
		_schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
		_jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<byte[]> SerializeAsync<T>(
		string topic,
		T message,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(message);

		return await SerializeInternalAsync(topic, message, typeof(T), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<byte[]> SerializeAsync(
		string topic,
		object message,
		Type messageType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(messageType);

		return await SerializeInternalAsync(topic, message, messageType, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<int> SerializeToBufferAsync<T>(
		IBufferWriter<byte> writer,
		string topic,
		T message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(message);

		return await SerializeToBufferInternalAsync(writer, topic, message, typeof(T), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<int> SerializeToBufferAsync(
		IBufferWriter<byte> writer,
		string topic,
		object message,
		Type messageType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(messageType);

		return await SerializeToBufferInternalAsync(writer, topic, message, messageType, cancellationToken).ConfigureAwait(false);
	}

	private static JsonSerializerOptions CreateDefaultJsonOptions() => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private async Task<byte[]> SerializeInternalAsync(
		string topic,
		object message,
		Type messageType,
		CancellationToken cancellationToken)
	{
		var subject = SchemaRegistrySubject.ForValue(topic);

		LogSerializingMessage(messageType.Name, topic, subject);

		// Generate JSON Schema for the type
		var schema = JsonSchemaGenerator.Generate(messageType, _jsonOptions);

		// Get or register schema ID (caching is handled by ISchemaRegistryClient)
		// GetSchemaIdAsync auto-registers if schema doesn't exist (Confluent SDK behavior)
		var schemaId = await _schemaRegistry.GetSchemaIdAsync(subject, schema, cancellationToken).ConfigureAwait(false);

		LogSchemaIdResolved(schemaId, subject);

		// Serialize message to JSON
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(message, messageType, _jsonOptions);

		// Create output buffer: 5-byte header + JSON payload
		var result = new byte[ConfluentWireFormat.HeaderSize + jsonBytes.Length];

		// Write Confluent wire format header
		ConfluentWireFormat.WriteHeader(result.AsSpan(0, ConfluentWireFormat.HeaderSize), schemaId);

		// Copy JSON payload
		jsonBytes.CopyTo(result, ConfluentWireFormat.HeaderSize);

		LogSerializationComplete(result.Length, schemaId);

		return result;
	}

	/// <summary>
	/// Zero-copy serialization: writes directly to IBufferWriter without intermediate allocations.
	/// </summary>
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize")]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize")]
	private async ValueTask<int> SerializeToBufferInternalAsync(
		IBufferWriter<byte> writer,
		string topic,
		object message,
		Type messageType,
		CancellationToken cancellationToken)
	{
		var subject = SchemaRegistrySubject.ForValue(topic);

		LogZeroCopySerializationStarted(messageType.Name, topic, subject);

		// Generate JSON Schema for the type
		var schema = JsonSchemaGenerator.Generate(messageType, _jsonOptions);

		// Get or register schema ID (caching is handled by ISchemaRegistryClient)
		var schemaId = await _schemaRegistry.GetSchemaIdAsync(subject, schema, cancellationToken).ConfigureAwait(false);

		LogSchemaIdResolved(schemaId, subject);

		// Write 5-byte Confluent wire format header directly to buffer (zero-copy)
		var headerSpan = writer.GetSpan(ConfluentWireFormat.HeaderSize);
		ConfluentWireFormat.WriteHeader(headerSpan, schemaId);
		writer.Advance(ConfluentWireFormat.HeaderSize);

		LogZeroCopyHeaderWritten(schemaId);

		// Write JSON payload directly to buffer using Utf8JsonWriter (zero-copy)
		// This avoids intermediate byte[] allocation
		using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions
		{
			Encoder = _jsonOptions.Encoder,
			Indented = _jsonOptions.WriteIndented,
			SkipValidation = false
		});

		JsonSerializer.Serialize(jsonWriter, message, messageType, _jsonOptions);

		var jsonBytesWritten = (int)jsonWriter.BytesCommitted;
		LogZeroCopyPayloadWritten(jsonBytesWritten);

		var totalBytesWritten = ConfluentWireFormat.HeaderSize + jsonBytesWritten;
		LogZeroCopySerializationComplete(totalBytesWritten, schemaId);

		return totalBytesWritten;
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.SerializingMessage, LogLevel.Debug,
		"Serializing {MessageType} for topic {Topic} with subject {Subject}")]
	private partial void LogSerializingMessage(string messageType, string topic, string subject);

	[LoggerMessage(KafkaEventId.SchemaIdResolved, LogLevel.Debug,
		"Resolved schema ID {SchemaId} for subject {Subject}")]
	private partial void LogSchemaIdResolved(int schemaId, string subject);

	[LoggerMessage(KafkaEventId.SerializationComplete, LogLevel.Debug,
		"Serialization complete: {TotalBytes} bytes with schema ID {SchemaId}")]
	private partial void LogSerializationComplete(int totalBytes, int schemaId);

	// Zero-copy logging methods
	[LoggerMessage(KafkaEventId.ZeroCopySerializationStarted, LogLevel.Debug,
		"Zero-copy serializing {MessageType} for topic {Topic} with subject {Subject}")]
	private partial void LogZeroCopySerializationStarted(string messageType, string topic, string subject);

	[LoggerMessage(KafkaEventId.ZeroCopyHeaderWritten, LogLevel.Trace,
		"Zero-copy header written with schema ID {SchemaId}")]
	private partial void LogZeroCopyHeaderWritten(int schemaId);

	[LoggerMessage(KafkaEventId.ZeroCopyPayloadWritten, LogLevel.Trace,
		"Zero-copy JSON payload written: {JsonBytes} bytes")]
	private partial void LogZeroCopyPayloadWritten(int jsonBytes);

	[LoggerMessage(KafkaEventId.ZeroCopySerializationComplete, LogLevel.Debug,
		"Zero-copy serialization complete: {TotalBytes} bytes with schema ID {SchemaId}")]
	private partial void LogZeroCopySerializationComplete(int totalBytes, int schemaId);
}
