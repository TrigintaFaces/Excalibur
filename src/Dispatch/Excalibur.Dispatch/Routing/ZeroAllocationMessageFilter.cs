// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// High-performance message filter using delegates for zero-allocation filtering.
/// </summary>
public sealed class ZeroAllocationMessageFilter<TMessage>
{
	private readonly List<Func<TMessage, bool>> filters;

#if NET9_0_OR_GREATER


	private readonly Lock filterLock = new();


#else


	private readonly object filterLock = new();


#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="ZeroAllocationMessageFilter{TMessage}" /> class.
	/// </summary>
	public ZeroAllocationMessageFilter() => filters = new List<Func<TMessage, bool>>(capacity: 16);

	/// <summary>
	/// Adds a filter predicate.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddFilter(Func<TMessage, bool> filter)
	{
		ArgumentNullException.ThrowIfNull(filter);

		lock (filterLock)
		{
			filters.Add(filter);
		}
	}

	/// <summary>
	/// Filters messages in place with zero allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int FilterInPlace(Span<TMessage> messages)
	{
		if (messages.Length == 0 || filters.Count == 0)
		{
			return messages.Length;
		}

		var writeIndex = 0;

		// Process each message
		for (var readIndex = 0; readIndex < messages.Length; readIndex++)
		{
			var message = messages[readIndex];
			var passesAllFilters = true;

			// Check all filters using for loop instead of LINQ
			for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
			{
				if (!filters[filterIndex](message))
				{
					passesAllFilters = false;
					break;
				}
			}

			// Keep the message if it passes all filters
			if (passesAllFilters)
			{
				if (writeIndex != readIndex)
				{
					messages[writeIndex] = message;
				}

				writeIndex++;
			}
		}

		return writeIndex; // Return count of messages that passed filters
	}

	/// <summary>
	/// Filters messages and returns filtered results using ArrayPool.
	/// </summary>
	public (TMessage[] Results, int Count) FilterWithPool(ReadOnlySpan<TMessage> messages, ArrayPool<TMessage> pool)
	{
		if (messages.Length == 0)
		{
			return ([], 0);
		}

		// Rent array from pool
		var results = pool.Rent(messages.Length);
		var writeIndex = 0;

		try
		{
			// Process each message
			for (var i = 0; i < messages.Length; i++)
			{
				var message = messages[i];
				var passesAllFilters = true;

				// Check all filters
				for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
				{
					if (!filters[filterIndex](message))
					{
						passesAllFilters = false;
						break;
					}
				}

				if (passesAllFilters)
				{
					results[writeIndex++] = message;
				}
			}

			// Create properly sized result array
			var finalResults = new TMessage[writeIndex];
			Array.Copy(results, 0, finalResults, 0, writeIndex);

			// Return pooled array
			pool.Return(results, clearArray: true);

			return (finalResults, writeIndex);
		}
		catch
		{
			// Ensure we return the pooled array even on Exception
			pool.Return(results, clearArray: true);
			throw;
		}
	}
}
