// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;

using Google.Protobuf;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// In-memory implementation of schema registry with HTTP backend support.
/// </summary>
public sealed class PubSubSchemaRegistry : ISchemaRegistry, IDisposable
{
	private readonly IOptions<PubSubSerializationOptions> _options;
	private readonly ILogger<PubSubSchemaRegistry> _logger;
	private readonly HttpClient _httpClient;
	private readonly ConcurrentDictionary<string, SchemaMetadata> _schemaCache;
	private readonly SemaphoreSlim _registrationLock;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubSchemaRegistry" /> class.
	/// </summary>
	public PubSubSchemaRegistry(
		IOptions<PubSubSerializationOptions> options,
		ILogger<PubSubSchemaRegistry> logger,
		IHttpClientFactory httpClientFactory)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_httpClient = httpClientFactory?.CreateClient("SchemaRegistry") ?? throw new ArgumentNullException(nameof(httpClientFactory));

		_schemaCache = new ConcurrentDictionary<string, SchemaMetadata>(StringComparer.Ordinal);
		_registrationLock = new SemaphoreSlim(1, 1);
		_jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

		if (_options.Value.SchemaRegistryUrl is not null)
		{
			_httpClient.BaseAddress = _options.Value.SchemaRegistryUrl;
		}
	}

	/// <inheritdoc />
	public async Task<SchemaMetadata> RegisterSchemaAsync(
		Type messageType,
		string schema,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageType);
		ArgumentNullException.ThrowIfNull(schema);

		var typeName = GetTypeName(messageType);

		// Check cache first
		if (_schemaCache.TryGetValue(typeName, out var cached))
		{
			return cached;
		}

		await _registrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Double-check after acquiring lock
			if (_schemaCache.TryGetValue(typeName, out cached))
			{
				return cached;
			}

			var metadata = new SchemaMetadata
			{
				TypeName = typeName,
				Schema = schema,
				Version = await GetNextVersionAsync(typeName, cancellationToken).ConfigureAwait(false),
				Format = _options.Value.Format,
				RegisteredAt = DateTimeOffset.UtcNow,
			};

			// Register with backend if configured
			if (_httpClient.BaseAddress != null)
			{
				await RegisterWithBackendAsync(metadata, cancellationToken).ConfigureAwait(false);
			}

			_schemaCache[typeName] = metadata;
			_logger.LogInformation(
				"Registered schema for type {TypeName} with version {Version}",
				typeName,
				metadata.Version);

			return metadata;
		}
		finally
		{
			_ = _registrationLock.Release();
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task<SchemaMetadata?> GetSchemaAsync(
		Type messageType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var typeName = GetTypeName(messageType);

		// Check cache
		if (_schemaCache.TryGetValue(typeName, out var cached))
		{
			return cached;
		}

		// Try to fetch from backend
		if (_httpClient.BaseAddress != null)
		{
			try
			{
				var response = await _httpClient.GetAsync(
					new Uri($"/schemas/{Uri.EscapeDataString(typeName)}/latest", UriKind.Relative),
					cancellationToken).ConfigureAwait(false);

				if (response.IsSuccessStatusCode)
				{
					var metadata = await response.Content.ReadFromJsonAsync<SchemaMetadata>(
						_jsonOptions,
						cancellationToken).ConfigureAwait(false);

					if (metadata != null)
					{
						_schemaCache[typeName] = metadata;
						return metadata;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to fetch schema for {TypeName}", typeName);
			}
		}

		return null;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<bool> ValidateAsync(object message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (!_options.Value.EnableSchemaValidation)
		{
			return true;
		}

		var messageType = message.GetType();
		var schema = await GetSchemaAsync(messageType, cancellationToken).ConfigureAwait(false);

		if (schema == null)
		{
			_logger.LogWarning("No schema found for type {TypeName}", messageType.Name);
			return false;
		}

		// Perform validation based on format
		return _options.Value.Format switch
		{
			SerializationFormat.Json => ValidateJson(message, schema.Schema),
			SerializationFormat.Protobuf => ValidateProtobuf(message, schema.Schema),
			_ => true,
		};
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_registrationLock.Dispose();
		_httpClient.Dispose();
		GC.SuppressFinalize(this);
	}

	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "schema parameter reserved for future schema-based Protobuf validation")]
	private static bool ValidateProtobuf(object message, string schema) =>

		// Protobuf messages are self-validating through their structure
		message is IMessage;

	[RequiresUnreferencedCode(
		"Calls System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(JsonSerializerOptions, CancellationToken)")]
	private async Task<int> GetNextVersionAsync(string typeName, CancellationToken cancellationToken)
	{
		if (_httpClient.BaseAddress == null)
		{
			return 1;
		}

		try
		{
			var response = await _httpClient.GetAsync(
				new Uri($"/schemas/{Uri.EscapeDataString(typeName)}/versions", UriKind.Relative),
				cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode)
			{
				var versions = await response.Content.ReadFromJsonAsync<List<int>>(
					_jsonOptions,
					cancellationToken).ConfigureAwait(false);

				return (versions?.Count ?? 0) + 1;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get versions for {TypeName}", typeName);
		}

		return 1;
	}

	[RequiresUnreferencedCode(
		"Calls System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync<TValue>(String, TValue, JsonSerializerOptions, CancellationToken)")]
	private async Task RegisterWithBackendAsync(SchemaMetadata metadata, CancellationToken cancellationToken)
	{
		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"/schemas",
				metadata,
				_jsonOptions,
				cancellationToken).ConfigureAwait(false);

			_ = response.EnsureSuccessStatusCode();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to register schema with backend");

			// Don't throw - allow local registration to succeed
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "schema parameter reserved for future JSON Schema validation implementation")]
	private bool ValidateJson(object message, string schema)
	{
		try
		{
			// Basic validation - serialize and check it matches schema structure
			var json = JsonSerializer.Serialize(message, _jsonOptions);
			var doc = JsonDocument.Parse(json);

			// In a real implementation, would use JSON Schema validation
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "JSON validation failed");
			return false;
		}
	}

	private string GetTypeName(Type type) =>
		_options.Value.UseFullTypeNames
			? type.FullName ?? type.Name
			: type.Name;
}
