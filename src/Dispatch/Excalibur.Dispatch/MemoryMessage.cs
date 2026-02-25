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
	private readonly IMemoryOwner<byte>? _memoryOwner;
	private readonly Dictionary<string, object> _headers = [];
	private readonly DefaultMessageFeatures _features = new();
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryMessage" /> class with owned memory.
	/// </summary>
	/// <param name="memoryOwner"> The memory owner that manages the message body. </param>
	/// <param name="contentType"> The content type of the message body. </param>
	public MemoryMessage(IMemoryOwner<byte> memoryOwner, string contentType = "application/octet-stream")
	{
		_memoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));
		Body = _memoryOwner.Memory;
		ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
		OwnsMemory = true;
		MessageId = Guid.NewGuid().ToString();
		Timestamp = DateTimeOffset.UtcNow;
		Headers = new ReadOnlyDictionary<string, object>(_headers);
		MessageType = GetType().Name;
		Features = _features;
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
		MessageId = Guid.NewGuid().ToString();
		Timestamp = DateTimeOffset.UtcNow;
		Headers = new ReadOnlyDictionary<string, object>(_headers);
		MessageType = GetType().Name;
		Features = _features;
	}

	/// <inheritdoc />
	public string MessageId { get; }

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; }

	/// <inheritdoc />
	public Memory<byte> Body { get; }

	/// <inheritdoc />
	public string MessageType { get; }

	/// <inheritdoc />
	public IMessageFeatures Features { get; }

	/// <inheritdoc />
	public string ContentType { get; }

	/// <inheritdoc />
	public bool OwnsMemory { get; }

	/// <inheritdoc />
	public Guid Id => Guid.TryParse(MessageId, out var guid) ? guid : Guid.Empty;

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
}
