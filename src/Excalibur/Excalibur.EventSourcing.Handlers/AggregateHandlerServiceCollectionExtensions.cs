// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Handlers;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering aggregate-handler Deciders.
/// </summary>
public static class AggregateHandlerServiceCollectionExtensions
{
	/// <summary>
	/// Registers a Decider that routes <typeparamref name="TMessage"/> to an event-sourced
	/// aggregate: resolve identity, load, decide, then save with optimistic concurrency.
	/// </summary>
	/// <typeparam name="TAggregate">The event-sourced aggregate type.</typeparam>
	/// <typeparam name="TKey">The aggregate identifier type.</typeparam>
	/// <typeparam name="TMessage">The dispatched message (command) type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="resolveId">Resolves the aggregate identity from the message.</param>
	/// <param name="decide">
	/// The domain decision applied to the loaded aggregate. It invokes domain methods that raise the
	/// aggregate's own events; it does not persist directly.
	/// </param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// The identity resolver and decision are supplied here rather than reflected off the message,
	/// so the registration is AOT-safe (no runtime code generation). The handler resolves its
	/// <see cref="IEventSourcedRepository{TAggregate, TKey}"/> from the container. A message that
	/// targets a missing aggregate surfaces as <see cref="Excalibur.Data.ResourceNotFoundException"/>;
	/// a stale write surfaces as <see cref="Excalibur.Data.ConcurrencyException"/>.
	/// </remarks>
	public static IServiceCollection AddAggregateHandler<TAggregate, TKey, TMessage>(
		this IServiceCollection services,
		Func<TMessage, TKey> resolveId,
		Func<TAggregate, TMessage, CancellationToken, Task> decide)
		where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
		where TKey : notnull
		where TMessage : IDispatchAction
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(resolveId);
		ArgumentNullException.ThrowIfNull(decide);

		services.TryAddScoped<IActionHandler<TMessage>>(sp =>
			new AggregateHandler<TAggregate, TKey, TMessage>(
				sp.GetRequiredService<IEventSourcedRepository<TAggregate, TKey>>(),
				resolveId,
				decide));

		return services;
	}
}
