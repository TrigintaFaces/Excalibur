// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga;

/// <summary>
/// Represents the persisted state of a saga.
/// </summary>
/// <typeparam name="TSagaData"> The type of saga data. </typeparam>
public sealed class SagaPersistedState<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Gets or initializes the saga identifier.
	/// </summary>
	/// <value>or initializes the saga identifier.</value>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the saga definition.
	/// </summary>
	/// <value>or initializes the saga definition.</value>
	public ISagaDefinition<TSagaData> Definition { get; init; } = null!;

	/// <summary>
	/// Gets or initializes the saga data.
	/// </summary>
	/// <value>or initializes the saga data.</value>
	public TSagaData Data { get; init; } = null!;

	/// <summary>
	/// Gets or initializes the current state of the saga.
	/// </summary>
	/// <value>or initializes the current state of the saga.</value>
	public SagaState State { get; init; }

	/// <summary>
	/// Gets or initializes the current step index.
	/// </summary>
	/// <value>or initializes the current step index.</value>
	public int CurrentStepIndex { get; init; }

	/// <summary>
	/// Gets or initializes the list of activities.
	/// </summary>
	/// <value>or initializes the list of activities.</value>
	public IList<SagaActivity> Activities { get; init; } = [];

	/// <summary>
	/// Gets or initializes when the saga started.
	/// </summary>
	/// <value>or initializes when the saga started.</value>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets or initializes when the saga completed.
	/// </summary>
	/// <value>or initializes when the saga completed.</value>
	public DateTimeOffset? CompletedAt { get; init; }
}

