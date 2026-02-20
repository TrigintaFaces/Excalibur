// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a step-based saga orchestrator that coordinates multiple operations with explicit compensation support for failure scenarios.
/// This is the <strong>Process Manager</strong> pattern where a central coordinator directs participant services.
/// </summary>
/// <typeparam name="TSagaData"> The type of data flowing through the saga steps. </typeparam>
/// <remarks>
/// <para><strong>Pattern:</strong> Step-Based Orchestration (Process Manager)</para>
/// <para><strong>Use When:</strong></para>
/// <list type="bullet">
/// <item>Central coordinator owns business logic and directs workflow</item>
/// <item>Explicit compensation required for failures (e.g., payment rollback)</item>
/// <item>Steps execute sequentially with dependencies</item>
/// <item>Single bounded context owns the workflow</item>
/// <item>Strong consistency guarantees needed</item>
/// </list>
/// <para><strong>Alternatives:</strong> For event-driven choreography patterns where services autonomously react to domain events,
/// use <see cref="Messaging.Delivery.ISaga"/> instead.</para>
/// <para><strong>Examples:</strong> Order processing saga (payment â†’ inventory â†’ shipment), multi-step approval workflows,
/// transactional sagas requiring compensating actions.</para>
/// </remarks>
public interface IOrchestrationSaga<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Gets the unique identifier for this saga instance.
	/// </summary>
	/// <value> The saga identifier. </value>
	string SagaId { get; }

	/// <summary>
	/// Gets the current state of the saga.
	/// </summary>
	/// <value> The saga state. </value>
	SagaState State { get; }

	/// <summary>
	/// Gets the saga data.
	/// </summary>
	/// <value> The saga data payload. </value>
	TSagaData Data { get; }

	/// <summary>
	/// Executes the saga to completion or until failure.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The final saga result. </returns>
	Task<SagaResult<TSagaData>> ExecuteAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Compensates the saga by rolling back completed steps.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The compensation result. </returns>
	Task<CompensationResult> CompensateAsync(CancellationToken cancellationToken);
}
