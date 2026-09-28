// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Base class for process managers with an explicit state machine for handling messages.
/// </summary>
/// <typeparam name="TData"> The type of saga state data that extends <see cref="SagaState" />. </typeparam>
/// <remarks>
/// <para>
/// ProcessManager extends <see cref="Orchestration.SagaBase{TData}" /> to provide a structured state machine approach to saga orchestration. States are
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
/// The <see cref="CurrentState" /> IS persisted, because it lives in the saga state: <typeparamref name="TData" />
/// derives from <see cref="ProcessManagerState" />, which carries the position. Earlier releases claimed this
/// was automatic while holding the position in a field that was re-initialised on every delivery, so a
/// multi-state process manager resumed at "Initial" on its second message. There is now one representation
/// of the position and the type system requires it
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
	where TData : ProcessManagerState
{
	private const int MaxCacheEntries = 1024;

	private static readonly ConcurrentDictionary<Type, HandlerReflectionInfo> HandlerReflectionCache = new();

	private readonly Dictionary<string, StateDefinition<TData>> _states = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the name of the current state in the state machine.
	/// </summary>
	/// <value> The current state name. </value>
	public string CurrentState => State.CurrentStateName;


	/// <inheritdoc />
	public override bool HandlesEvent(object eventMessage)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		// Check if current state has a handler for this message type
		if (!_states.TryGetValue(State.CurrentStateName, out var state))
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
	public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		if (!_states.TryGetValue(State.CurrentStateName, out var state))
		{
			throw new InvalidStateTransitionException(State.CurrentStateName, State.CurrentStateName, eventMessage.GetType());
		}

		// Get handler for this message type using reflection to call the generic method
		var messageType = eventMessage.GetType();
		var handler = state.GetHandlerForType(messageType)
					  ?? throw new InvalidStateTransitionException(
						  State.CurrentStateName,
						  State.CurrentStateName,
						  messageType);

		// Use reflection to invoke the handler since we don't know TMessage at compile time
		return await InvokeHandlerAsync(handler, eventMessage, cancellationToken).ConfigureAwait(false);
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
			throw new InvalidStateTransitionException(State.CurrentStateName, stateName, null);
		}

		// Invoke OnExit on current state if defined
		if (_states.TryGetValue(State.CurrentStateName, out var currentStateDefinition))
		{
			currentStateDefinition.InvokeOnExit(State);
		}

		// Transition to new state
		State.CurrentStateName = stateName;

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
	private async Task<SagaEventOutcome> InvokeHandlerAsync(object handler, object message, CancellationToken cancellationToken)
	{
		// Get the generic types from the handler
		var handlerType = handler.GetType();

		if (HandlerReflectionCache.TryGetValue(handlerType, out var reflectionInfo))
		{
			// Cache hit -- use cached reflection info
		}
		else if (HandlerReflectionCache.Count >= MaxCacheEntries)
		{
			// Cache full -- compute without caching
			reflectionInfo = ComputeHandlerReflectionInfo(handlerType);
		}
		else
		{
			// Cache not full -- compute and cache
			reflectionInfo = HandlerReflectionCache.GetOrAdd(handlerType, static type => ComputeHandlerReflectionInfo(type));
		}

		// Create the context using AOT-safe factory when available, falling back to Activator
		var context = CreateContextInstance(handlerType, reflectionInfo.ContextType, State, message);

		// Check condition
		var shouldHandle = (bool)reflectionInfo.ShouldHandle.Invoke(handler, [State, message])!;

		if (!shouldHandle)
		{
			// DECLINED, and reported as such. This used to be a bare `return`, which the coordinator
			// could not distinguish from the handler having run -- so it recorded the event as processed
			// and persisted that record, permanently retiring a message no handler had touched. A message
			// arriving before its guard is satisfiable is DEFERRED, not discarded.
			return SagaEventOutcome.Declined;
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

		// The actions ran, so the event WAS acted on. Only this path reports Handled, and only a
		// Handled event is recorded as processed.
		return SagaEventOutcome.Handled;
	}

	/// <summary>
	/// Creates a <see cref="SagaContext{TData, TMessage}"/> instance using the AOT-safe factory
	/// registry when available, falling back to <see cref="Activator.CreateInstance(Type, object[])"/>
	/// in JIT environments.
	/// </summary>
	[RequiresDynamicCode("Falls back to Activator.CreateInstance when no AOT-safe context factory is registered")]
	[RequiresUnreferencedCode("Reflects over the handler type's generic arguments to locate the context constructor")]
	private object CreateContextInstance(
		Type handlerType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type contextType,
		object state,
		object message)
	{
		var genericArgs = handlerType.GetGenericArguments();
		if (genericArgs.Length == 2)
		{
			var aotContext = SagaContextFactoryRegistry.CreateContext(genericArgs[0], genericArgs[1], state, message, this);
			if (aotContext is not null)
			{
				return aotContext;
			}
		}

		// JIT fallback: use Activator.CreateInstance (not available in AOT)
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			throw new PlatformNotSupportedException(
				$"No AOT-safe context factory registered for handler type '{handlerType.Name}'. " +
				"Register all saga message handlers via AddSaga<TSaga, TSagaState>() during DI composition.");
		}

		return Activator.CreateInstance(contextType, state, message, this)!;
	}

	[RequiresDynamicCode("Process manager uses MakeGenericType to create SagaContext<,> at runtime")]
	[RequiresUnreferencedCode("Process manager uses reflection (GetMethod, GetProperty) to discover handler members at runtime")]
	private static HandlerReflectionInfo ComputeHandlerReflectionInfo(Type type)
	{
		var genericArgs = type.GetGenericArguments();

		if (genericArgs.Length != 2)
		{
			throw new InvalidOperationException(
				Resources.ProcessManager_HandlerTypeMustHaveExactlyTwoGenericArguments);
		}

		var dataType = genericArgs[0];
		var messageType = genericArgs[1];

		Type contextType;
		if (RuntimeFeature.IsDynamicCodeSupported)
		{
			// JIT path: use MakeGenericType
			contextType = typeof(SagaContext<,>).MakeGenericType(dataType, messageType);
		}
		else
		{
			// AOT path: store the open generic type -- actual construction handled by SagaContextFactoryRegistry
			contextType = typeof(SagaContext<,>);
		}

		const BindingFlags nonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

		return new HandlerReflectionInfo(
			ContextType: contextType,
			ShouldHandle: type.GetMethod("ShouldHandle", nonPublicInstance)!,
			ExecuteActions: type.GetMethod("ExecuteActions", nonPublicInstance)!,
			TargetState: type.GetProperty("TargetState", nonPublicInstance)!,
			ShouldComplete: type.GetProperty("ShouldComplete", nonPublicInstance)!);
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
