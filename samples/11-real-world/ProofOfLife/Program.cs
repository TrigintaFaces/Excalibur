// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Proof-of-Life Consumer App  (bd-s94fvq)
// ============================================================================
//
// This sample validates the complete Excalibur consumer developer experience:
//
//   1. Message dispatching (IDispatcher, IActionHandler)
//   2. Domain modeling (AggregateRoot<TKey>, RaiseEvent, ApplyEventInternal)
//   3. Event sourcing (IEventSourcedRepository, IEventStore, snapshots)
//   4. Projections (IProjectionStore<T>, read models)
//   5. Query API (dispatch queries through handlers)
//
// Key constraint: uses ONLY public APIs — no InternalsVisibleTo, no internal
// types. If it compiles and runs, the public API surface is validated.
//
// Uses InMemory event store so anyone can 'dotnet run' without infrastructure.
//
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Abstractions;

using ProofOfLife.Domain;
using ProofOfLife.Messages;
using ProofOfLife.Projections;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. Dispatch — message dispatching with handler discovery
// ============================================================================

builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// ============================================================================
// 2. Event serializer — required for event sourcing (serializes domain events)
// ============================================================================

builder.Services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// ============================================================================
// 3. Excalibur event sourcing — InMemory provider for zero-infrastructure demo
// ============================================================================

builder.Services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
	// Register the TodoAggregate repository with factory function.
	// This makes IEventSourcedRepository<TodoAggregate, Guid> available via DI.
	es.AddRepository<TodoAggregate, Guid>(id => new TodoAggregate(id));
}));

// InMemory event store (for development/testing — swap to SqlServer for production)
builder.Services.AddInMemoryEventStore();

// ============================================================================
// 4. Projection store — in-memory read model store
// ============================================================================

// Register a singleton in-memory projection store for TodoProjection.
// In production, swap this for AddSqlServerProjectionStore<TodoProjection>(),
// AddPostgresProjectionStore<TodoProjection>(), or any other provider.
builder.Services.AddSingleton<IProjectionStore<TodoProjection>, InMemoryProjectionStore<TodoProjection>>();

var app = builder.Build();

// Initialize the local message bus (required for dispatch to work)
_ = app.Services.GetRequiredKeyedService<IMessageBus>("Local");

// ============================================================================
// 5. Minimal API endpoints — REST API backed by Excalibur
// ============================================================================

var todos = app.MapGroup("/api/todos");

// POST /api/todos — Create a new todo
todos.MapPost("/", async (CreateTodoRequest request, IDispatcher dispatcher, IServiceProvider sp, CancellationToken ct) =>
{
	var context = DispatchContextInitializer.CreateDefaultContext(sp);
	var result = await dispatcher.DispatchAsync<CreateTodoCommand, Guid>(
		new CreateTodoCommand(request.Title), context, ct).ConfigureAwait(false);

	if (!result.Succeeded)
	{
		return Results.BadRequest(new { error = result.ErrorMessage });
	}

	var todoId = result.ReturnValue;

	// Update projection after save (simple synchronous projection pattern)
	var store = sp.GetRequiredService<IProjectionStore<TodoProjection>>();
	await store.UpsertAsync(todoId.ToString(), new TodoProjection
	{
		Id = todoId,
		Title = request.Title,
		IsCompleted = false,
		Version = 1
	}, ct).ConfigureAwait(false);

	return Results.Created($"/api/todos/{todoId}", new { id = todoId });
});

// GET /api/todos — List all todos
todos.MapGet("/", async (IDispatcher dispatcher, IServiceProvider sp, CancellationToken ct) =>
{
	var context = DispatchContextInitializer.CreateDefaultContext(sp);
	var result = await dispatcher.DispatchAsync<ListTodosQuery, IReadOnlyList<TodoDto>>(
		new ListTodosQuery(), context, ct).ConfigureAwait(false);

	return result.Succeeded ? Results.Ok(result.ReturnValue) : Results.BadRequest(new { error = result.ErrorMessage });
});

// GET /api/todos/{id} — Get a specific todo
todos.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, IServiceProvider sp, CancellationToken ct) =>
{
	var context = DispatchContextInitializer.CreateDefaultContext(sp);
	var result = await dispatcher.DispatchAsync<GetTodoQuery, TodoDto?>(
		new GetTodoQuery(id), context, ct).ConfigureAwait(false);

	if (!result.Succeeded)
	{
		return Results.BadRequest(new { error = result.ErrorMessage });
	}

	return result.ReturnValue is not null ? Results.Ok(result.ReturnValue) : Results.NotFound();
});

// POST /api/todos/{id}/complete — Mark a todo as completed
todos.MapPost("/{id:guid}/complete", async (Guid id, IDispatcher dispatcher, IServiceProvider sp, CancellationToken ct) =>
{
	var context = DispatchContextInitializer.CreateDefaultContext(sp);
	var result = await dispatcher.DispatchAsync(
		new CompleteTodoCommand(id), context, ct).ConfigureAwait(false);

	if (!result.Succeeded)
	{
		return Results.BadRequest(new { error = result.ErrorMessage });
	}

	// Update projection
	var store = sp.GetRequiredService<IProjectionStore<TodoProjection>>();
	var projection = await store.GetByIdAsync(id.ToString(), ct).ConfigureAwait(false);
	if (projection is not null)
	{
		projection.IsCompleted = true;
		projection.CompletedAt = DateTimeOffset.UtcNow;
		projection.Version++;
		await store.UpsertAsync(id.ToString(), projection, ct).ConfigureAwait(false);
	}

	return Results.NoContent();
});

// PUT /api/todos/{id}/title — Update a todo's title
todos.MapPut("/{id:guid}/title", async (Guid id, UpdateTitleRequest request, IDispatcher dispatcher, IServiceProvider sp, CancellationToken ct) =>
{
	var context = DispatchContextInitializer.CreateDefaultContext(sp);
	var result = await dispatcher.DispatchAsync(
		new UpdateTodoTitleCommand(id, request.NewTitle), context, ct).ConfigureAwait(false);

	if (!result.Succeeded)
	{
		return Results.BadRequest(new { error = result.ErrorMessage });
	}

	// Update projection
	var store = sp.GetRequiredService<IProjectionStore<TodoProjection>>();
	var projection = await store.GetByIdAsync(id.ToString(), ct).ConfigureAwait(false);
	if (projection is not null)
	{
		projection.Title = request.NewTitle;
		projection.Version++;
		await store.UpsertAsync(id.ToString(), projection, ct).ConfigureAwait(false);
	}

	return Results.NoContent();
});

// GET /api/todos/count — Get total count from projection store
todos.MapGet("/count", async (IProjectionStore<TodoProjection> store, CancellationToken ct) =>
{
	var count = await store.CountAsync(filters: null, ct).ConfigureAwait(false);
	return Results.Ok(new { count });
});

app.Run();

// ============================================================================
// Request DTOs for Minimal API binding
// ============================================================================

/// <summary>Request body for creating a todo.</summary>
internal sealed record CreateTodoRequest(string Title);

/// <summary>Request body for updating a todo title.</summary>
internal sealed record UpdateTitleRequest(string NewTitle);
