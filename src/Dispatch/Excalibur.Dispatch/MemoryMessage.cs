// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// A message implementation that holds its content as Memory&lt;byte&gt; for zero-copy operations.
/// </summary>
public sealed class MemoryMessage : IMemoryMessage, IDisposable
{
	private static readonly IReadOnlyDictionary<string, object> EmptyHeaders =
		new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(0, StringComparer.Ordinal));

	private readonly IMemoryOwner<byte>? _memoryOwner;
	private DefaultMessageFeatures? _features;
	private Guid _id;
	private string? _messageId;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessage" /> class with owned memory.
	/// </summary>
	/// <param name="memoryOwner"> The memory owner that manages the message body. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessage(IMemoryOwner<byte> memoryOwner, string contentType = "application/octet-stream")
		: this(memoryOwner, memoryOwner?.Memory.Length ?? throw new ArgumentNullException(nameof(memoryOwner)), contentType)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessage" /> class with owned memory and explicit payload length.
	/// </summary>
	/// <param name="memoryOwner"> The memory owner that manages the message body. </param>
	/// <param name="payloadLength"> The valid payload length within <paramref name="memoryOwner" /> memory. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessage(IMemoryOwner<byte> memoryOwner, int payloadLength, string contentType = "application/octet-stream")
	{
		_memoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));
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
	/// Initializes a new instance of the <see cref="MemoryMessage" /> class with borrowed memory.
	/// </summary>
	/// <param name="body"> The message body as borrowed memory. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessage(Memory<byte> body, string contentType)
	{
		Body = body;
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		OwnsMemory = false;
		_memoryOwner = null;
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
	public string MessageType => nameof(MemoryMessage);

	/// <inheritdoc />
	public IMessageFeatures Features => _features ??=
		new DefaultMessageFeatures();

	/// <inheritdoc />
	public string ContentType { get; }

	/// <inheritdoc />
	public bool OwnsMemory { get; }

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
