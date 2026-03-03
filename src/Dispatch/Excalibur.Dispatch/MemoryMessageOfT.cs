// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// A typed message implementation that provides both Memory&lt;byte&gt; and typed access to content.
/// </summary>
/// <typeparam name="T"> The type of the message content. </typeparam>
public sealed class MemoryMessageOfT<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : IMemoryMessage<T>, IDisposable
	where T : class
{
	private static readonly string MessageTypeName = typeof(T).Name;
	private static readonly IReadOnlyDictionary<string, object> EmptyHeaders =
		new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(0, StringComparer.Ordinal));

	private readonly IMemoryOwner<byte>? _memoryOwner;
	private readonly IUtf8JsonSerializer? _serializer;
	private DefaultMessageFeatures? _features;
	private Guid _id;
	private string? _messageId;
	private T? _content;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessageOfT{T}" /> class with owned memory.
	/// </summary>
	/// <param name="memoryOwner"> The memory owner that manages the message body. </param>
	/// <param name="serializer"> The serializer for deserializing content. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessageOfT(
		IMemoryOwner<byte> memoryOwner,
		IUtf8JsonSerializer serializer,
		string contentType = "application/json")
		: this(memoryOwner, serializer, memoryOwner?.Memory.Length ?? throw new ArgumentNullException(nameof(memoryOwner)), contentType)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessageOfT{T}" /> class with owned memory and explicit payload length.
	/// </summary>
	/// <param name="memoryOwner"> The memory owner that manages the message body. </param>
	/// <param name="serializer"> The serializer for deserializing content. </param>
	/// <param name="payloadLength"> The valid payload length within <paramref name="memoryOwner" /> memory. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessageOfT(
		IMemoryOwner<byte> memoryOwner,
		IUtf8JsonSerializer serializer,
		int payloadLength,
		string contentType = "application/json")
	{
		_memoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		if ((uint)payloadLength > (uint)_memoryOwner.Memory.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(payloadLength));
		}

		Body = _memoryOwner.Memory.Slice(0, payloadLength);
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		OwnsMemory = true;
		Timestamp = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessageOfT{T}" /> class with borrowed memory.
	/// </summary>
	/// <param name="body"> The message body as borrowed memory. </param>
	/// <param name="serializer"> The serializer for deserializing content. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessageOfT(
		Memory<byte> body,
		IUtf8JsonSerializer serializer,
		string contentType)
	{
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		Body = body;
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		OwnsMemory = false;
		_memoryOwner = null;
		Timestamp = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessageOfT{T}" /> class with pre-deserialized content.
	/// </summary>
	/// <param name="content"> The pre-deserialized content. </param>
	/// <param name="body"> The message body as memory. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessageOfT(T content, Memory<byte> body, string contentType)
	{
		_content = content ?? throw new ArgumentNullException(nameof(content));
		Body = body;
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		OwnsMemory = false;
		IsDeserialized = true;
		_memoryOwner = null;
		_serializer = null;
		Timestamp = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessageOfT{T}" /> class with pre-deserialized content and owned memory.
	/// </summary>
	/// <param name="content">The pre-deserialized content.</param>
	/// <param name="memoryOwner">The memory owner that manages the message body.</param>
	/// <param name="payloadLength">The valid payload length within <paramref name="memoryOwner" /> memory.</param>
	/// <param name="contentType">The content type of the message body.</param>
	public MemoryMessageOfT(T content, IMemoryOwner<byte> memoryOwner, int payloadLength, string contentType = "application/json")
	{
		_content = content ?? throw new ArgumentNullException(nameof(content));
		_memoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));
		if ((uint)payloadLength > (uint)_memoryOwner.Memory.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(payloadLength));
		}

		Body = _memoryOwner.Memory.Slice(0, payloadLength);
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		OwnsMemory = true;
		IsDeserialized = true;
		_serializer = null;
		Timestamp = DateTimeOffset.UtcNow;
	}

	/// <inheritdoc />
	public string MessageId => _messageId ??= EnsureId().ToString();

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers => EmptyHeaders;

	/// <inheritdoc />
	public Memory<byte> Body { get; }

	/// <inheritdoc />
	public string MessageType => MessageTypeName;

	/// <inheritdoc />
	public IMessageFeatures Features => _features ??=
		new DefaultMessageFeatures();

	/// <inheritdoc />
	public string ContentType { get; }

	/// <inheritdoc />
	public bool OwnsMemory { get; }

	/// <inheritdoc />
	public T Content
	{
		get
		{
			if (!IsDeserialized)
			{
				if (_serializer == null)
				{
					throw new InvalidOperationException(Resources.MemoryMessageOfT_CannotDeserializeWithoutSerializer);
				}

				_content = _serializer.DeserializeFromUtf8<T>(Body.Span);
				IsDeserialized = true;
			}

			return _content!;
		}
	}

	/// <inheritdoc />
	public bool IsDeserialized { get; private set; }

	/// <inheritdoc />
	public Guid Id => EnsureId();

	/// <inheritdoc />
	public MessageKinds Kind => MessageKinds.Action;

	/// <summary>
	/// Disposes the message and returns any owned memory to the pool.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing && OwnsMemory)
		{
			_memoryOwner?.Dispose();
		}

		_disposed = true;
	}

	private Guid EnsureId()
	{
		if (_id != Guid.Empty)
		{
			return _id;
		}

		if (_messageId is not null &&
			Guid.TryParse(_messageId, out var parsedId))
		{
			_id = parsedId;
			return parsedId;
		}

		_id = Guid.NewGuid();
		return _id;
	}
}
