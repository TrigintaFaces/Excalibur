// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests.Commands;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

using FullStackAddExcalibur.Domain;

using Microsoft.Extensions.Logging;

namespace FullStackAddExcalibur.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating an <see cref="OrderAggregate"/>,
/// persisting it through <see cref="IEventSourcedRepository{TAggregate, TKey}"/>, and
/// dispatching the uncommitted domain events through <see cref="IDispatcher"/> so
/// registered <see cref="IEventHandler{TEvent}"/> projection handlers run in-process.
/// </summary>
/// <remarks>
/// This is the canonical L3 pipeline shape used across the Excalibur samples:
/// <list type="number">
/// <item><description>Dispatch a strongly-typed <see cref="ICommand{TResponse}"/> command (also flows correlation, tenant, audit, and activity metadata).</description></item>
/// <item><description>Save the aggregate (the repository also enqueues events in the transactional outbox for transport).</description></item>
/// <item><description>Dispatch the aggregate's uncommitted events so local <see cref="IEventHandler{TEvent}"/> projection handlers observe them.</description></item>
/// </list>
/// <para>
/// The outbox + transport path is still the source of truth for cross-process delivery;
/// dispatching events locally here gives the read-side projection low-latency updates
/// without waiting for the outbox poll cycle. Real deployments can choose either path;
/// many use both.
/// </para>
/// </remarks>
public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _orderRepository;
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<CreateOrderHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CreateOrderHandler"/> class.
	/// </summary>
	/// <param name="orderRepository">The event-sourced order repository.</param>
	/// <param name="dispatcher">The dispatcher used to publish domain events to local projection handlers.</param>
	/// <param name="logger">The logger.</param>
	public CreateOrderHandler(
		IEventSourcedRepository<OrderAggregate, Guid> orderRepository,
		IDispatcher dispatcher,
		ILogger<CreateOrderHandler> logger)
	{
		_orderRepository = orderRepository;
		_dispatcher = dispatcher;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var orderId = Guid.NewGuid();
		var order = OrderAggregate.Create(
			orderId,
			action.ExternalOrderId,
			action.CustomerId,
			action.CustomerExternalId,
			action.OrderDate);

		foreach (var line in action.LineItems)
		{
			order.AddLineItem(Guid.NewGuid(), line.ProductName, line.Quantity, line.UnitPrice);
		}

		// Capture uncommitted events BEFORE SaveAsync: SaveAsync clears the
		// aggregate's uncommitted event list after persisting to the event store
		// and the outbox.
		var uncommitted = order.GetUncommittedEvents().ToArray();

		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		// Dispatch each domain event to in-process IEventHandler<T> subscribers so
		// read-side projections update immediately. The outbox has already durably
		// captured the same events for cross-process / cross-service delivery.
		foreach (var @event in uncommitted)
		{
			await DispatchDomainEventAsync(@event, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogInformation(
			"Created order {OrderId} for customer {CustomerExternalId} with {LineCount} line items, total {Total:C}",
			orderId,
			action.CustomerExternalId,
			order.LineItems.Count,
			order.TotalAmount);

		return orderId;
	}

	private Task DispatchDomainEventAsync(IDomainEvent @event, CancellationToken cancellationToken)
	{
		// A real system would route through an outbox-driven event dispatcher. For the
		// sample we surface the canonical in-process path so projection handlers are
		// exercised without requiring the background outbox processor.
		return @event switch
		{
			OrderCreated created => _dispatcher.DispatchAsync(created, cancellationToken),
			OrderLineItemAdded added => _dispatcher.DispatchAsync(added, cancellationToken),
			OrderShipped shipped => _dispatcher.DispatchAsync(shipped, cancellationToken),
			OrderCancelled cancelled => _dispatcher.DispatchAsync(cancelled, cancellationToken),
			_ => Task.CompletedTask,
		};
	}
}
