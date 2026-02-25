// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Deserializes JSON messages from Confluent wire format.
/// </summary>
/// <remarks>
/// <para>
/// This deserializer:
/// </para>
/// <list type="bullet">
///   <item><description>Extracts the schema ID from the wire format header</description></item>
///   <item><description>Resolves the .NET type via <see cref="ISchemaTypeResolver"/></description></item>
///   <item><description>Deserializes the JSON payload using System.Text.Json</description></item>
/// </list>
/// <para>
/// Uses <see cref="ReadOnlyMemory{T}"/> for zero-copy deserialization from Kafka consumer buffers.
/// </para>
/// </remarks>
public sealed class ConfluentJsonDeserializer : IConfluentFormatDeserializer
{
	private readonly ISchemaTypeResolver _typeResolver;
	private readonly ISubjectNameStrategy _subjectNameStrategy;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ILogger<ConfluentJsonDeserializer> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentJsonDeserializer"/> class.
	/// </summary>
	/// <param name="typeResolver">The schema type resolver.</param>
	/// <param name="subjectNameStrategy">The subject naming strategy.</param>
	/// <param name="logger">The logger.</param>
	public ConfluentJsonDeserializer(
		ISchemaTypeResolver typeResolver,
		ISubjectNameStrategy subjectNameStrategy,
		ILogger<ConfluentJsonDeserializer> logger)
		: this(typeResolver, subjectNameStrategy, null, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentJsonDeserializer"/> class.
	/// </summary>
	/// <param name="typeResolver">The schema type resolver.</param>
	/// <param name="subjectNameStrategy">The subject naming strategy.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	/// <param name="logger">The logger.</param>
	public ConfluentJsonDeserializer(
		ISchemaTypeResolver typeResolver,
		ISubjectNameStrategy subjectNameStrategy,
		JsonSerializerOptions? jsonOptions,
		ILogger<ConfluentJsonDeserializer> logger)
	{
		_typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
		_subjectNameStrategy = subjectNameStrategy ?? throw new ArgumentNullException(nameof(subjectNameStrategy));
		_jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<T> DeserializeAsync<T>(
		string topic,
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);

		var result = await DeserializeInternalAsync(topic, data, typeof(T), cancellationToken)
			.ConfigureAwait(false);

		return (T)result.Message;
	}

	/// <inheritdoc/>
	public async Task<DeserializationResult> DeserializeAsync(
		string topic,
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);

		return await DeserializeInternalAsync(topic, data, null, cancellationToken)
			.ConfigureAwait(false);
	}

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize(ReadOnlySpan<Byte>, Type, JsonSerializerOptions)")]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize(ReadOnlySpan<Byte>, Type, JsonSerializerOptions)")]
	private async Task<DeserializationResult> DeserializeInternalAsync(
		string topic,
		ReadOnlyMemory<byte> data,
		Type? expectedType,
		CancellationToken cancellationToken)
	{
		// Extract schema ID from wire format header
		var schemaId = ConfluentWireFormat.ReadSchemaId(data.Span);

		_logger.LogDebug(
			"Deserializing message from topic {Topic} with schema ID {SchemaId}",
			topic,
			schemaId);

		// Resolve the .NET type from schema
		Type messageType;
		if (expectedType != null)
		{
			// Type is known at compile time - use it directly
			messageType = expectedType;
		}
		else
		{
			// Runtime type resolution via schema title
			var subject = _subjectNameStrategy.GetValueSubject(topic, typeof(object));
			var resolution = await _typeResolver.ResolveTypeAsync(schemaId, subject, cancellationToken)
				.ConfigureAwait(false);

			if (!resolution.IsSuccess)
			{
				throw new SchemaRegistryException(
					$"Type resolution failed for schema {schemaId}: {resolution.FailureReason}");
			}

			messageType = resolution.MessageType;
		}

		// Extract payload (skip 5-byte header)
		var payload = data.Slice(ConfluentWireFormat.HeaderSize);

		// Deserialize JSON payload
		object? message;
		try
		{
			message = JsonSerializer.Deserialize(payload.Span, messageType, _jsonOptions);
		}
		catch (JsonException ex)
		{
			_logger.LogError(
				ex,
				"Failed to deserialize JSON payload for topic {Topic}, schema {SchemaId}, type {Type}",
				topic,
				schemaId,
				messageType.FullName);

			throw new SchemaRegistryException(
				$"JSON deserialization failed for type {messageType.Name}: {ex.Message}",
				ex);
		}

		if (message == null)
		{
			throw new SchemaRegistryException(
				$"Deserialization returned null for type {messageType.Name}");
		}

		// Extract version if message is versioned
		var version = 1;
		if (message is IVersionedMessage versioned)
		{
			version = versioned.Version;
		}

		_logger.LogDebug(
			"Deserialized message of type {Type} (version {Version}) from topic {Topic}",
			messageType.Name,
			version,
			topic);

		return new DeserializationResult(message, messageType, schemaId, version);
	}
}
