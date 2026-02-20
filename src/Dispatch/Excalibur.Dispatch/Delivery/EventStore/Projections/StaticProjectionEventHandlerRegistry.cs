// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides a static, in-memory registry for managing projection event handlers in the event sourcing system. This registry maintains a
/// mapping between event types and their corresponding projection handlers, enabling efficient handler resolution during event processing.
/// </summary>
/// <remarks>
/// This implementation uses a dictionary-based lookup for O(1) handler resolution performance. Handlers are registered by event type string
/// and resolved using the same key. Thread safety considerations should be addressed in multi-threaded environments.
/// </remarks>
public sealed class StaticProjectionEventHandlerRegistry : IProjectionEventHandlerRegistry
{
	private readonly Dictionary<string, IProjectionHandler> _handlers = [];

	/// <summary>
	/// Registers a projection handler for the specified event type, enabling the handler to process events of that type during projection updates.
	/// </summary>
	/// <typeparam name="TKey"> The type of key used to identify projections, typically matching the aggregate key type. </typeparam>
	/// <param name="eventType"> The string identifier for the event type this handler will process. </param>
	/// <param name="handler"> The projection handler instance that will process events of the specified type. </param>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="eventType" /> is null, empty, or whitespace. </exception>
	/// <remarks>
	/// If a handler is already registered for the specified event type, it will be replaced with the new handler. Event type strings should
	/// follow consistent naming conventions to ensure proper handler resolution.
	/// </remarks>
	public void Register<TKey>(string eventType, IProjectionHandler handler)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

		_handlers[eventType] = handler;
	}

	/// <summary>
	/// Resolves and returns the projection handler registered for the specified event type. This method provides fast O(1) lookup
	/// performance for handler resolution during event processing.
	/// </summary>
	/// <typeparam name="TKey"> The type of key used to identify projections, typically matching the aggregate key type. </typeparam>
	/// <param name="eventType"> The string identifier for the event type to resolve a handler for. </param>
	/// <returns> The <see cref="IProjectionHandler" /> registered for the event type, or <c> null </c> if no handler is registered. </returns>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="eventType" /> is null, empty, or whitespace. </exception>
	/// <remarks>
	/// Returns null if no handler has been registered for the specified event type. Callers should handle the null case appropriately,
	/// either by providing fallback behavior or by ensuring all required handlers are registered during application startup.
	/// </remarks>
	public IProjectionHandler? ResolveHandler<TKey>(string eventType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

		return _handlers.GetValueOrDefault(eventType);
	}
}
