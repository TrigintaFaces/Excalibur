// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Provides a fluent API for configuring how a message is handled within a process manager state.
/// </summary>
/// <typeparam name="TData">The type of saga state data that extends <see cref="SagaState"/>.</typeparam>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <remarks>
/// <para>
/// Message handlers are configured using a fluent builder pattern within state definitions.
/// Each handler can specify:
/// </para>
/// <list type="bullet">
/// <item><description>State transitions via <see cref="TransitionTo"/></description></item>
/// <item><description>Actions to execute via <see cref="Then"/></description></item>
/// <item><description>Conditional execution via <see cref="If"/></description></item>
/// <item><description>Saga completion via <see cref="Complete"/></description></item>
/// </list>
/// <para>
/// <b>Example Usage:</b>
/// </para>
/// <code>
/// During("PaymentPending", s => s
///     .When&lt;PaymentReceived&gt;(h => h
///         .If((data, msg) => msg.Amount > 0)
///         .TransitionTo("Shipping")
///         .Then(ctx => ctx.Data.PaymentId = ctx.Message.PaymentId)));
/// </code>
/// </remarks>
public interface IMessageHandler<TData, TMessage>
	where TData : SagaState
	where TMessage : class
{
	/// <summary>
	/// Specifies the state to transition to when this message is handled.
	/// </summary>
	/// <param name="stateName">The name of the target state. Case-insensitive.</param>
	/// <returns>The handler builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// State names are case-insensitive. The state must be defined via
	/// <see cref="ProcessManager{TData}.Initially"/>, <see cref="ProcessManager{TData}.During"/>,
	/// or <see cref="ProcessManager{TData}.Finally"/> methods.
	/// </para>
	/// <para>
	/// If the target state doesn't exist, an <see cref="InvalidStateTransitionException"/>
	/// will be thrown when the transition is attempted.
	/// </para>
	/// </remarks>
	IMessageHandler<TData, TMessage> TransitionTo(string stateName);

	/// <summary>
	/// Specifies an action to execute when this message is handled.
	/// </summary>
	/// <param name="action">The action to execute, receiving the saga context.</param>
	/// <returns>The handler builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Multiple <see cref="Then"/> calls can be chained to execute multiple actions in order.
	/// The saga context provides access to the saga data, the message being processed,
	/// and the process manager instance.
	/// </para>
	/// </remarks>
	IMessageHandler<TData, TMessage> Then(Action<SagaContext<TData, TMessage>> action);

	/// <summary>
	/// Specifies a condition that must be true for this handler to execute.
	/// </summary>
	/// <param name="condition">A predicate that receives the saga data and message.</param>
	/// <returns>The handler builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// If the condition returns <see langword="false"/>, this handler is skipped
	/// and the next matching handler (if any) is evaluated.
	/// </para>
	/// <para>
	/// Only one <see cref="If"/> condition can be set per handler. Multiple calls
	/// will replace the previous condition.
	/// </para>
	/// </remarks>
	IMessageHandler<TData, TMessage> If(Func<TData, TMessage, bool> condition);

	/// <summary>
	/// Marks the saga as completed when this message is handled.
	/// </summary>
	/// <returns>The handler builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Calling <see cref="Complete"/> is equivalent to calling
	/// <see cref="Messaging.Delivery.Orchestration.Saga{TSagaState}.MarkCompleted"/>
	/// at the end of message processing.
	/// </para>
	/// <para>
	/// Completed sagas will not process further messages and may be eligible
	/// for cleanup operations.
	/// </para>
	/// </remarks>
	IMessageHandler<TData, TMessage> Complete();
}
