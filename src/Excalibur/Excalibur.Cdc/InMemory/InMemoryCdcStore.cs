// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// In-memory implementation of the CDC change store for testing scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This store is thread-safe and suitable for concurrent access in testing scenarios.
/// </para>
/// </remarks>
public sealed class InMemoryCdcStore : IInMemoryCdcStore
{
	private readonly ConcurrentQueue<InMemoryCdcChange> _pendingChanges = new();
	private readonly ConcurrentBag<InMemoryCdcChange> _processedChanges = [];
	private readonly InMemoryCdcOptions _options;

#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();
#else
	private readonly object _lock = new();
#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCdcStore"/> class.
	/// </summary>
	/// <param name="options">The in-memory CDC options.</param>
	public InMemoryCdcStore(IOptions<InMemoryCdcOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCdcStore"/> class with default options.
	/// </summary>
	public InMemoryCdcStore()
		: this(Options.Create(new InMemoryCdcOptions()))
	{
	}

	/// <inheritdoc/>
	public void AddChange(InMemoryCdcChange change)
	{
		ArgumentNullException.ThrowIfNull(change);
		_pendingChanges.Enqueue(change);
	}

	/// <inheritdoc/>
	public void AddChanges(IEnumerable<InMemoryCdcChange> changes)
	{
		ArgumentNullException.ThrowIfNull(changes);
		foreach (var change in changes)
		{
			AddChange(change);
		}
	}

	/// <inheritdoc/>
	public IReadOnlyList<InMemoryCdcChange> GetPendingChanges(int maxCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

		var result = new List<InMemoryCdcChange>(Math.Min(maxCount, _pendingChanges.Count));

		lock (_lock)
		{
			while (result.Count < maxCount && _pendingChanges.TryDequeue(out var change))
			{
				result.Add(change);
			}
		}

		return result;
	}

	/// <inheritdoc/>
	public void MarkAsProcessed(IEnumerable<InMemoryCdcChange> changes)
	{
		ArgumentNullException.ThrowIfNull(changes);

		if (!_options.PreserveHistory)
		{
			return;
		}

		foreach (var change in changes)
		{
			_processedChanges.Add(change);
		}
	}

	/// <inheritdoc/>
	public void Clear()
	{
		lock (_lock)
		{
			while (_pendingChanges.TryDequeue(out _))
			{
				// Drain the queue
			}

			_processedChanges.Clear();
		}
	}

	/// <inheritdoc/>
	public int GetPendingCount() => _pendingChanges.Count;

	/// <inheritdoc/>
	public IReadOnlyList<InMemoryCdcChange> GetHistory() => [.. _processedChanges];
}
