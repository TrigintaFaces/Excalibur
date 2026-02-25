// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// High-performance channel writer with custom wait strategies.
/// </summary>
/// <typeparam name="T"> The type of items in the channel. </typeparam>
public sealed class DispatchChannelWriter<T>(ChannelWriter<T> innerWriter, IWaitStrategy waitStrategy)
	: ChannelWriter<T>
{
	private readonly ChannelWriter<T> _innerWriter = innerWriter ?? throw new ArgumentNullException(nameof(innerWriter));
	private readonly IWaitStrategy _waitStrategy = waitStrategy ?? throw new ArgumentNullException(nameof(waitStrategy));

	/// <summary>
	/// Attempts to write an item to the channel without waiting.
	/// </summary>
	/// <param name="item"> The item to write to the channel. </param>
	/// <returns> true if the item was successfully written; otherwise, false. </returns>
	public override bool TryWrite(T item) => _innerWriter.TryWrite(item);

	/// <summary>
	/// Waits asynchronously for space to become available to write.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous wait operation. </returns>
	public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken)
	{
		// Note: Wait strategy is available for future enhancements
		_ = _waitStrategy;
		return _innerWriter.WaitToWriteAsync(cancellationToken);
	}

	/// <summary>
	/// Asynchronously writes an item to the channel.
	/// </summary>
	/// <param name="item"> The item to write to the channel. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous write operation. </returns>
	public override ValueTask WriteAsync(T item, CancellationToken cancellationToken) =>
		_innerWriter.WriteAsync(item, cancellationToken);

	/// <summary>
	/// Attempts to mark the channel as completed, preventing further writes.
	/// </summary>
	/// <param name="error"> An optional error to associate with the completion. </param>
	/// <returns> true if the channel was successfully completed; otherwise, false. </returns>
	public override bool TryComplete(Exception? error = null) => _innerWriter.TryComplete(error);
}
