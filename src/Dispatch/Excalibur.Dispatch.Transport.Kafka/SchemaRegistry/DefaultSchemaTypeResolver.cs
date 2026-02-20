// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Default implementation of <see cref="ISchemaTypeResolver"/> that uses
/// the JSON Schema <c>title</c> property for type mapping.
/// </summary>
/// <remarks>
/// <para>
/// This resolver maintains two caches:
/// </para>
/// <list type="bullet">
///   <item><description>Schema ID → SchemaTypeResolution (immutable once resolved)</description></item>
///   <item><description>Message type name → .NET Type (registered at startup)</description></item>
/// </list>
/// <para>
/// Thread-safe for concurrent access.
/// </para>
/// </remarks>
public sealed class DefaultSchemaTypeResolver : ISchemaTypeResolver
{
	private readonly ISchemaRegistryClient _schemaRegistryClient;
	private readonly ILogger<DefaultSchemaTypeResolver> _logger;
	private readonly ConcurrentDictionary<int, SchemaTypeResolution> _resolutionCache = new();
	private readonly ConcurrentDictionary<string, Type> _typesByMessageType = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultSchemaTypeResolver"/> class.
	/// </summary>
	/// <param name="schemaRegistryClient">The Schema Registry client.</param>
	/// <param name="logger">The logger.</param>
	public DefaultSchemaTypeResolver(
		ISchemaRegistryClient schemaRegistryClient,
		ILogger<DefaultSchemaTypeResolver> logger)
	{
		_schemaRegistryClient = schemaRegistryClient ?? throw new ArgumentNullException(nameof(schemaRegistryClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<SchemaTypeResolution> ResolveTypeAsync(
		int schemaId,
		string subject,
		CancellationToken cancellationToken)
	{
		// Check cache first (O(1) lookup)
		if (_resolutionCache.TryGetValue(schemaId, out var cached))
		{
			return cached;
		}

		// Fetch schema from registry (returns string directly)
		var schemaJson = await _schemaRegistryClient.GetSchemaByIdAsync(schemaId, cancellationToken)
			.ConfigureAwait(false);

		if (string.IsNullOrEmpty(schemaJson))
		{
			var failed = SchemaTypeResolution.Failed(schemaId, null, $"Schema not found for ID {schemaId}");
			_ = _resolutionCache.TryAdd(schemaId, failed);
			return failed;
		}

		// Parse title from JSON Schema
		var messageTypeName = ExtractTitleFromSchema(schemaJson);
		if (string.IsNullOrEmpty(messageTypeName))
		{
			_logger.LogWarning(
				"Schema {SchemaId} for subject {Subject} does not have a title property",
				schemaId,
				subject);

			var failed = SchemaTypeResolution.Failed(schemaId, schemaJson, "Schema does not have a title property");
			_ = _resolutionCache.TryAdd(schemaId, failed);
			return failed;
		}

		// Look up registered type
		if (!_typesByMessageType.TryGetValue(messageTypeName, out var messageType))
		{
			_logger.LogWarning(
				"No type registered for message type {MessageTypeName} (schema {SchemaId})",
				messageTypeName,
				schemaId);

			var failed = SchemaTypeResolution.Failed(
				schemaId,
				schemaJson,
				$"No type registered for message type '{messageTypeName}'");
			_ = _resolutionCache.TryAdd(schemaId, failed);
			return failed;
		}

		// Create and cache successful resolution
		var resolution = SchemaTypeResolution.Success(schemaId, messageType, messageTypeName, schemaJson);
		_ = _resolutionCache.TryAdd(schemaId, resolution);

		_logger.LogDebug(
			"Resolved schema {SchemaId} to type {TypeName} via message type {MessageTypeName}",
			schemaId,
			messageType.FullName,
			messageTypeName);

		return resolution;
	}

	/// <inheritdoc/>
	public void RegisterType<T>() where T : IDispatchMessage
	{
		RegisterType(typeof(T));
	}

	/// <inheritdoc/>
	public void RegisterType<T>(string messageTypeName) where T : IDispatchMessage
	{
		RegisterType(typeof(T), messageTypeName);
	}

	/// <inheritdoc/>
	public void RegisterType(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);
		RegisterType(messageType, messageType.Name);
	}

	/// <inheritdoc/>
	public void RegisterType(Type messageType, string messageTypeName)
	{
		ArgumentNullException.ThrowIfNull(messageType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

		if (_typesByMessageType.TryAdd(messageTypeName, messageType))
		{
			_logger.LogDebug(
				"Registered type {TypeName} for message type {MessageTypeName}",
				messageType.FullName,
				messageTypeName);
		}
		else if (_typesByMessageType.TryGetValue(messageTypeName, out var existingType) && existingType != messageType)
		{
			_logger.LogWarning(
				"Message type {MessageTypeName} is already registered to {ExistingType}, ignoring {NewType}",
				messageTypeName,
				existingType.FullName,
				messageType.FullName);
		}
	}

	/// <inheritdoc/>
	public bool IsTypeRegistered(string messageTypeName)
	{
		return _typesByMessageType.ContainsKey(messageTypeName);
	}

	private static string? ExtractTitleFromSchema(string schemaJson)
	{
		try
		{
			using var doc = JsonDocument.Parse(schemaJson);
			if (doc.RootElement.TryGetProperty("title", out var titleElement) &&
				titleElement.ValueKind == JsonValueKind.String)
			{
				return titleElement.GetString();
			}
		}
		catch (JsonException)
		{
			// Invalid JSON - return null
		}

		return null;
	}
}
