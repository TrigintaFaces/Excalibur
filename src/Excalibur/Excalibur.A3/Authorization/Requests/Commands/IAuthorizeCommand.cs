// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Commands;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Represents a command that requires authorization and produces a response.
/// </summary>
/// <remarks>
/// Combines the functionalities of <see cref="ICommand" /> and <see cref="IRequireAuthorization" />. This interface is intended for
/// commands that need to verify user permissions or access control before execution.
/// </remarks>
public interface IAuthorizeCommand : ICommand, IRequireAuthorization;

/// <summary>
/// Represents a command that requires authorization and produces a response.
/// </summary>
/// <typeparam name="TResponse"> The type of response produced by the command. </typeparam>
/// <remarks>
/// Combines the functionalities of <see cref="ICommand{TResponse}" /> and <see cref="IRequireAuthorization" />. This interface is intended
/// for commands that need to verify user permissions or access control before execution.
/// </remarks>
public interface IAuthorizeCommand<TResponse> : ICommand<TResponse>, IRequireAuthorization;
