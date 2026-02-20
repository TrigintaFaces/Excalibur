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
internal sealed class JsonClaimCheckSerializer(JsonSerializerOptions? options = null) : IBinaryMessageSerializer
{
	private readonly JsonSerializerOptions? _options = options;

	/// <inheritdoc/>
	public string SerializerName => "Json-Abstraction";

	/// <inheritdoc/>
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc/>
	public string ContentType => "application/json";

	/// <inheritdoc/>
	public bool SupportsCompression => false;

	/// <inheritdoc/>
	public string Format => "JSON";

	/// <inheritdoc/>
	[RequiresDynamicCode("JSON serialization requires dynamic code generation")]
	[RequiresUnreferencedCode("JSON serialization may reference types that could be trimmed")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		// Serialize directly to UTF-8 bytes, avoiding an intermediate string allocation.
		return JsonSerializer.SerializeToUtf8Bytes(message, _options);
	}

	/// <inheritdoc/>
	[RequiresDynamicCode("JSON deserialization requires dynamic code generation")]
	[RequiresUnreferencedCode("JSON deserialization may reference types that could be trimmed")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		// Deserialize directly from UTF-8 bytes, avoiding an intermediate string allocation.
		return JsonSerializer.Deserialize<T>(data.AsSpan(), _options)!;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization may reference types that could be trimmed")]
	public void Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message, IBufferWriter<byte> bufferWriter)
	{
		// Serialize directly to the buffer writer via Utf8JsonWriter, avoiding intermediate string allocation.
		using var writer = new Utf8JsonWriter(bufferWriter);
		JsonSerializer.Serialize(writer, message, _options);
	}

	/// <inheritdoc/>
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> data)
	{
		// Deserialize directly from UTF-8 bytes, avoiding an intermediate string allocation.
		return JsonSerializer.Deserialize<T>(data, _options)!;
	}

	/// <summary>
	/// Deserializes directly from a stream using streaming JSON deserialization,
	/// avoiding loading the entire stream into a byte array first.
	/// </summary>
	/// <typeparam name="T"> The type of the message to deserialize. </typeparam>
	/// <param name="stream"> The stream to read from. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The deserialized message. </returns>
	[RequiresDynamicCode("JSON deserialization requires dynamic code generation")]
	[RequiresUnreferencedCode("JSON deserialization may reference types that could be trimmed")]
	public async ValueTask<T> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		Stream stream,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);

		// Use System.Text.Json streaming deserialization directly on the stream,
		// avoiding the intermediate byte[] → string → object conversion path.
		var result = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken).ConfigureAwait(false);
		return result!;
	}

	/// <summary>
	/// Serializes directly to a stream using streaming JSON serialization,
	/// avoiding intermediate byte array allocation.
	/// </summary>
	/// <typeparam name="T"> The type of the message to serialize. </typeparam>
	/// <param name="message"> The message to serialize. </param>
	/// <param name="stream"> The stream to write to. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	[RequiresDynamicCode("JSON serialization requires dynamic code generation")]
	[RequiresUnreferencedCode("JSON serialization may reference types that could be trimmed")]
	public async ValueTask SerializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		T message,
		Stream stream,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);

		// Use System.Text.Json streaming serialization directly to the stream,
		// avoiding the intermediate object → string → byte[] → stream path.
		await JsonSerializer.SerializeAsync(stream, message, _options, cancellationToken).ConfigureAwait(false);
	}
}
