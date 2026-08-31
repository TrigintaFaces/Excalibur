// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// In-memory state store for Firestore CDC position tracking.
/// </summary>
/// <remarks>
/// This implementation is intended for testing and development.
/// Positions are not persisted and will be lost when the process exits.
/// </remarks>
internal sealed class InMemoryFirestoreCdcStateStore : IFirestoreCdcStateStore
{
	private readonly ConcurrentDictionary<string, FirestoreCdcStateEntry> _positions = new();
	private volatile bool _disposed;

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	public Task<FirestoreCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		if (!_positions.TryGetValue(processorName, out var entry))
		{
			return Task.FromResult<FirestoreCdcPosition?>(null);
		}

		if (!FirestoreCdcPosition.TryFromBase64(entry.PositionData, out var position))
		{
			return Task.FromResult<FirestoreCdcPosition?>(null);
		}

		return Task.FromResult(position);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	public Task SavePositionAsync(
		string processorName,
		FirestoreCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ArgumentNullException.ThrowIfNull(position);

		var entry = new FirestoreCdcStateEntry
		{
			ProcessorName = processorName,
			PositionData = position.ToBase64(),
			UpdatedAt = DateTimeOffset.UtcNow,
			EventCount = 0,
		};

		_positions[processorName] = entry;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		_ = _positions.TryRemove(processorName, out _);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets all stored positions.
	/// </summary>
	/// <returns>A read-only dictionary of processor names to state entries.</returns>
	public IReadOnlyDictionary<string, FirestoreCdcStateEntry> GetAllPositions()
	{
		return _positions;
	}

	/// <summary>
	/// Clears all stored positions.
	/// </summary>
	public void Clear()
	{
		_positions.Clear();
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await GetPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		if (position is not FirestoreCdcPosition firestorePosition)
		{
			firestorePosition = FirestoreCdcPosition.FromBase64(position.ToToken());
		}

		return SavePositionAsync(consumerId, firestorePosition, cancellationToken);
	}

	/// <inheritdoc/>
	Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);

		// Report whether a checkpoint was actually removed. Delegating to the Firestore-shaped overload
		// and returning a hardcoded true said "deleted" for a consumer that never had a checkpoint --
		// the shared contract uses this bool to distinguish those two cases, and every other provider
		// does. TryRemove already answers the question; the void-returning overload discards it.
		return Task.FromResult(_positions.TryRemove(consumerId, out _));
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await Task.CompletedTask.ConfigureAwait(false);

		foreach (var kvp in _positions)
		{
			if (FirestoreCdcPosition.TryFromBase64(kvp.Value.PositionData, out var position) && position is not null)
			{
				yield return (kvp.Key, position);
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		_disposed = true;
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_disposed = true;
	}
}
