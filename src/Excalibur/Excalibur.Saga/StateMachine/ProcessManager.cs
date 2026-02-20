// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Base class for process managers with an explicit state machine for handling messages.
/// </summary>
/// <typeparam name="TData"> The type of saga state data that extends <see cref="SagaState" />. </typeparam>
/// <remarks>
/// <para>
/// ProcessManager extends <see cref="Saga{TSagaState}" /> to provide a structured state machine approach to saga orchestration. States are
/// defined declaratively using fluent APIs:
/// </para>
/// <list type="bullet">
/// <item>
/// <description> <see cref="Initially" /> - Defines handlers for the initial state </description>
/// </item>
/// <item>
/// <description> <see cref="During" /> - Defines handlers for named states </description>
/// </item>
/// <item>
/// <description> <see cref="Finally" /> - Defines handlers for the final state before completion </description>
/// </item>
/// </list>
/// <para> <b> Example Usage: </b> </para>
/// <code>
///public class OrderProcessManager : ProcessManager&lt;OrderData&gt;
///{
///public OrderProcessManager(OrderData data, IDispatcher dispatcher, ILogger logger)
///: base(data, dispatcher, logger)
///{
///Initially(s =&gt; s
///.When&lt;OrderPlaced&gt;(h =&gt; h
///.TransitionTo("PaymentPending")
///.Then(ctx =&gt; ctx.Data.OrderId = ctx.Message.OrderId)));
///
///During("PaymentPending", s =&gt; s
///.When&lt;PaymentReceived&gt;(h =&gt; h
///.TransitionTo("Shipping")
///.Then(ctx =&gt; ctx.Data.PaymentId = ctx.Message.PaymentId))
///.When&lt;PaymentFailed&gt;(h =&gt; h
///.TransitionTo("Cancelled")));
///
///During("Shipping", s =&gt; s
///.When&lt;OrderShipped&gt;(h =&gt; h
///.Complete()));
///}
///}
/// </code>
/// <para>
/// The <see cref="CurrentState" /> is automatically persisted as part of the saga state via the <see cref="CurrentStateName" /> property
/// which should be added to your TData class.
/// </para>
/// </remarks>
/// <param name="initialState"> The initial state data for the saga. </param>
/// <param name="dispatcher"> The dispatcher for sending commands and publishing events. </param>
/// <param name="logger"> The logger for this process manager. </param>
public abstract class ProcessManager<TData>(
	TData initialState,
	IDispatcher dispatcher,
	ILogger logger) : Orchestration.SagaBase<TData>(initialState, dispatcher, logger)
	where TData : SagaState
{
	private static readonly ConcurrentDictionary<Type, HandlerReflectionInfo> HandlerReflectionCache = new();

	private readonly Dictionary<string, StateDefinition<TData>> _states = new(StringComparer.OrdinalIgnoreCase);
	private string _currentState = "Initial";

	/// <summary>
	/// Gets the name of the current state in the state machine.
	/// </summary>
	/// <value> The current state name. </value>
	public string CurrentState => _currentState;

	/// <summary>
	/// Gets or sets the name of the current state for persistence purposes.
	/// </summary>
	/// <value> The current state name to be persisted with saga data. </value>
	/// <remarks>
	/// <para> To persist the state machine position, add a property to your TData class: </para>
	/// <code>
	///public class OrderData : SagaState
	///{
	///public string CurrentStateName { get; set; } = "Initial";
	///}
	/// </code>
	/// <para> Then override this property to read/write from that field: </para>
	/// <code>
	///protected override string CurrentStateName
	///{
	///get =&gt; State.CurrentStateName;
	///set =&gt; State.CurrentStateName = value;
	///}
	/// </code>
	/// </remarks>
	protected virtual string CurrentStateName
	{
		get => _currentState;
		set => _currentState = value;
	}

	/// <inheritdoc />
	public override bool HandlesEvent(object eventMessage)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		// Check if current state has a handler for this message type
		if (!_states.TryGetValue(_currentState, out var state))
		{
			return false;
		}

		return state.HasHandler(eventMessage.GetType());
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Process manager fundamentally requires reflection for state machine handler invocation")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Process manager fundamentally requires runtime code generation for state machine handler invocation")]
	public override async Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		if (!_states.TryGetValue(_currentState, out var state))
		{
			throw new InvalidStateTransitionException(_currentState, _currentState, eventMessage.GetType());
		}

		// Get handler for this message type using reflection to call the generic method
		var messageType = eventMessage.GetType();
		var handler = state.GetHandlerForType(messageType)
					  ?? throw new InvalidStateTransitionException(
						  _currentState,
						  _currentState,
						  messageType);

		// Use reflection to invoke the handler since we don't know TMessage at compile time
		await InvokeHandlerAsync(handler, eventMessage, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Transitions to a new state, invoking OnExit on the current state and OnEnter on the new state.
	/// </summary>
	/// <param name="stateName"> The name of the target state. Case-insensitive. </param>
	/// <exception cref="InvalidStateTransitionException"> Thrown when the target state is not defined. </exception>
	/// <remarks>
	/// <para>
	/// This method is called internally by message handlers when <see cref="IMessageHandler{TData, TMessage}.TransitionTo" /> is
	/// configured. The transition sequence is:
	/// </para>
	/// <list type="number">
	/// <item>
	/// <description> Invoke OnExit on current state </description>
	/// </item>
	/// <item>
	/// <description> Update current state to target state </description>
	/// </item>
	/// <item>
	/// <description> Invoke OnEnter on new state </description>
	/// </item>
	/// </list>
	/// </remarks>
	protected internal void TransitionTo(string stateName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stateName);

		if (!_states.TryGetValue(stateName, out var targetState))
		{
			throw new InvalidStateTransitionException(_currentState, stateName, null);
		}

		// Invoke OnExit on current state if defined
		if (_states.TryGetValue(_currentState, out var currentStateDefinition))
		{
			currentStateDefinition.InvokeOnExit(State);
		}

		// Transition to new state
		_currentState = stateName;
		CurrentStateName = stateName;

		// Invoke OnEnter on new state
		targetState.InvokeOnEnter(State);
	}

	/// <summary>
	/// Defines handlers for the initial state ("Initial").
	/// </summary>
	/// <param name="configure"> An action to configure the initial state. </param>
	/// <remarks>
	/// <para>
	/// All process managers start in the "Initial" state. Use this method to define which messages start the workflow and what transitions
	/// they trigger.
	/// </para>
	/// </remarks>
	protected void Initially(Action<IStateDefinition<TData>> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var state = new StateDefinition<TData>("Initial");
		configure(state);
		_states["Initial"] = state;
	}

	/// <summary>
	/// Defines handlers for a named state.
	/// </summary>
	/// <param name="stateName"> The name of the state. Case-insensitive. </param>
	/// <param name="configure"> An action to configure the state. </param>
	/// <remarks>
	/// <para> States can handle multiple message types, each potentially triggering different transitions or actions. </para>
	/// </remarks>
	protected void During(string stateName, Action<IStateDefinition<TData>> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
		ArgumentNullException.ThrowIfNull(configure);

		var state = new StateDefinition<TData>(stateName);
		configure(state);
		_states[stateName] = state;
	}

	/// <summary>
	/// Defines handlers for the final state before completion ("Final").
	/// </summary>
	/// <param name="configure"> An action to configure the final state. </param>
	/// <remarks>
	/// <para>
	/// The "Final" state is a conventional last state before saga completion. Messages handled in this state typically trigger the
	/// <see cref="IMessageHandler{TData, TMessage}.Complete" /> action to mark the saga as complete.
	/// </para>
	/// </remarks>
	protected void Finally(Action<IStateDefinition<TData>> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var state = new StateDefinition<TData>("Final");
		configure(state);
		_states["Final"] = state;
	}

	[RequiresDynamicCode("Process manager uses MakeGenericType and Activator.CreateInstance to wire state machine handlers at runtime")]
	[RequiresUnreferencedCode("Process manager uses reflection (GetMethod, GetProperty, Invoke) to wire state machine handlers at runtime")]
	private async Task InvokeHandlerAsync(object handler, object message, CancellationToken cancellationToken)
	{
		// Get the generic types from the handler
		var handlerType = handler.GetType();
		var reflectionInfo = HandlerReflectionCache.GetOrAdd(handlerType, static type =>
		{
			var genericArgs = type.GetGenericArguments();

			if (genericArgs.Length != 2)
			{
				throw new InvalidOperationException(
					Resources.ProcessManager_HandlerTypeMustHaveExactlyTwoGenericArguments);
			}

			var dataType = genericArgs[0];
			var messageType = genericArgs[1];
			var contextType = typeof(SagaContext<,>).MakeGenericType(dataType, messageType);

			const BindingFlags nonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

			return new HandlerReflectionInfo(
				ContextType: contextType,
				ShouldHandle: type.GetMethod("ShouldHandle", nonPublicInstance)!,
				ExecuteActions: type.GetMethod("ExecuteActions", nonPublicInstance)!,
				TargetState: type.GetProperty("TargetState", nonPublicInstance)!,
				ShouldComplete: type.GetProperty("ShouldComplete", nonPublicInstance)!);
		});

		// Create the context using cached type info
		var context = Activator.CreateInstance(reflectionInfo.ContextType, State, message, this)!;

		// Check condition
		var shouldHandle = (bool)reflectionInfo.ShouldHandle.Invoke(handler, [State, message])!;

		if (!shouldHandle)
		{
			return; // Condition not met, skip this handler
		}

		// Execute actions
		_ = reflectionInfo.ExecuteActions.Invoke(handler, [context]);

		// Check transition
		var targetState = reflectionInfo.TargetState.GetValue(handler) as string;

		if (!string.IsNullOrEmpty(targetState))
		{
			TransitionTo(targetState);
		}

		// Check completion
		var shouldComplete = (bool)reflectionInfo.ShouldComplete.GetValue(handler)!;

		if (shouldComplete)
		{
			await MarkCompletedAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Cached reflection metadata for a handler type to avoid repeated GetMethod/GetProperty lookups.
	/// </summary>
	private sealed record HandlerReflectionInfo(
		Type ContextType,
		MethodInfo ShouldHandle,
		MethodInfo ExecuteActions,
		PropertyInfo TargetState,
		PropertyInfo ShouldComplete);
}
