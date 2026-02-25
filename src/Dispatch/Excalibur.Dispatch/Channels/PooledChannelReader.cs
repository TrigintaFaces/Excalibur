// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.ObjectPool;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// A channel reader that automatically wraps messages in pooled envelopes.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PooledChannelReader" /> class. </remarks>
/// <param name="innerReader"> The underlying channel reader. </param>
/// <param name="messagePool"> The message pool to use for pooling. </param>
public sealed class PooledChannelReader(
	ChannelReader<MessageEnvelope> innerReader,
	ObjectPool<IDispatchMessage> messagePool) : ChannelReader<MessageEnvelope>
{
	private readonly ChannelReader<MessageEnvelope> _innerReader = innerReader ?? throw new ArgumentNullException(nameof(innerReader));
	// Reserved for future pooling implementation

#pragma warning restore IDE0052, CA1823

	/// <inheritdoc />
	public override bool CanCount => _innerReader.CanCount;

	/// <inheritdoc />
	public override int Count => _innerReader.Count;

	/// <inheritdoc />
	public override Task Completion => _innerReader.Completion;

	/// <inheritdoc />
	public override bool TryRead(out MessageEnvelope item)
	{
		if (_innerReader.TryRead(out var envelope))
		{
			item = envelope; // MessageEnvelope is not poolable
			return true;
		}

		item = null!;
		return false;
	}

	/// <inheritdoc />
	public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
		_innerReader.WaitToReadAsync(cancellationToken);

	/// <inheritdoc />
	public override bool TryPeek(out MessageEnvelope item)
	{
		// We can't peek with pooling as it would create a pooled wrapper without the ability to dispose it properly
		item = null!;
		return false;
	}

	/// <inheritdoc />
	public override async IAsyncEnumerable<MessageEnvelope> ReadAllAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var envelope in _innerReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return envelope; // MessageEnvelope is not poolable
		}
	}
}
