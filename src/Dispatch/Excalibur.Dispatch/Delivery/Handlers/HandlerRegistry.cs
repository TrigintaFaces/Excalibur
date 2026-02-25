// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Thread-safe registry for dispatch handlers. Supports multiple handlers per message type
/// for event-based messaging patterns.
/// </summary>
public sealed class HandlerRegistry : IHandlerRegistry
{
	private readonly ConcurrentDictionary<Type, List<HandlerRegistryEntry>> _handlers = new();
	private readonly ConcurrentDictionary<Type, HandlerRegistryEntry[]> _handlerSnapshots = new();
#if NET9_0_OR_GREATER
	private readonly Lock _updateLock = new();
#else
	private readonly object _updateLock = new();
#endif

	/// <summary>
	/// Gets all registered handler entries.
	/// </summary>
	/// <returns> A read-only list of all registered handler entries. </returns>
	public IReadOnlyList<HandlerRegistryEntry> GetAll() =>
		_handlers.Values.SelectMany(static list => list).ToList().AsReadOnly();

	/// <summary>
	/// Registers a handler for the specified message type. For events (IDispatchEvent),
	/// multiple handlers can be registered for the same message type.
	/// </summary>
	/// <param name="messageType"> The type of message the handler processes. </param>
	/// <param name="handlerType"> The type of the handler. </param>
	/// <param name="expectsResponse"> Whether the handler is expected to return a response. </param>
	public void Register(Type messageType, Type handlerType, bool expectsResponse)
	{
		var entry = new HandlerRegistryEntry(messageType, handlerType, expectsResponse);
		HandlerRegistryEntry[]? snapshot = null;

		_ = _handlers.AddOrUpdate(
			messageType,
			_ =>
			{
				snapshot = [entry];
				return [entry];
			},
			(_, existing) =>
			{
				// Lock required: List<T> is not thread-safe for concurrent modifications.
				// Without this lock, concurrent Clear()/Add() calls can result in duplicate entries.
				lock (_updateLock)
				{
					// For events, allow multiple handlers; for commands/documents, replace
					if (typeof(IDispatchEvent).IsAssignableFrom(messageType))
					{
						// Avoid duplicate registrations of the same handler type
						if (!existing.Exists(e => e.HandlerType == handlerType))
						{
							existing.Add(entry);
						}
					}
					else
					{
						// Commands and documents: single handler (replace)
						existing.Clear();
						existing.Add(entry);
					}

					snapshot = [.. existing];
					return existing;
				}
			});

		if (snapshot is { Length: > 0 })
		{
			_handlerSnapshots[messageType] = snapshot;
		}
	}

	/// <summary>
	/// Attempts to get a handler entry for the specified message type.
	/// For message types with multiple handlers, returns the first registered handler.
	/// </summary>
	/// <param name="messageType"> The type of message to find a handler for. </param>
	/// <param name="entry"> When this method returns, contains the handler entry if found. </param>
	/// <returns> True if a handler was found; otherwise, false. </returns>
	public bool TryGetHandler(Type messageType, out HandlerRegistryEntry entry)
	{
		if (_handlerSnapshots.TryGetValue(messageType, out var snapshot) && snapshot.Length > 0)
		{
			entry = snapshot[0];
			return true;
		}

		if (_handlers.TryGetValue(messageType, out var list) && list.Count > 0)
		{
			entry = list[0];
			return true;
		}

		entry = default!;
		return false;
	}

	/// <summary>
	/// Attempts to get all handlers registered for the specified message type.
	/// </summary>
	/// <param name="messageType">The message type to look up.</param>
	/// <param name="entries">Registered handlers for the message type.</param>
	/// <returns><c>true</c> when at least one handler exists for the type.</returns>
	internal bool TryGetHandlers(Type messageType, out IReadOnlyList<HandlerRegistryEntry> entries)
	{
		if (_handlerSnapshots.TryGetValue(messageType, out var snapshot) && snapshot.Length > 0)
		{
			entries = snapshot;
			return true;
		}

		if (_handlers.TryGetValue(messageType, out var list) && list.Count > 0)
		{
			entries = list;
			return true;
		}

		entries = [];
		return false;
	}

	/// <summary>
	/// Attempts to get a snapshot array of handlers for the specified message type.
	/// Snapshot arrays are precomputed to remove first-hit fan-out allocation costs.
	/// </summary>
	internal bool TryGetHandlerSnapshot(Type messageType, out HandlerRegistryEntry[] entries)
	{
		if (_handlerSnapshots.TryGetValue(messageType, out var snapshot) && snapshot.Length > 0)
		{
			entries = snapshot;
			return true;
		}

		if (_handlers.TryGetValue(messageType, out var list) && list.Count > 0)
		{
			lock (_updateLock)
			{
				snapshot = [.. list];
			}

			_handlerSnapshots[messageType] = snapshot;
			entries = snapshot;
			return true;
		}

		entries = [];
		return false;
	}

	/// <summary>
	/// Precomputes immutable handler snapshots for all registered message types.
	/// </summary>
	internal void PrecomputeSnapshots()
	{
		lock (_updateLock)
		{
			foreach (var (messageType, handlers) in _handlers)
			{
				if (handlers.Count == 0)
				{
					continue;
				}

				_handlerSnapshots[messageType] = [.. handlers];
			}
		}
	}
}
