// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Queues;

/// <summary>
/// Simple in-memory implementation of IDistributedOrderedSetQueue.
/// </summary>
/// <typeparam name="T"> The type of items in the queue. </typeparam>
public sealed class SimpleOrderedSet<T> : IDistributedOrderedSetQueue<T>
	where T : notnull
{
	private readonly Channel<T> _channel;
	private readonly ConcurrentDictionary<T, byte> _set = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="SimpleOrderedSet{T}" /> class.
	/// </summary>
	/// <param name="capacity"> Optional capacity limit. </param>
	public SimpleOrderedSet(int? capacity = null)
	{
		_channel = capacity.HasValue
			? Channel.CreateBounded<T>(new BoundedChannelOptions(capacity.Value) { FullMode = BoundedChannelFullMode.Wait, })
			: Channel.CreateUnbounded<T>();
	}

	/// <inheritdoc />
	public async Task<bool> AddAsync(T item, CancellationToken cancellationToken)
	{
		if (_set.TryAdd(item, 0))
		{
			if (await _channel.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false) && _channel.Writer.TryWrite(item))
			{
				return true;
			}

			_ = _set.TryRemove(item, out _);
		}

		return false;
	}

	/// <inheritdoc />
	public Task<(bool Success, T? Item)> TryPopAsync(CancellationToken cancellationToken)
	{
		if (_channel.Reader.TryRead(out var item))
		{
			_ = _set.TryRemove(item, out _);
			return Task.FromResult((true, (T?)item));
		}

		return Task.FromResult((false, default(T?)));
	}

	/// <inheritdoc />
	public Task<bool> ContainsAsync(T item, CancellationToken cancellationToken) => Task.FromResult(_set.ContainsKey(item));

	/// <inheritdoc />
	public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_channel.Reader.Count);

	/// <inheritdoc />
	public Task<bool> RemoveAsync(T item, CancellationToken cancellationToken) => Task.FromResult(_set.TryRemove(item, out _));
}
