// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;

namespace FullStackAddExcalibur.Commands;

/// <summary>
/// Command to create a new order with its line items.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Excalibur <see cref="CommandBase{TResponse}"/> CQRS base so the
/// command automatically participates in:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IAmCorrelatable"/> — a correlation id travels end-to-end through the pipeline.</description></item>
/// <item><description><see cref="IAmMultiTenant"/> — a tenant id flows into projections, outbox, and transport envelopes.</description></item>
/// <item><description><see cref="IAmAuditable"/> — the <c>Excalibur.A3.AuditMiddleware</c> writes an activity-audit record automatically.</description></item>
/// <item><description><see cref="IActivity"/> — activity name, display name, and description for observability (traces + logs).</description></item>
/// <item><description>Transaction scope — <see cref="System.Transactions.TransactionScopeOption.Required"/> by default with <c>ReadCommitted</c> isolation.</description></item>
/// </list>
/// <para>
/// The <see cref="CreateOrderHandler"/> creates an <see cref="Domain.OrderAggregate"/>,
/// persists it via <see cref="Excalibur.EventSourcing.Abstractions.IEventSourcedRepository{TAggregate,TKey}"/>
/// (which also enqueues the domain events in the outbox for at-least-once delivery),
/// and then dispatches the domain events so local
/// <see cref="Excalibur.Dispatch.Abstractions.Delivery.IEventHandler{TEvent}"/>
/// projection handlers can update read models.
/// </para>
/// </remarks>
public sealed class CreateOrderCommand : CommandBase<Guid>, IAmAuditable
{
	/// <summary>Initializes a new instance with defaults (correlation id is generated, tenant is default).</summary>
	public CreateOrderCommand()
	{
	}

	/// <summary>Initializes a new instance with an explicit correlation id and optional tenant id.</summary>
	/// <param name="correlationId">The correlation id used to trace the command end-to-end.</param>
	/// <param name="tenantId">The owning tenant id (falls back to the default tenant when omitted).</param>
	public CreateOrderCommand(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>Gets or sets the external (legacy) order identifier.</summary>
	public required string ExternalOrderId { get; init; }

	/// <summary>Gets or sets the customer identifier.</summary>
	public required Guid CustomerId { get; init; }

	/// <summary>Gets or sets the customer's external identifier.</summary>
	public required string CustomerExternalId { get; init; }

	/// <summary>Gets or sets the order date.</summary>
	public required DateTime OrderDate { get; init; }

	/// <summary>Gets or sets the line items on the order.</summary>
	public required IReadOnlyList<CreateOrderLineItem> LineItems { get; init; }
}

/// <summary>
/// Line item payload used when creating an order.
/// </summary>
public sealed record CreateOrderLineItem(
	string ProductName,
	int Quantity,
	decimal UnitPrice);
