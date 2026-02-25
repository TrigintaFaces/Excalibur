// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Orchestrates saga execution.
/// </summary>
public interface ISagaOrchestrator
{
	/// <summary>
	/// Creates a new saga instance.
	/// </summary>
	/// <typeparam name="TSagaData"> The type of saga data. </typeparam>
	/// <param name="sagaDefinition"> The saga definition. </param>
	/// <param name="data"> The initial saga data. </param>
	/// <returns> The created saga. </returns>
	IOrchestrationSaga<TSagaData> CreateSaga<TSagaData>(ISagaDefinition<TSagaData> sagaDefinition, TSagaData data)
		where TSagaData : class;

	/// <summary>
	/// Retrieves an existing saga by ID.
	/// </summary>
	/// <typeparam name="TSagaData"> The type of saga data. </typeparam>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The saga if found, null otherwise. </returns>
	Task<IOrchestrationSaga<TSagaData>?> GetSagaAsync<TSagaData>(string sagaId, CancellationToken cancellationToken)
		where TSagaData : class;

	/// <summary>
	/// Lists active sagas.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> List of active saga summaries. </returns>
	Task<IEnumerable<SagaSummary>> ListActiveSagasAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Cancels a saga by ID.
	/// </summary>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <param name="reason"> The reason for cancellation. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the cancellation operation. </returns>
	Task CancelSagaAsync(string sagaId, string reason, CancellationToken cancellationToken);
}
