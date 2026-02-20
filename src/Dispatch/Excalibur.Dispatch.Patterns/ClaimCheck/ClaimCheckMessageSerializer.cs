// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Message serializer that implements the Claim Check pattern for large messages. Small messages are serialized normally, while large
/// messages are stored in external storage and replaced with a small reference.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ClaimCheckMessageSerializer" /> class. </remarks>
/// <param name="claimCheckProvider"> The claim check provider for storing large payloads. </param>
/// <param name="baseSerializer">
/// The base serializer for normal serialization. Must not be null; use the IJsonSerializer overload for JSON scenarios.
/// </param>
/// <param name="options"> The claim check options. If null, uses default options. </param>
public sealed class ClaimCheckMessageSerializer(
	IClaimCheckProvider claimCheckProvider,
	IBinaryMessageSerializer baseSerializer,
	ClaimCheckOptions? options = null) : IBinaryMessageSerializer
{
	/// <summary>
	/// Magic byte prefix for claim check envelope data: ASCII "CC01" (0x43, 0x43, 0x30, 0x31).
	/// Used for fast format detection without exception-driven control flow.
	/// </summary>
	private static readonly byte[] ClaimCheckMagicPrefix = "CC01"u8.ToArray();

	private readonly IClaimCheckProvider _claimCheckProvider =
		claimCheckProvider ?? throw new ArgumentNullException(nameof(claimCheckProvider));

	private readonly IBinaryMessageSerializer _baseSerializer =
		baseSerializer ?? throw new ArgumentNullException(nameof(baseSerializer));

	private readonly ClaimCheckOptions _options = options ?? new ClaimCheckOptions();

	/// <summary>
	/// Initializes a new instance of the <see cref="ClaimCheckMessageSerializer" /> class
	/// using the built-in <see cref="JsonClaimCheckSerializer"/> for JSON serialization.
	/// </summary>
	/// <param name="claimCheckProvider"> The claim check provider. </param>
	/// <param name="options"> Optional claim check options. </param>
	public ClaimCheckMessageSerializer(
		IClaimCheckProvider claimCheckProvider,
		ClaimCheckOptions? options = null)
		: this(claimCheckProvider, new JsonClaimCheckSerializer(), options)
	{
	}

	/// <inheritdoc />
	public string SerializerName => $"ClaimCheck-{_baseSerializer.SerializerName}";

	/// <inheritdoc />
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc />
	public string ContentType => _baseSerializer.ContentType;

	/// <inheritdoc />
	public bool SupportsCompression => _baseSerializer.SupportsCompression;

	/// <inheritdoc />
	public string Format => $"ClaimCheck({_baseSerializer.Format})";

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var payload = _baseSerializer.Serialize(message);

		if (payload.Length > _options.PayloadThreshold)
		{
			throw new NotSupportedException(
				$"Message payload ({payload.Length} bytes) exceeds the claim check threshold ({_options.PayloadThreshold} bytes). " +
				"Use SerializeAsync to enable the claim check pattern for large messages.");
		}

		return payload;
	}

	/// <inheritdoc />
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);

		// Check for claim check magic prefix to detect envelope format without exceptions
		if (IsClaimCheckEnvelope(data))
		{
			throw new NotSupportedException(
				"Data contains a claim check envelope reference. Use DeserializeAsync to retrieve the payload from external storage.");
		}

		return _baseSerializer.Deserialize<T>(data);
	}

	/// <summary>
	/// Serializes a message to bytes asynchronously, using claim check for large payloads.
	/// </summary>
	/// <remarks>
	/// This instance method shadows the extension method to enable the claim check pattern for large payloads.
	/// Small messages are serialized directly. Large messages (exceeding <see cref="ClaimCheckOptions.PayloadThreshold"/>)
	/// are stored in the claim check provider and replaced with a compact envelope.
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async ValueTask<byte[]> SerializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		T message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		var payload = _baseSerializer.Serialize(message);

		if (payload.Length <= _options.PayloadThreshold)
		{
			return payload;
		}

		var metadata = new ClaimCheckMetadata
		{
			MessageType = typeof(T).Name,
			ContentType = _baseSerializer.ContentType,
		};

		var reference = await _claimCheckProvider.StoreAsync(payload, cancellationToken, metadata).ConfigureAwait(false);

		var envelopeBytes = _baseSerializer.Serialize(new ClaimCheckEnvelope
		{
			Reference = reference,
			MessageType = typeof(T).Name,
			SerializerName = _baseSerializer.SerializerName,
			OriginalSize = payload.Length,
		});

		// Prepend magic prefix so receivers can detect claim check envelopes without exception-driven control flow
		return PrependMagicPrefix(envelopeBytes);
	}

	/// <summary>
	/// Deserializes bytes to a message asynchronously, resolving claim check references.
	/// </summary>
	/// <remarks>
	/// This instance method shadows the extension method to enable resolving claim check references
	/// from external storage. If the data represents a claim check envelope, the original payload is
	/// retrieved from the provider before deserialization.
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async ValueTask<T> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		byte[] data,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		cancellationToken.ThrowIfCancellationRequested();

		// Check for claim check magic prefix to detect envelope format without exceptions
		if (!IsClaimCheckEnvelope(data))
		{
			return _baseSerializer.Deserialize<T>(data);
		}

		// Strip the magic prefix and deserialize the envelope
		var envelopeBytes = StripMagicPrefix(data);
		var envelope = _baseSerializer.Deserialize<ClaimCheckEnvelope>(envelopeBytes);
		var storedPayload = await _claimCheckProvider.RetrieveAsync(envelope.Reference, cancellationToken).ConfigureAwait(false);
		return _baseSerializer.Deserialize<T>(storedPayload);
	}

	/// <inheritdoc />
	public void Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message, IBufferWriter<byte> bufferWriter) => _baseSerializer.Serialize(message, bufferWriter);

	/// <inheritdoc />
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> data) => _baseSerializer.Deserialize<T>(data);

	/// <summary>
	/// Checks whether the data starts with the claim check magic prefix.
	/// </summary>
	private static bool IsClaimCheckEnvelope(byte[] data)
	{
		if (data.Length < ClaimCheckMagicPrefix.Length)
		{
			return false;
		}

		return data.AsSpan(0, ClaimCheckMagicPrefix.Length).SequenceEqual(ClaimCheckMagicPrefix);
	}

	/// <summary>
	/// Prepends the magic prefix to envelope bytes.
	/// </summary>
	private static byte[] PrependMagicPrefix(byte[] envelopeBytes)
	{
		var result = new byte[ClaimCheckMagicPrefix.Length + envelopeBytes.Length];
		ClaimCheckMagicPrefix.CopyTo(result, 0);
		envelopeBytes.CopyTo(result, ClaimCheckMagicPrefix.Length);
		return result;
	}

	/// <summary>
	/// Strips the magic prefix from data to get the raw envelope bytes.
	/// </summary>
	private static byte[] StripMagicPrefix(byte[] data)
	{
		var envelopeBytes = new byte[data.Length - ClaimCheckMagicPrefix.Length];
		Array.Copy(data, ClaimCheckMagicPrefix.Length, envelopeBytes, 0, envelopeBytes.Length);
		return envelopeBytes;
	}
}
