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
/// The base serializer for normal serialization.
/// </param>
/// <param name="options"> The claim check options. If null, uses default options. </param>
[RequiresUnreferencedCode("Claim check serialization may require types that cannot be statically analyzed.")]
[RequiresDynamicCode("Claim check serialization may require runtime code generation.")]
public sealed class ClaimCheckMessageSerializer(
	IClaimCheckProvider claimCheckProvider,
	ISerializer baseSerializer,
	ClaimCheckOptions? options = null) : ISerializer
{
	/// <summary>
	/// Magic byte prefix for claim check envelope data: ASCII "CC01" (0x43, 0x43, 0x30, 0x31).
	/// Used for fast format detection without exception-driven control flow.
	/// </summary>
	private static readonly byte[] ClaimCheckMagicPrefix = "CC01"u8.ToArray();

	private readonly IClaimCheckProvider _claimCheckProvider =
		claimCheckProvider ?? throw new ArgumentNullException(nameof(claimCheckProvider));

	private readonly ISerializer _baseSerializer =
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
	public string Name => $"ClaimCheck-{_baseSerializer.Name}";

	/// <inheritdoc />
	public string Version => "1.0.0";

	/// <inheritdoc />
	public string ContentType => _baseSerializer.ContentType;

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		var payload = _baseSerializer.SerializeToBytes(value);

		if (payload.Length > _options.PayloadThreshold)
		{
			throw new NotSupportedException(
				$"Message payload ({payload.Length} bytes) exceeds the claim check threshold ({_options.PayloadThreshold} bytes). " +
				"Use SerializeAsync to enable the claim check pattern for large messages.");
		}

		var span = bufferWriter.GetSpan(payload.Length);
		payload.CopyTo(span);
		bufferWriter.Advance(payload.Length);
	}

	/// <inheritdoc />
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		// Check for claim check magic prefix to detect envelope format without exceptions
		if (data.Length >= ClaimCheckMagicPrefix.Length &&
			data.Slice(0, ClaimCheckMagicPrefix.Length).SequenceEqual(ClaimCheckMagicPrefix))
		{
			throw new NotSupportedException(
				"Data contains a claim check envelope reference. Use DeserializeAsync to retrieve the payload from external storage.");
		}

		return _baseSerializer.Deserialize<T>(data);
	}

	/// <inheritdoc />
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);
		return _baseSerializer.SerializeObject(value, type);
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return _baseSerializer.DeserializeObject(data, type);
	}

	/// <summary>
	/// Serializes a message to bytes asynchronously, using claim check for large payloads.
	/// </summary>
	public async ValueTask<byte[]> SerializeAsync<T>(
		T message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		var payload = _baseSerializer.SerializeToBytes(message);

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

		var envelopeBytes = _baseSerializer.SerializeToBytes(new ClaimCheckEnvelope
		{
			Reference = reference,
			MessageType = typeof(T).Name,
			SerializerName = _baseSerializer.Name,
			OriginalSize = payload.Length,
		});

		// Prepend magic prefix so receivers can detect claim check envelopes without exception-driven control flow
		return PrependMagicPrefix(envelopeBytes);
	}

	/// <summary>
	/// Deserializes bytes to a message asynchronously, resolving claim check references.
	/// </summary>
	public async ValueTask<T> DeserializeAsync<T>(
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
