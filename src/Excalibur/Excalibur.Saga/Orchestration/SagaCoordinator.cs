// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Coordinates saga execution by processing saga events and managing saga state transitions. This component serves as the main orchestrator
/// for long-running business processes implemented as sagas, handling event routing, saga instantiation, state management, and completion tracking.
/// </summary>
/// <param name="serviceProvider"> Service provider for dependency injection and saga instantiation. </param>
/// <param name="sagaStore"> Persistent store for saga state management and retrieval. </param>
/// <param name="logger"> Logger for saga coordination activities, errors, and performance metrics. </param>
public sealed partial class SagaCoordinator(IServiceProvider serviceProvider, ISagaStore sagaStore, ILogger<SagaCoordinator> logger)
	: ISagaCoordinator
{
	private static readonly MethodInfo HandleEventInternalMethodInfo =
		typeof(SagaCoordinator).GetMethod(
			nameof(HandleEventInternalAsync),
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

	private static readonly ConcurrentDictionary<(Type SagaType, Type StateType), MethodInfo> GenericMethodCache = new();
	/// <summary>
	/// Processes a saga event by routing it to the appropriate saga instance and managing state transitions. This method handles saga
	/// discovery, instantiation, event processing, and state persistence for long-running business processes. Supports both saga initiation
	/// events and continuation events with proper error handling and logging.
	/// </summary>
	/// <param name="messageContext"> Message context containing routing, tracing, and correlation information. </param>
	/// <param name="evt"> Saga event to process, containing business data and saga correlation identifier. </param>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns> Task representing the asynchronous saga event processing operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when messageContext or evt is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga configuration is invalid or saga cannot be instantiated. </exception>
	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection to invoke generic HandleEventAsyncInternal method with runtime types")]
	[RequiresDynamicCode("This method uses MakeGenericMethod with runtime types")]
	public async Task ProcessEventAsync(
		IMessageContext messageContext,
		ISagaEvent evt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageContext);
		ArgumentNullException.ThrowIfNull(evt);

		var sagaType = SagaRegistry.GetSagaTypeForEvent(evt.GetType());
		if (sagaType is null)
		{
			LogNoSagaRegistered(evt.GetType().Name);

			return;
		}

		var sagaInfo = SagaRegistry.GetSagaInfo(sagaType);
		if (sagaInfo is null)
		{
			LogNoSagaMetadata(sagaType.Name);

			return;
		}

		var sagaStateType = sagaInfo.StateType;

		var method = GenericMethodCache.GetOrAdd(
			(sagaType, sagaStateType),
			static key => HandleEventInternalMethodInfo.MakeGenericMethod(key.SagaType, key.StateType));

		var result = method.Invoke(this, [messageContext, evt, sagaInfo, cancellationToken]);
		if (result is not Task task)
		{
			throw new InvalidOperationException(
				$"Expected Task from {method.DeclaringType?.Name}.{method.Name} but got {result?.GetType().Name ?? "null"}");
		}

		await task.ConfigureAwait(false);
	}

	/// <summary>
	/// Internal generic method that handles saga event processing with strongly-typed saga and state types. This method manages the
	/// complete saga lifecycle including state loading, event processing, and persistence. Handles both saga initiation (creating new
	/// state) and continuation (loading existing state) scenarios.
	/// </summary>
	/// <typeparam name="TSaga"> Specific saga type that will process the event. </typeparam>
	/// <typeparam name="TSagaState"> State type associated with the saga for persistence. </typeparam>
	/// <param name="messageContext"> Message context providing correlation and routing information. </param>
	/// <param name="evt"> Saga event containing business data and correlation identifiers. </param>
	/// <param name="sagaInfo"> Metadata about the saga including event handling capabilities. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and shutdown support. </param>
	/// <returns> Task representing the asynchronous saga event processing operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga cannot be instantiated or state cannot be managed. </exception>
	[RequiresUnreferencedCode("Uses reflection to instantiate saga types")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Saga types are preserved through configuration and DI registration")]
	[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with RequiresDynamicCodeAttribute may break when trimming",
		Justification = "Saga types are preserved through configuration")]
	public async Task HandleEventInternalAsync<TSaga, TSagaState>(
		IMessageContext messageContext,
		ISagaEvent evt,
		SagaInfo sagaInfo,
		CancellationToken cancellationToken)
		where TSaga : SagaBase<TSagaState>
		where TSagaState : SagaState, new()
	{
		ArgumentNullException.ThrowIfNull(messageContext);
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(sagaInfo);

		TSagaState? sagaState;

		var isStartEvent = sagaInfo.IsStartEvent(evt.GetType());

		if (isStartEvent)
		{
			sagaState = new TSagaState { SagaId = Guid.Parse(evt.SagaId) };
		}
		else
		{
			sagaState = await sagaStore.LoadAsync<TSagaState>(Guid.Parse(evt.SagaId), cancellationToken).ConfigureAwait(false);
			if (sagaState is null)
			{
				LogSagaNotFound(evt.SagaId, evt.GetType().Name);

				return;
			}
		}

		var saga = ActivatorUtilities.CreateInstance<TSaga>(serviceProvider, sagaState);

		if (!saga.HandlesEvent(evt))
		{
			LogSagaNotHandled(evt.SagaId, evt.GetType().Name);

			return;
		}

		await saga.HandleAsync(evt, cancellationToken).ConfigureAwait(false);

		await sagaStore.SaveAsync(sagaState, cancellationToken).ConfigureAwait(false);

		if (saga.IsCompleted)
		{
			LogSagaCompleted(evt.SagaId);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.SagaStepFailedStartingCompensation, LogLevel.Warning,
		"Saga {SagaId} not found for event {EventType}.")]
	private partial void LogSagaNotFound(string sagaId, string eventType);

	[LoggerMessage(SagaEventId.SagaStepExecuting, LogLevel.Warning,
		"Saga {SagaId} does not handle event {EventType}.")]
	private partial void LogSagaNotHandled(string sagaId, string eventType);

	[LoggerMessage(SagaEventId.SagaCompensationCompleted, LogLevel.Information,
		"Saga {SagaId} completed and persisted.")]
	private partial void LogSagaCompleted(string sagaId);

	[LoggerMessage(SagaEventId.SagaExecutionStarting, LogLevel.Warning,
		"No saga registered for event type {EventType}")]
	private partial void LogNoSagaRegistered(string eventType);

	[LoggerMessage(SagaEventId.SagaExecutionFailed, LogLevel.Error,
		"No saga metadata configured for saga type {SagaType}")]
	private partial void LogNoSagaMetadata(string sagaType);
}
