// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Application.Requests.Commands;

/// <summary>
/// Represents a command in the system, combining CQRS and Dispatch patterns.
/// </summary>
/// <remarks>
/// This interface unifies the CQRS command pattern with Dispatch's action pattern, providing a single abstraction for command operations.
/// </remarks>
public interface ICommand : IDispatchAction, IActivity
{
}

/// <summary>
/// Represents a command that returns a result.
/// </summary>
/// <typeparam name="TResult"> The type of result returned by the command. </typeparam>
public interface ICommand<TResult> : ICommand, IDispatchAction<TResult>
{
}

/// <summary>
/// Handler for CQRS commands.
/// </summary>
/// <typeparam name="TCommand"> The type of command to handle. </typeparam>
public interface ICommandHandler<in TCommand> : IActionHandler<TCommand>
	where TCommand : ICommand
{
}

/// <summary>
/// Handler for CQRS commands that return a result.
/// </summary>
/// <typeparam name="TCommand"> The type of command to handle. </typeparam>
/// <typeparam name="TResult"> The type of result to return. </typeparam>
public interface ICommandHandler<in TCommand, TResult> : IActionHandler<TCommand, TResult>
	where TCommand : ICommand<TResult>
{
}
