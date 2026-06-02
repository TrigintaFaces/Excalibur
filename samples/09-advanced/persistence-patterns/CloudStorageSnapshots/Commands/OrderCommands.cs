// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;
using Excalibur.EventSourcing;

using CloudStorageSnapshots.Domain;

using Microsoft.Extensions.Logging;

namespace CloudStorageSnapshots.Commands;

/// <summary>Creates a new order and persists the initial <c>OrderCreated</c> event.</summary>
/// <remarks>
/// Uses <see cref="CommandBase{TResponse}"/> so the command participates in correlation,
/// tenant propagation, audit (<see cref="IAmAuditable"/>), and activity metadata.
/// </remarks>
public sealed class CreateOrderCommand : CommandBase<Guid>, IAmAuditable
{
	/// <summary>Initializes a new instance with defaults.</summary>
	public CreateOrderCommand()
	{
	}

	/// <summary>Initializes a new instance with an explicit correlation id and tenant id.</summary>
	public CreateOrderCommand(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}
}

/// <summary>Appends <see cref="Count"/> notes to an existing order.</summary>
public sealed class AppendOrderNotesCommand : CommandBase<int>, IAmAuditable
{
	/// <summary>Initializes a new instance with defaults.</summary>
	public AppendOrderNotesCommand()
	{
	}

	/// <summary>Initializes a new instance with an explicit correlation id and tenant id.</summary>
	public AppendOrderNotesCommand(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>Gets the target order identifier.</summary>
	public required Guid OrderId { get; init; }

	/// <summary>Gets the number of notes to append.</summary>
	public required int Count { get; init; }
}

/// <summary>Handles <see cref="CreateOrderCommand"/>.</summary>
public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;
	private readonly ILogger<CreateOrderHandler> _logger;

	public CreateOrderHandler(
		IEventSourcedRepository<OrderAggregate, Guid> repository,
		ILogger<CreateOrderHandler> logger)
	{
		_repository = repository;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var id = Guid.NewGuid();
		var order = OrderAggregate.Create(id);
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("Created order {OrderId} in hot store", id);
		return id;
	}
}

/// <summary>Handles <see cref="AppendOrderNotesCommand"/>.</summary>
public sealed class AppendOrderNotesHandler : ICommandHandler<AppendOrderNotesCommand, int>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;
	private readonly ILogger<AppendOrderNotesHandler> _logger;

	public AppendOrderNotesHandler(
		IEventSourcedRepository<OrderAggregate, Guid> repository,
		ILogger<AppendOrderNotesHandler> logger)
	{
		_repository = repository;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<int> HandleAsync(AppendOrderNotesCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(action.Count);

		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false)
			?? throw new KeyNotFoundException($"Order {action.OrderId} not found");

		for (var i = 0; i < action.Count; i++)
		{
			order.AppendNote($"note-{order.Notes.Count + 1}");
		}

		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation(
			"Appended {Count} notes to order {OrderId}; hot store now holds {Total} notes (tiered decorator routes reads across hot+cold)",
			action.Count, action.OrderId, order.Notes.Count);

		return order.Notes.Count;
	}
}
