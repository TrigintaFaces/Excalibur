// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Thread-safe registry for dispatch handlers. Supports multiple handlers per message type
/// for event-based messaging patterns.
/// </summary>
internal sealed class HandlerRegistry : IHandlerRegistry
{
	private readonly ConcurrentDictionary<Type, List<HandlerRegistryEntry>> _handlers = new();
	private readonly ConcurrentDictionary<Type, HandlerRegistryEntry[]> _handlerSnapshots = new();
	private readonly Lock _updateLock = new();

	/// <inheritdoc />
	IReadOnlyList<IHandlerRegistryEntry> IHandlerRegistry.GetAll() => GetAll();

	/// <inheritdoc />
	bool IHandlerRegistry.TryGetHandlers(Type messageType, out IReadOnlyList<IHandlerRegistryEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		// Overrides the interface's GetAll()-filtering default: this registry indexes by message type, so the
		// answer is a dictionary hit rather than a scan of every registration in the application.
		//
		// THE COPY IS DELIBERATE, and it is defensive rather than required. The conversion itself does not
		// need it -- IReadOnlyList<T> is covariant, so the stored list converts directly. What needs it is
		// that this is a PUBLIC seam: the stored snapshot is a HandlerRegistryEntry[], array covariance makes
		// it reachable as an IHandlerRegistryEntry[], and a caller that casts it back can write through it
		// into the registry's live state. Handing out a copy costs one small array per call and removes that
		// surface entirely. Do not delete it for the allocation without replacing it with a wrapper that is
		// equally non-aliasing.
		if (TryGetHandlers(messageType, out var concrete))
		{
			entries = [.. concrete];
			return true;
		}

		entries = [];
		return false;
	}

	/// <inheritdoc />
	bool IHandlerRegistry.TryGetHandler(Type messageType, out IHandlerRegistryEntry entry)
	{
		var found = TryGetHandler(messageType, out var concrete);
		entry = concrete;
		return found;
	}

	/// <summary>
	/// Gets all registered handler entries.
	/// </summary>
	/// <returns> A read-only list of all registered handler entries. </returns>
	public IReadOnlyList<HandlerRegistryEntry> GetAll()
	{
		if (!_handlerSnapshots.IsEmpty)
		{
			var totalEntries = 0;
			foreach (var snapshot in _handlerSnapshots.Values)
			{
				totalEntries += snapshot.Length;
			}

			var entries = new List<HandlerRegistryEntry>(Math.Max(totalEntries, 0));
			foreach (var snapshot in _handlerSnapshots.Values)
			{
				entries.AddRange(snapshot);
			}

			return entries.AsReadOnly();
		}

		lock (_updateLock)
		{
			var totalEntries = 0;
			foreach (var handlers in _handlers.Values)
			{
				totalEntries += handlers.Count;
			}

			var entries = new List<HandlerRegistryEntry>(Math.Max(totalEntries, 0));
			foreach (var handlers in _handlers.Values)
			{
				for (var i = 0; i < handlers.Count; i++)
				{
					entries.Add(handlers[i]);
				}
			}

			return entries.AsReadOnly();
		}
	}

	/// <summary>
	/// Returns the implementation type a descriptor contributes to the index, or throws when it has none.
	/// </summary>
	/// <param name="serviceType">The handler interface the descriptor registers.</param>
	/// <param name="implementationType">The descriptor's implementation type, if it declares one.</param>
	/// <param name="implementationInstance">The descriptor's implementation instance, if it holds one.</param>
	/// <returns>The type to index the handler under.</returns>
	/// <exception cref="InvalidOperationException">
	/// The descriptor declares neither an implementation type nor an instance, so nothing can be indexed.
	/// </exception>
	/// <remarks>
	/// The guard lives on the index because the index defines what is indexable. Refusing is deliberate:
	/// the alternative, and what this previously did, is to skip the descriptor and leave the consumer
	/// with a handler that is registered, resolvable, and silently never reached — surfacing much later
	/// as a dispatch-time "no handler registered" for an action, and as nothing at all for an event,
	/// since publishing to zero handlers is legal.
	/// </remarks>
	public static Type RequireIndexableHandlerType(
		Type serviceType,
		Type? implementationType,
		object? implementationInstance)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return implementationType
			?? implementationInstance?.GetType()
			?? throw new InvalidOperationException(
				$"The handler registered for '{serviceType}' was created by a factory delegate, so it has "
				+ "no implementation type and cannot be added to the handler index — it would never be "
				+ $"invoked. Register the handler by type instead, as services.AddScoped<{serviceType}, "
				+ "YourHandler>(), or register an instance. If the handler needs values that are not "
				+ "services, register those values as their own service and let the container construct "
				+ "the handler.");
	}

	/// <summary>
	/// Registers a handler for the specified message type. For events (IDispatchEvent),
	/// multiple handlers can be registered for the same message type.
	/// </summary>
	/// <param name="messageType"> The type of message the handler processes. </param>
	/// <param name="handlerType"> The type of the handler. </param>
	/// <param name="expectsResponse"> Whether the handler is expected to return a response. </param>
	/// <param name="responseType"> The handler's response type, when known up front; otherwise <see langword="null" />. </param>
	public void Register(Type messageType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType, bool expectsResponse, Type? responseType = null)
	{
		var entry = new HandlerRegistryEntry(messageType, handlerType, expectsResponse, responseType);
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
