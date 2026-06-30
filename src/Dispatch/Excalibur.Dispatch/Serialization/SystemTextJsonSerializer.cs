// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Consolidated System.Text.Json serializer implementing <see cref="ISerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces SystemTextJsonPluggableSerializer and SystemTextJsonMessageSerializer.
/// Provides human-readable JSON output, ideal for debugging and development.
/// </para>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.SystemTextJson"/> (2)
/// </para>
/// </remarks>
[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization may require runtime code generation.")]
public sealed class SystemTextJsonSerializer : ISerializer
{
	private readonly JsonSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance with default options (camelCase, compact).
	/// </summary>
	public SystemTextJsonSerializer()
		: this(null)
	{
	}

	/// <summary>
	/// Initializes a new instance with custom JSON options.
	/// </summary>
	/// <param name="options">Custom JSON serializer options. If null, defaults with camelCase are used.</param>
	public SystemTextJsonSerializer(JsonSerializerOptions? options)
	{
		_options = options ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,

			// Security: pin an explicit depth bound so the default-options path cannot deserialize a
			// hostile deeply-nested payload into unbounded recursion (stack overflow). 64 matches the
			// framework's configured DispatchJsonSerializer; it also equals STJ's implicit default, so
			// pinning it changes no behavior today while making the bound explicit and immune to a
			// future framework/runtime default change. See bd-qvbzm4. Callers supplying their own
			// options own their depth bound.
			MaxDepth = 64
		};
	}

	/// <inheritdoc />
	public string Name => "System.Text.Json";

	/// <inheritdoc />
	public string Version => typeof(JsonSerializer).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	public string ContentType => "application/json";

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		try
		{
			using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions
			{
				Indented = _options.WriteIndented,
				Encoder = _options.Encoder,
			});
			JsonSerializer.Serialize(writer, value, _options);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc cref="ISerializer.Deserialize{T}"/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(data, _options)
				?? throw SerializationException.NullResult<T>();
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("deserialize", ex);
		}
	}

	/// <inheritdoc />
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return JsonSerializer.SerializeToUtf8Bytes(value, type, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return JsonSerializer.Deserialize(data, type, _options)
				?? throw SerializationException.NullResultForType(type);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.WrapObject(type, "deserialize", ex);
		}
	}

}
