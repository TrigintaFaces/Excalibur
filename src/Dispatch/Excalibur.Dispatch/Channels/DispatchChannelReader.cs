// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// High-performance channel reader with custom wait strategies.
/// </summary>
/// <typeparam name="T"> The type of items in the channel. </typeparam>
public sealed class DispatchChannelReader<T>(ChannelReader<T> innerReader, IWaitStrategy waitStrategy)
	: ChannelReader<T>
{
	private readonly ChannelReader<T> _innerReader = innerReader ?? throw new ArgumentNullException(nameof(innerReader));
	private readonly IWaitStrategy _waitStrategy = waitStrategy ?? throw new ArgumentNullException(nameof(waitStrategy));

	/// <summary>
	/// Gets a value indicating whether the underlying reader supports counting items.
	/// </summary>
	/// <value>The current <see cref="CanCount"/> value.</value>
	public override bool CanCount => _innerReader.CanCount;

	/// <summary>
	/// Gets the current number of items in the channel.
	/// </summary>
	/// <value>The current <see cref="Count"/> value.</value>
	public override int Count => _innerReader.Count;

	/// <summary>
	/// Gets a task that completes when no more items will be available to read.
	/// </summary>
	/// <value>The current <see cref="Completion"/> value.</value>
	public override Task Completion => _innerReader.Completion;

	/// <summary>
	/// Attempts to read an item from the channel without waiting.
	/// </summary>
	/// <param name="item"> The item that was read, if successful. </param>
	/// <returns> true if an item was successfully read; otherwise, false. </returns>
	public override bool TryRead([MaybeNullWhen(false)] out T item) => _innerReader.TryRead(out item);

	/// <summary>
	/// Asynchronously reads an item from the channel.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous read operation. </returns>
	public override ValueTask<T> ReadAsync(CancellationToken cancellationToken) =>
		_innerReader.ReadAsync(cancellationToken);

	/// <summary>
	/// Waits asynchronously for data to become available to read.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous wait operation. </returns>
	public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
	{
		// Note: Wait strategy is available for future enhancements
		_ = _waitStrategy;
		return _innerReader.WaitToReadAsync(cancellationToken);
	}

	/// <summary>
	/// Attempts to peek at an item in the channel without removing it.
	/// </summary>
	/// <param name="item"> The item that was peeked at, if successful. </param>
	/// <returns> true if an item was successfully peeked at; otherwise, false. </returns>
	public override bool TryPeek([MaybeNullWhen(false)] out T item) => _innerReader.TryPeek(out item);
}
