// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// In-memory implementation of <see cref="ICursorMapStore"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// Cursor maps are stored in a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// and are lost when the process restarts. For production use, use a durable
/// implementation such as <c>SqlServerCursorMapStore</c> or <c>PostgresCursorMapStore</c>.
/// </para>
/// </remarks>
internal sealed class InMemoryCursorMapStore : ICursorMapStore
{
	private readonly ConcurrentDictionary<string, ReadOnlyDictionary<string, long>> _cursors = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		IReadOnlyDictionary<string, long> result = _cursors.TryGetValue(projectionName, out var map)
			? map
			: ReadOnlyDictionary<string, long>.Empty;

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task SaveCursorMapAsync(
		string projectionName,
		IReadOnlyDictionary<string, long> cursorMap,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);
		ArgumentNullException.ThrowIfNull(cursorMap);

		// Atomic replace: store an immutable snapshot
		_cursors[projectionName] = new ReadOnlyDictionary<string, long>(
			new Dictionary<string, long>(cursorMap));

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task ResetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		_cursors.TryRemove(projectionName, out _);

		return Task.CompletedTask;
	}
}
