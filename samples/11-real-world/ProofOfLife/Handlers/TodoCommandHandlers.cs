// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

using ProofOfLife.Domain;
using ProofOfLife.Messages;

namespace ProofOfLife.Handlers;

/// <summary>
/// Handles CreateTodoCommand: creates a new aggregate and persists it.
/// </summary>
public sealed class CreateTodoHandler : IActionHandler<CreateTodoCommand, Guid>
{
	private readonly IEventSourcedRepository<TodoAggregate, Guid> _repository;

	public CreateTodoHandler(IEventSourcedRepository<TodoAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task<Guid> HandleAsync(CreateTodoCommand action, CancellationToken cancellationToken)
	{
		var todoId = Guid.NewGuid();
		var todo = TodoAggregate.Create(todoId, action.Title);

		await _repository.SaveAsync(todo, cancellationToken).ConfigureAwait(false);

		return todoId;
	}
}

/// <summary>
/// Handles CompleteTodoCommand: loads aggregate, applies business logic, saves.
/// </summary>
public sealed class CompleteTodoHandler : IActionHandler<CompleteTodoCommand>
{
	private readonly IEventSourcedRepository<TodoAggregate, Guid> _repository;

	public CompleteTodoHandler(IEventSourcedRepository<TodoAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(CompleteTodoCommand action, CancellationToken cancellationToken)
	{
		var todo = await _repository.GetByIdAsync(action.TodoId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Todo {action.TodoId} not found.");

		todo.Complete();

		await _repository.SaveAsync(todo, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Handles UpdateTodoTitleCommand: loads aggregate, updates title, saves.
/// </summary>
public sealed class UpdateTodoTitleHandler : IActionHandler<UpdateTodoTitleCommand>
{
	private readonly IEventSourcedRepository<TodoAggregate, Guid> _repository;

	public UpdateTodoTitleHandler(IEventSourcedRepository<TodoAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(UpdateTodoTitleCommand action, CancellationToken cancellationToken)
	{
		var todo = await _repository.GetByIdAsync(action.TodoId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Todo {action.TodoId} not found.");

		todo.UpdateTitle(action.NewTitle);

		await _repository.SaveAsync(todo, cancellationToken).ConfigureAwait(false);
	}
}
