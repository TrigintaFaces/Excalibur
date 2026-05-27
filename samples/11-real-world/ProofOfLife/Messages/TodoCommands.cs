// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ProofOfLife.Messages;

/// <summary>Command to create a new todo item. Returns the todo ID.</summary>
public sealed record CreateTodoCommand(string Title) : IDispatchAction<Guid>;

/// <summary>Command to mark a todo as completed.</summary>
public sealed record CompleteTodoCommand(Guid TodoId) : IDispatchAction;

/// <summary>Command to update a todo's title.</summary>
public sealed record UpdateTodoTitleCommand(Guid TodoId, string NewTitle) : IDispatchAction;

/// <summary>Query to get a todo by ID.</summary>
public sealed record GetTodoQuery(Guid TodoId) : IDispatchAction<TodoDto?>;

/// <summary>Query to list all todos.</summary>
public sealed record ListTodosQuery() : IDispatchAction<IReadOnlyList<TodoDto>>;

/// <summary>DTO representing a todo item for read operations.</summary>
public sealed record TodoDto(Guid Id, string Title, bool IsCompleted, DateTimeOffset? CompletedAt, long Version);
