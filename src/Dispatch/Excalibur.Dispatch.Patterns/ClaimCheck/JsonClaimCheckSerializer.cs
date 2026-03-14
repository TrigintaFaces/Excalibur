// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// JSON Claim Check serializer that uses System.Text.Json directly for zero-copy UTF-8 serialization.
/// </summary>
[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
[RequiresDynamicCode("JSON serialization may require runtime code generation.")]
internal sealed class JsonClaimCheckSerializer(JsonSerializerOptions? options = null) : ISerializer
{
	private readonly JsonSerializerOptions? _options = options;

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
		using var writer = new Utf8JsonWriter(bufferWriter);
		JsonSerializer.Serialize(writer, value, _options);
	}

	/// <inheritdoc/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		return JsonSerializer.Deserialize<T>(data, _options)!;
	}

	/// <inheritdoc/>
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);
		return JsonSerializer.SerializeToUtf8Bytes(value, type, _options);
	}

	/// <inheritdoc/>
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return JsonSerializer.Deserialize(data, type, _options)!;
	}
}
