// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Global registry for saga type configuration and event-to-saga mapping. This static registry maintains the relationship between event
/// types and saga types, enabling runtime saga discovery and coordination for workflow orchestration.
/// </summary>
public static class SagaRegistry
{
	private static readonly ConcurrentDictionary<Type, SagaInfo> EventToSagaMap = new();

	/// <summary>
	/// Registers a saga type with its event handling configuration for workflow orchestration. This method establishes the mapping between
	/// events and saga types, enabling runtime saga discovery and automatic event routing to appropriate saga instances.
	/// </summary>
	/// <typeparam name="TSaga"> Saga implementation type that handles business logic and workflow coordination. </typeparam>
	/// <typeparam name="TSagaState"> State type for saga persistence and workflow state tracking. </typeparam>
	/// <param name="configure"> Configuration action to specify handled events and saga lifecycle behavior. </param>
	/// <exception cref="ArgumentNullException"> Thrown when configure action is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga configuration conflicts with existing registrations. </exception>
	public static void Register<TSaga, TSagaState>(
		Action<SagaInfo> configure)
		where TSaga : SagaBase<TSagaState>
		where TSagaState : SagaState, new()
	{
		ArgumentNullException.ThrowIfNull(configure);

		var info = new SagaInfo(typeof(TSaga), typeof(TSagaState));
		configure(info);

		foreach (var eventType in info.GetHandledEvents())
		{
			EventToSagaMap[eventType] = info;
		}
	}

	/// <summary>
	/// Retrieves the saga type that can handle the specified event type. This method supports runtime saga discovery for event routing and
	/// workflow coordination.
	/// </summary>
	/// <param name="eventType"> Event type to find the corresponding saga handler. </param>
	/// <returns> Saga type that can handle the event, or null if no handler is registered. </returns>
	public static Type? GetSagaTypeForEvent(Type eventType) => EventToSagaMap.TryGetValue(eventType, out var info) ? info.SagaType : null;

	/// <summary>
	/// Retrieves the saga configuration information for the specified saga type. This method provides access to saga metadata including
	/// handled events and lifecycle configuration.
	/// </summary>
	/// <param name="sagaType"> Saga type to retrieve configuration information. </param>
	/// <returns> SagaInfo containing configuration metadata, or null if the saga type is not registered. </returns>
	public static SagaInfo? GetSagaInfo(Type sagaType) => EventToSagaMap.Values.FirstOrDefault(x => x.SagaType == sagaType);
}
