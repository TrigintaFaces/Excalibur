// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Extension methods for batch reading from channels to improve CPU efficiency. Identified as 3-5% CPU improvement opportunity in profiling.
/// </summary>
public static class ChannelBatchExtensions
{
	/// <summary>
	/// Try to read multiple items from a channel in a single operation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int TryReadMany<T>(
		this ChannelReader<T> reader,
		Span<T> items)
	{
		ArgumentNullException.ThrowIfNull(reader);

		var count = 0;

		while (count < items.Length && reader.TryRead(out var item))
		{
			items[count++] = item;
		}

		return count;
	}

	/// <summary>
	/// Try to read multiple items into a collection.
	/// </summary>
	public static int TryReadMany<T>(
		this ChannelReader<T> reader,
		Collection<T> items,
		int maxCount)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentNullException.ThrowIfNull(items);

		var count = 0;
		items.Clear();

		while (count < maxCount && reader.TryRead(out var item))
		{
			items.Add(item);
			count++;
		}

		return count;
	}

	/// <summary>
	/// Read items in batches asynchronously.
	/// </summary>
	public static IAsyncEnumerable<IReadOnlyList<T>> ReadBatchesAsync<T>(
		this ChannelReader<T> reader,
		int batchSize,
		TimeSpan batchTimeout,
		CancellationToken cancellationToken) =>
		new BatchChannelReader<T>(reader, batchSize, batchTimeout)
			.ReadBatchesAsync(cancellationToken);

	/// <summary>
	/// Wait to read with a batch hint for better performance.
	/// </summary>
	public static async ValueTask<BatchReadResult<T>> WaitToReadBatchAsync<T>(
		this ChannelReader<T> reader,
		int desiredBatchSize,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);

		// Wait for at least one item
		if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			return new BatchReadResult<T>([], hasItems: false);
		}

		// Try to read up to desiredBatchSize items
		var items = new T[desiredBatchSize];
		var count = reader.TryReadMany(items);

		if (count < desiredBatchSize)
		{
			// Return only what we got
			var result = new T[count];
			Array.Copy(items, result, count);
			return new BatchReadResult<T>(result, hasItems: true);
		}

		return new BatchReadResult<T>(items, hasItems: true);
	}

	/// <summary>
	/// Try to read multiple items into a list (internal for performance-critical paths).
	/// </summary>
	internal static int TryReadMany<T>(
		this ChannelReader<T> reader,
		List<T> items,
		int maxCount)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentNullException.ThrowIfNull(items);

		var count = 0;
		items.Clear();

		while (count < maxCount && reader.TryRead(out var item))
		{
			items.Add(item);
			count++;
		}

		return count;
	}
}
