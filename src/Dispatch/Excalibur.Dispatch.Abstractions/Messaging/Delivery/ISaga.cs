// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Defines the contract for event-driven choreography saga instances that manage long-running business processes through event reactions.
/// This is the <strong>Process Coordinator</strong> pattern where services autonomously react to domain events.
/// </summary>
/// <remarks>
/// <para><strong>Pattern:</strong> Event-Driven Choreography (Process Coordinator)</para>
/// <para><strong>Use When:</strong></para>
/// <list type="bullet">
/// <item>Decentralized coordination across services (no central orchestrator)</item>
/// <item>Services publish domain events autonomously</item>
/// <item>Long-running processes (hours/days)</item>
/// <item>Multiple bounded contexts participate</item>
/// <item>Eventual consistency acceptable</item>
/// </list>
/// <para><strong>Alternatives:</strong> For step-based orchestration with explicit compensation logic,
/// use <see cref="Excalibur.Saga.Abstractions.ISaga{TSagaData}"/> instead.</para>
/// <para><strong>Examples:</strong> Order fulfillment workflows, user onboarding journeys, multi-tenant provisioning processes.</para>
/// <para>The saga pattern implements process management in distributed systems, handling both success and failure scenarios through compensating
/// actions. Sagas maintain their own state and can span multiple bounded contexts or microservices.</para>
/// <para>See pattern selection guidance and architectural decisions documentation.</para>
/// </remarks>
public interface ISaga
{
	/// <summary>
	/// Gets the unique identifier for this saga instance. This identifier is used for saga persistence, correlation, and lifecycle management.
	/// </summary>
	/// <value> A <see cref="Guid" /> that uniquely identifies this saga instance across its entire lifecycle. </value>
	/// <remarks>
	/// The saga ID is typically assigned when the saga is first created and remains constant throughout the saga's execution. This
	/// identifier enables correlation of events with specific saga instances and supports saga persistence and recovery operations.
	/// </remarks>
	Guid Id { get; }

	/// <summary>
	/// Gets a value indicating whether this saga has completed its business process. Completed sagas should not process additional events
	/// and may be eligible for cleanup.
	/// </summary>
	/// <value> <c> true </c> if the saga has completed successfully or has been terminated; otherwise, <c> false </c>. </value>
	/// <remarks>
	/// Saga completion can result from successful execution of all steps, explicit termination due to business rules, or failure handling
	/// that results in compensation completion. The saga coordinator uses this property to determine whether to route events to the saga.
	/// </remarks>
	bool IsCompleted { get; }

	/// <summary>
	/// Determines whether this saga instance can handle the specified event message. This method enables event filtering and routing to
	/// appropriate saga instances.
	/// </summary>
	/// <param name="eventMessage"> The event message to evaluate for handling compatibility. </param>
	/// <returns> <c> true </c> if this saga can process the event; otherwise, <c> false </c>. </returns>
	/// <remarks>
	/// Implementations should examine the event type, content, and current saga state to determine handling eligibility. This method
	/// supports saga event correlation and helps prevent incorrect event routing in complex orchestration scenarios.
	/// </remarks>
	bool HandlesEvent(object eventMessage);

	/// <summary>
	/// Asynchronously processes the specified event message, potentially advancing the saga's state and triggering additional actions or
	/// state transitions within the business process.
	/// </summary>
	/// <param name="eventMessage"> The event message to process, which may trigger saga state changes. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during event processing. </param>
	/// <returns> A task that represents the asynchronous event handling operation. </returns>
	/// <remarks>
	/// Event handling may result in state transitions, command dispatching, timer scheduling, or saga completion. Implementations should
	/// ensure idempotency where possible and handle duplicate event delivery gracefully. The method should update saga state and coordinate
	/// any necessary downstream actions based on the business process logic.
	/// </remarks>
	Task HandleAsync(object eventMessage, CancellationToken cancellationToken);
}

/// <summary>
/// Extends the basic saga contract with strongly-typed access to saga state. This generic interface provides type-safe access to saga state
/// while maintaining compatibility with the base saga infrastructure.
/// </summary>
/// <typeparam name="TSagaState"> The type of state maintained by this saga, must inherit from <see cref="SagaState" />. </typeparam>
/// <remarks>
/// The generic saga interface enables strongly-typed saga implementations while preserving the ability to work with sagas polymorphically
/// through the base interface. This design supports both type-safe saga development and flexible saga management within the orchestration infrastructure.
/// </remarks>
public interface ISaga<out TSagaState> : ISaga
	where TSagaState : SagaState
{
	/// <summary>
	/// Gets the current state of the saga, providing typed access to saga-specific data and process tracking information maintained
	/// throughout the saga's lifecycle.
	/// </summary>
	/// <value>
	/// The current state instance of type <typeparamref name="TSagaState" /> containing all data and status information for this saga's
	/// business process.
	/// </value>
	/// <remarks>
	/// The saga state encapsulates all information needed to track the business process, including progress indicators, accumulated data,
	/// error states, and correlation information. State changes are typically coordinated through event handling and persist across saga
	/// lifecycle boundaries to enable process recovery and consistency.
	/// </remarks>
	TSagaState State { get; }
}
