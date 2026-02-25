// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Channel reader that batches items for improved throughput.
/// </summary>
public sealed class BatchChannelReader<T>(ChannelReader<T> reader, int batchSize, TimeSpan batchTimeout)
{
	private readonly ChannelReader<T> _reader = reader ?? throw new ArgumentNullException(nameof(reader));
	private readonly int _batchSize = batchSize > 0 ? batchSize : throw new ArgumentOutOfRangeException(nameof(batchSize));

	private readonly TimeSpan _batchTimeout =
		batchTimeout > TimeSpan.Zero ? batchTimeout : throw new ArgumentOutOfRangeException(nameof(batchTimeout));

	private readonly List<T> _buffer = new(batchSize);

	/// <summary>
	/// Reads batches of items from the underlying channel reader.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation. </param>
	/// <returns> An async enumerable of batches containing items. </returns>
	public async IAsyncEnumerable<IReadOnlyList<T>> ReadBatchesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		using var timeoutCts = new CancellationTokenSource();
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		while (!cancellationToken.IsCancellationRequested)
		{
			_buffer.Clear();
			timeoutCts.CancelAfter(_batchTimeout);

			var shouldYieldBatch = false;
			var shouldBreak = false;
			T[]? batchToYield = null;

			try
			{
				// Try to fill the batch
				while (_buffer.Count < _batchSize)
				{
					if (await _reader.WaitToReadAsync(linkedCts.Token).ConfigureAwait(false))
					{
						if (_reader.TryRead(out var item))
						{
							_buffer.Add(item);
						}
					}
					else
					{
						// Channel completed
						if (_buffer.Count > 0)
						{
							shouldYieldBatch = true;
							batchToYield = [.. _buffer];
						}

						shouldBreak = true;
						break;
					}
				}

				// Yield the batch
				if (!shouldBreak && _buffer.Count > 0)
				{
					shouldYieldBatch = true;
					batchToYield = [.. _buffer];
				}
			}
			catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
			{
				// Timeout reached, yield what we have
				if (_buffer.Count > 0)
				{
					shouldYieldBatch = true;
					batchToYield = [.. _buffer];
				}
			}

			if (shouldYieldBatch && batchToYield != null)
			{
				yield return batchToYield;
			}

			if (shouldBreak)
			{
				yield break;
			}

			_ = timeoutCts.TryReset();
		}
	}
}
