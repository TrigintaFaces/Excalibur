// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Message serializer that implements the Claim Check pattern for large messages. Small messages are serialized normally, while large
/// messages are stored in external storage and replaced with a small reference.
/// </summary>
/// <remarks>
/// <para> Initializes a new instance of the <see cref="ClaimCheckMessageSerializer" /> class. </para>
/// <para>
/// <b>Wire format ([tag:1][body]).</b> Every payload this serializer emits is prefixed with a single leading
/// frame-tag byte that the ClaimCheck layer exclusively owns: <c>0x00</c> = an inline (non-offloaded) payload,
/// <c>0x01</c> = a claim-check envelope (reference to external storage). Classification is therefore unambiguous
/// by construction — a collision between an inline payload and the envelope marker is structurally inexpressible,
/// regardless of the base serializer's binary format (the previous in-band <c>"CC01"</c> magic-prefix heuristic
/// could mis-classify an inline payload whose bytes happened to begin with that sequence). Readers switch on
/// byte 0 and reject any payload they did not write (unknown tag or empty input → typed
/// <see cref="SerializationException"/>); the reader contract is "I only read what I wrote".
/// </para>
/// </remarks>
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
	/// Leading frame-tag byte for an inline (non-offloaded) payload: <c>[0x00][body]</c>.
	/// </summary>
	private const byte InlineFrameTag = 0x00;

	/// <summary>
	/// Leading frame-tag byte for a claim-check envelope payload: <c>[0x01][envelope]</c>.
	/// </summary>
	private const byte EnvelopeFrameTag = 0x01;

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

		// [tag:1][body] framing: byte 0 is the inline tag the ClaimCheck layer owns, so the reader never has to
		// guess inline-vs-envelope from the base payload's leading bytes.
		var span = bufferWriter.GetSpan(payload.Length + 1);
		span[0] = InlineFrameTag;
		payload.CopyTo(span[1..]);
		bufferWriter.Advance(payload.Length + 1);
	}

	/// <inheritdoc />
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		if (data.IsEmpty)
		{
			throw SerializationException.EmptyPayload();
		}

		var tag = data[0];
		return tag switch
		{
			InlineFrameTag => _baseSerializer.Deserialize<T>(data[1..]),
			EnvelopeFrameTag => throw new NotSupportedException(
				"Data contains a claim check envelope reference. Use DeserializeAsync to retrieve the payload from external storage."),
			_ => throw UnrecognizedFrameTag(tag),
		};
	}

	/// <inheritdoc />
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		// The runtime-typed object channel never offloads, so it is always an inline frame. Framing it (rather than
		// emitting raw base bytes) keeps the "ClaimCheck owns byte 0 of every payload it emits" invariant total and
		// prevents a silent cross-method mismatch with the generic Serialize/Deserialize path.
		var payload = _baseSerializer.SerializeObject(value, type);
		return PrependFrameTag(InlineFrameTag, payload);
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (data.IsEmpty)
		{
			throw SerializationException.EmptyPayload();
		}

		var tag = data[0];
		return tag switch
		{
			InlineFrameTag => _baseSerializer.DeserializeObject(data[1..], type),
			EnvelopeFrameTag => throw new NotSupportedException(
				"Data contains a claim check envelope reference, which the runtime-typed object channel cannot resolve. " +
				"Use DeserializeAsync<T> to retrieve the payload from external storage."),
			_ => throw UnrecognizedFrameTag(tag),
		};
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
			// Inline frame: [0x00][payload].
			return PrependFrameTag(InlineFrameTag, payload);
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

		// Envelope frame: [0x01][envelope] so receivers classify by the tag byte, never by guessing leading bytes.
		return PrependFrameTag(EnvelopeFrameTag, envelopeBytes);
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

		if (data.Length == 0)
		{
			throw SerializationException.EmptyPayload();
		}

		var tag = data[0];
		if (tag == InlineFrameTag)
		{
			return _baseSerializer.Deserialize<T>(data.AsSpan(1));
		}

		if (tag != EnvelopeFrameTag)
		{
			throw UnrecognizedFrameTag(tag);
		}

		// Strip the frame tag and resolve the envelope reference.
		var envelope = _baseSerializer.Deserialize<ClaimCheckEnvelope>(data.AsSpan(1));
		var storedPayload = await _claimCheckProvider.RetrieveAsync(envelope.Reference, cancellationToken).ConfigureAwait(false);
		return _baseSerializer.Deserialize<T>(storedPayload);
	}

	/// <summary>
	/// Allocates a new buffer of <c>[tag][body]</c> with the frame tag prepended.
	/// </summary>
	private static byte[] PrependFrameTag(byte tag, byte[] body)
	{
		var result = new byte[body.Length + 1];
		result[0] = tag;
		body.CopyTo(result, 1);
		return result;
	}

	/// <summary>
	/// Builds the typed error for a payload whose leading frame tag is not one this serializer emits.
	/// </summary>
	private static SerializationException UnrecognizedFrameTag(byte tag)
		=> new($"Unrecognized ClaimCheck frame tag 0x{tag:X2} — payload was not produced by ClaimCheckMessageSerializer; " +
			"check that the same serializer is used to write and read (serializer pairing).")
		{
			SerializerId = tag,
			Operation = SerializationOperation.Deserialize,
		};
}
