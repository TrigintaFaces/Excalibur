// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents an action message (command or query) that can be dispatched without expecting a response.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IDispatchMessage" /> for action-style messages such as commands and queries that do not return a
/// specific response type. For actions that return a response, use <see cref="IDispatchAction{TResponse}" />.
/// </remarks>
public interface IDispatchAction : IDispatchMessage;

/// <summary>
/// Represents an action message (command or query) that returns a response.
/// </summary>
/// <typeparam name="TResponse"> The type of response expected from this action. </typeparam>
/// <remarks>
/// This interface extends <see cref="IDispatchAction" /> for action-style messages such as commands and queries that return a specific
/// response type. The response type is used to ensure type safety in the dispatch pipeline.
/// </remarks>
public interface IDispatchAction<TResponse> : IDispatchAction;
