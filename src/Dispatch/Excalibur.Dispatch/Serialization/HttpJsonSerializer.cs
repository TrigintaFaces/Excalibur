// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// HTTP serializer implementation using System.Text.Json.
/// </summary>
/// <remarks>
/// <para>
/// This serializer is specifically designed for HTTP request/response body serialization.
/// It provides Stream-based methods optimized for ASP.NET Core scenarios where data
/// is read from or written to HTTP streams directly.
/// </para>
/// <para>
/// <b>Configuration:</b>
/// </para>
/// <para>
/// The serializer accepts optional <see cref="JsonSerializerOptions"/> for customization.
/// If not provided, sensible defaults are used:
/// </para>
/// <list type="bullet">
///   <item><c>PropertyNameCaseInsensitive = true</c> for forgiving parsing</item>
///   <item><c>PropertyNamingPolicy = JsonNamingPolicy.CamelCase</c> for web conventions</item>
///   <item><c>DefaultIgnoreCondition = WhenWritingNull</c> for compact output</item>
/// </list>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// // Register in DI
/// services.AddDispatchSerialization();
///
/// // Or with custom options
/// services.AddSingleton&lt;IHttpSerializer&gt;(new HttpJsonSerializer(new JsonSerializerOptions
/// {
///     PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
/// }));
///
/// // Use in controller
/// await _httpSerializer.SerializeAsync(Response.Body, result, cancellationToken);
/// var request = await _httpSerializer.DeserializeAsync&lt;CreateOrderRequest&gt;(Request.Body);
/// </code>
/// <para>
/// See the pluggable serialization architecture documentation.
/// </para>
/// </remarks>
public sealed class HttpJsonSerializer : IHttpSerializer
{
	/// <summary>
	/// Default JSON options for HTTP serialization.
	/// </summary>
	private static readonly JsonSerializerOptions DefaultOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	private readonly JsonSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpJsonSerializer"/> class
	/// with default options.
	/// </summary>
	public HttpJsonSerializer()
		: this(DefaultOptions)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpJsonSerializer"/> class
	/// with custom options.
	/// </summary>
	/// <param name="options">The JSON serializer options to use.</param>
	/// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
	public HttpJsonSerializer(JsonSerializerOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public async Task SerializeAsync(
		Stream stream,
		object value,
		Type type,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			await JsonSerializer.SerializeAsync(stream, value, type, _options, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (JsonException ex)
		{
			throw SerializationException.SerializationFailed(
				type,
				SerializerIds.SystemTextJson,
				"System.Text.Json",
				ex);
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public async Task<object?> DeserializeAsync(
		Stream stream,
		Type type,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return await JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (JsonException ex)
		{
			throw SerializationException.DeserializationFailed(
				type,
				SerializerIds.SystemTextJson,
				"System.Text.Json",
				ex);
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public byte[] Serialize(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return JsonSerializer.SerializeToUtf8Bytes(value, type, _options);
		}
		catch (JsonException ex)
		{
			throw SerializationException.SerializationFailed(
				type,
				SerializerIds.SystemTextJson,
				"System.Text.Json",
				ex);
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public object? Deserialize(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return JsonSerializer.Deserialize(data, type, _options);
		}
		catch (JsonException ex)
		{
			throw SerializationException.DeserializationFailed(
				type,
				SerializerIds.SystemTextJson,
				"System.Text.Json",
				ex);
		}
	}
}
