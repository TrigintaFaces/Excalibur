// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Defines a state in a process manager state machine, including message handlers
/// and entry/exit actions.
/// </summary>
/// <typeparam name="TData">The type of saga state data that extends <see cref="SagaState"/>.</typeparam>
/// <remarks>
/// <para>
/// States are defined using a fluent API within the process manager constructor.
/// Each state can handle multiple message types and define entry/exit hooks.
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// </para>
/// <code>
/// During("PaymentPending", s => s
///     .OnEnter(data => Console.WriteLine("Entering PaymentPending"))
///     .When&lt;PaymentReceived&gt;(h => h.TransitionTo("Shipping"))
///     .When&lt;PaymentFailed&gt;(h => h.TransitionTo("Cancelled"))
///     .OnExit(data => Console.WriteLine("Exiting PaymentPending")));
/// </code>
/// </remarks>
public interface IStateDefinition<TData>
	where TData : SagaState
{
	/// <summary>
	/// Gets the name of this state.
	/// </summary>
	/// <value>The state name, which is case-insensitive for lookup purposes.</value>
	string Name { get; }

	/// <summary>
	/// Configures a handler for a specific message type within this state.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to handle.</typeparam>
	/// <param name="configure">An action to configure the message handler.</param>
	/// <returns>The state definition for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Multiple <see cref="When{TMessage}"/> calls can be chained to handle different
	/// message types. Only one handler per message type is supported per state.
	/// </para>
	/// <para>
	/// Message handlers are evaluated in registration order. If a handler's
	/// <see cref="IMessageHandler{TData, TMessage}.If"/> condition returns false,
	/// the message is not handled by that state.
	/// </para>
	/// </remarks>
	IStateDefinition<TData> When<TMessage>(Action<IMessageHandler<TData, TMessage>> configure)
		where TMessage : class;

	/// <summary>
	/// Specifies an action to execute when entering this state.
	/// </summary>
	/// <param name="action">The action to execute, receiving the saga data.</param>
	/// <returns>The state definition for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The OnEnter action is invoked after a transition to this state completes,
	/// but before any message handlers execute. Only one OnEnter action can be
	/// registered per state. Subsequent calls replace the previous action.
	/// </para>
	/// </remarks>
	IStateDefinition<TData> OnEnter(Action<TData> action);

	/// <summary>
	/// Specifies an action to execute when exiting this state.
	/// </summary>
	/// <param name="action">The action to execute, receiving the saga data.</param>
	/// <returns>The state definition for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The OnExit action is invoked before a transition from this state begins,
	/// before the OnEnter action of the target state. Only one OnExit action
	/// can be registered per state. Subsequent calls replace the previous action.
	/// </para>
	/// </remarks>
	IStateDefinition<TData> OnExit(Action<TData> action);
}
