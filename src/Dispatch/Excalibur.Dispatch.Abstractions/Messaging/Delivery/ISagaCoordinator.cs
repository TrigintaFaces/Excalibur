// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Defines the contract for saga coordination services that manage the lifecycle and event routing for saga instances within the
/// distributed messaging system. The coordinator is responsible for finding appropriate saga instances, routing events, and managing saga
/// state transitions.
/// </summary>
/// <remarks>
/// The saga coordinator serves as the central orchestration point for saga pattern implementation, handling event correlation, saga
/// instance resolution, and coordination of complex business processes. It bridges between the messaging infrastructure and individual saga
/// instances, ensuring proper event routing and saga lifecycle management in distributed system architectures.
/// </remarks>
public interface ISagaCoordinator
{
	/// <summary>
	/// Asynchronously processes a saga event by routing it to appropriate saga instances and coordinating any resulting state transitions
	/// or actions within the business process.
	/// </summary>
	/// <param name="messageContext">
	/// The message processing context containing metadata, correlation information, and processing state accumulated during message
	/// pipeline execution.
	/// </param>
	/// <param name="evt">
	/// The saga event to process, containing business data and correlation information needed for saga instance resolution and state updates.
	/// </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during saga event processing. </param>
	/// <returns> A task that represents the asynchronous saga event processing operation. </returns>
	/// <remarks>
	/// <para>
	/// This method orchestrates the complete saga event processing workflow:
	/// - Correlates events with existing saga instances or creates new instances as needed
	/// - Routes events to appropriate saga handlers based on event type and saga state
	/// - Manages saga state persistence and recovery operations
	/// - Coordinates any downstream actions or command dispatching triggered by state transitions
	/// </para>
	/// <para>
	/// The coordinator ensures saga consistency and handles both success and failure scenarios, including compensation logic and error
	/// recovery procedures.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("This method uses reflection to invoke generic HandleEventAsyncInternal method with runtime types")]
	[RequiresDynamicCode("This method uses MakeGenericMethod with runtime types")]
	Task ProcessEventAsync(
		IMessageContext messageContext,
		ISagaEvent evt,
		CancellationToken cancellationToken);
}
