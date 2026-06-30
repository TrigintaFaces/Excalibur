// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// JSON Claim Check serializer that uses System.Text.Json directly for zero-copy UTF-8 serialization.
/// </summary>
[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization may require runtime code generation.")]
internal sealed class JsonClaimCheckSerializer(JsonSerializerOptions? options = null) : ISerializer
{
	// unv8i3: when no options are supplied, default to the framework-wide JSON policy
	// (camelCase + case-insensitive) so payloads interop with every other ISerializer impl.
	// Without this, null options fall through to System.Text.Json's PascalCase/case-sensitive
	// defaults — a cross-serializer interop hazard and a convention violation.
	private readonly JsonSerializerOptions _options = options ?? new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
	};

	/// <inheritdoc/>
	public string Name => "Json-Abstraction";

	/// <inheritdoc/>
	public string Version => typeof(JsonSerializer).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc/>
	public string ContentType => "application/json";

	/// <inheritdoc/>
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(bufferWriter);

		try
		{
			using var writer = new Utf8JsonWriter(bufferWriter);
			JsonSerializer.Serialize(writer, value, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc/>
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

	/// <inheritdoc/>
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

	/// <inheritdoc/>
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
