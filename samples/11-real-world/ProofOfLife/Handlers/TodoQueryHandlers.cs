// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

using ProofOfLife.Domain;
using ProofOfLife.Messages;
using ProofOfLife.Projections;

namespace ProofOfLife.Handlers;

/// <summary>
/// Handles GetTodoQuery: reads from the projection store (read model).
/// </summary>
/// <remarks>
/// This demonstrates CQRS separation: commands go through the aggregate,
/// queries read from projection stores (denormalized read models).
/// </remarks>
public sealed class GetTodoHandler : IActionHandler<GetTodoQuery, TodoDto?>
{
	private readonly IProjectionStore<TodoProjection> _projectionStore;

	public GetTodoHandler(IProjectionStore<TodoProjection> projectionStore)
	{
		_projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
	}

	public async Task<TodoDto?> HandleAsync(GetTodoQuery action, CancellationToken cancellationToken)
	{
		var projection = await _projectionStore.GetByIdAsync(
			action.TodoId.ToString(), cancellationToken).ConfigureAwait(false);

		return projection?.ToDto();
	}
}

/// <summary>
/// Handles ListTodosQuery: queries all projections from the store.
/// </summary>
public sealed class ListTodosHandler : IActionHandler<ListTodosQuery, IReadOnlyList<TodoDto>>
{
	private readonly IProjectionStore<TodoProjection> _projectionStore;

	public ListTodosHandler(IProjectionStore<TodoProjection> projectionStore)
	{
		_projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
	}

	public async Task<IReadOnlyList<TodoDto>> HandleAsync(ListTodosQuery action, CancellationToken cancellationToken)
	{
		var projections = await _projectionStore.QueryAsync(
			filters: null, options: null, cancellationToken).ConfigureAwait(false);

		return projections.Select(p => p.ToDto()).ToList();
	}
}
