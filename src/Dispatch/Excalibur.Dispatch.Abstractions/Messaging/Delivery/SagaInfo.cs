// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Contains metadata and configuration information for a saga type, including handled events and saga lifecycle. This class manages the
/// registration of event types that can start or continue a saga instance, providing runtime information for saga coordination and event routing.
/// </summary>
/// <param name="sagaType"> The saga implementation type that handles business logic. </param>
/// <param name="stateType"> The state type used for saga persistence and workflow tracking. </param>
public sealed class SagaInfo(Type sagaType, Type stateType)
{
	private readonly HashSet<Type> _startEvents = [];
	private readonly HashSet<Type> _handledEvents = [];

	/// <summary>
	/// Gets the saga implementation type that contains the business logic for processing events. This type must inherit from
	/// Saga&lt;TState&gt; and implement event handling methods.
	/// </summary>
	/// <value>The current <see cref="SagaType"/> value.</value>
	public Type SagaType { get; } = sagaType;

	/// <summary>
	/// Gets the state type used for saga persistence and workflow tracking. This type must inherit from SagaState and contain all data
	/// needed for saga coordination.
	/// </summary>
	/// <value>The current <see cref="StateType"/> value.</value>
	public Type StateType { get; } = stateType;

	/// <summary>
	/// Configures an event type as a saga initiation event that can start new saga instances. Start events create new saga state and begin
	/// the workflow orchestration process.
	/// </summary>
	/// <typeparam name="TEvent"> Event type that can initiate new saga instances. </typeparam>
	/// <returns> This SagaInfo instance for fluent configuration chaining. </returns>
	public SagaInfo StartsWith<TEvent>()
	{
		_ = _startEvents.Add(typeof(TEvent));
		_ = _handledEvents.Add(typeof(TEvent));
		return this;
	}

	/// <summary>
	/// Configures an event type as a saga continuation event that can be processed by existing saga instances. Handle events continue
	/// existing workflows without creating new saga state.
	/// </summary>
	/// <typeparam name="TEvent"> Event type that can be processed by existing saga instances. </typeparam>
	/// <returns> This SagaInfo instance for fluent configuration chaining. </returns>
	public SagaInfo Handles<TEvent>()
	{
		_ = _handledEvents.Add(typeof(TEvent));
		return this;
	}

	/// <summary>
	/// Determines if the specified event type can initiate new saga instances. Start events create new saga state and begin workflow orchestration.
	/// </summary>
	/// <param name="eventType"> Event type to check for saga initiation capability. </param>
	/// <returns> True if the event can start new saga instances; otherwise, false. </returns>
	public bool IsStartEvent(Type eventType) => _startEvents.Contains(eventType);

	/// <summary>
	/// Determines if the specified event type can be processed by this saga type. This includes both start events and continuation events
	/// for workflow processing.
	/// </summary>
	/// <param name="eventType"> Event type to check for saga handling capability. </param>
	/// <returns> True if the saga can process this event type; otherwise, false. </returns>
	public bool HandlesEvent(Type eventType) => _handledEvents.Contains(eventType);

	/// <summary>
	/// Gets all event types that can be processed by this saga, including both start and continuation events.
	/// </summary>
	/// <returns> Collection of event types that the saga can handle. </returns>
	public IEnumerable<Type> GetHandledEvents() => _handledEvents;
}
