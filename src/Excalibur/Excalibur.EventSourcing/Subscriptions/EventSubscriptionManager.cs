// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Default implementation of <see cref="IEventSubscriptionManager"/> that manages
/// named <see cref="EventStoreLiveSubscription"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This manager creates and tracks subscription instances in a thread-safe
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Subscriptions are created
/// lazily and can be retrieved by name.
/// </para>
/// </remarks>
public sealed class EventSubscriptionManager : IEventSubscriptionManager
{
	private readonly ConcurrentDictionary<string, IEventSubscription> _subscriptions = new();
	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly ILoggerFactory _loggerFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventSubscriptionManager"/> class.
	/// </summary>
	/// <param name="eventStore">The event store for creating subscriptions.</param>
	/// <param name="eventSerializer">The event serializer.</param>
	/// <param name="loggerFactory">The logger factory.</param>
	public EventSubscriptionManager(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		ILoggerFactory loggerFactory)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
	}

	/// <inheritdoc />
	public IEventSubscription CreateSubscription(string name, EventSubscriptionOptions options)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentNullException.ThrowIfNull(options);

		var subscription = new EventStoreLiveSubscription(
			_eventStore,
			_eventSerializer,
			options,
			_loggerFactory.CreateLogger<EventStoreLiveSubscription>());

		if (!_subscriptions.TryAdd(name, subscription))
		{
			throw new InvalidOperationException($"A subscription with the name '{name}' already exists.");
		}

		return subscription;
	}

	/// <inheritdoc />
	public IEventSubscription? GetSubscription(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		return _subscriptions.GetValueOrDefault(name);
	}
}
