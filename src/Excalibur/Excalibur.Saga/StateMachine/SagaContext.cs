// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Provides context for processing a message within a process manager state machine.
/// </summary>
/// <typeparam name="TData">The type of saga state data that extends <see cref="SagaState"/>.</typeparam>
/// <typeparam name="TMessage">The type of message being processed.</typeparam>
/// <remarks>
/// <para>
/// The SagaContext is passed to message handler actions during state machine execution,
/// providing access to both the saga data and the message that triggered the transition.
/// </para>
/// </remarks>
/// <param name="Data">The current saga state data.</param>
/// <param name="Message">The message being processed.</param>
/// <param name="ProcessManager">The process manager instance processing this message.</param>
public sealed record SagaContext<TData, TMessage>(
	TData Data,
	TMessage Message,
	ProcessManager<TData> ProcessManager)
	where TData : SagaState
	where TMessage : class;
