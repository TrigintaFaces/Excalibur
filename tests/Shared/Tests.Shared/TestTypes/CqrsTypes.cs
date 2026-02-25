// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Command dispatcher interface for CQRS testing.
/// </summary>
public interface ICommandDispatcher
{
	/// <summary>Dispatches a command.</summary>
	Task<TResult> DispatchAsync<TResult>(object command, CancellationToken cancellationToken = default);

	/// <summary>Dispatches a command without result.</summary>
	Task DispatchAsync(object command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query dispatcher interface for CQRS testing.
/// </summary>
public interface IQueryDispatcher
{
	/// <summary>Dispatches a query and returns the result.</summary>
	Task<TResult> DispatchAsync<TResult>(object query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command interface for CQRS.
/// </summary>
public interface ICommand
{
	/// <summary>Gets the command ID.</summary>
	Guid CommandId { get; }
}

/// <summary>
/// Command interface with result for CQRS.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommand<TResult> : ICommand
{
}

/// <summary>
/// Query interface for CQRS.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQuery<TResult>
{
	/// <summary>Gets the query ID.</summary>
	Guid QueryId { get; }
}

/// <summary>
/// Command handler interface for CQRS (no result).
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
	/// <summary>Handles the command.</summary>
	Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command handler interface for CQRS (with result).
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
	/// <summary>Handles the command.</summary>
	Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query handler interface for CQRS.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
	/// <summary>Handles the query.</summary>
	Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Message handler interface for testing.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public interface IMessageHandler<TMessage>
{
	/// <summary>Handles the message.</summary>
	Task HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cron scheduler interface for scheduled tasks.
/// </summary>
public interface ICronScheduler
{
	/// <summary>Schedules a task with a cron expression.</summary>
	Task ScheduleAsync(string cronExpression, Func<CancellationToken, Task> task, CancellationToken cancellationToken = default);

	/// <summary>Cancels a scheduled task.</summary>
	Task CancelAsync(string taskId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Prewarmable interface for lazy initialization.
/// </summary>
public interface IPrewarmable
{
	/// <summary>Prewarms the component.</summary>
	Task PrewarmAsync(CancellationToken cancellationToken = default);

	/// <summary>Gets whether the component is warmed.</summary>
	bool IsWarmed { get; }
}

/// <summary>
/// Serializer type enumeration.
/// </summary>
public enum SerializerType
{
	/// <summary>System.Text.Json serializer.</summary>
	SystemTextJson,

	/// <summary>Newtonsoft.Json serializer.</summary>
	NewtonsoftJson,

	/// <summary>MessagePack serializer.</summary>
	MessagePack,

	/// <summary>Protobuf serializer.</summary>
	Protobuf
}
