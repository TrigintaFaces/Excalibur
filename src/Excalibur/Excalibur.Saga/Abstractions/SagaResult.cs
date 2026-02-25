// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents the result of a saga execution.
/// </summary>
/// <typeparam name="TSagaData"> The type of saga data. </typeparam>
public sealed class SagaResult<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Gets or initializes the saga identifier.
	/// </summary>
	/// <value> The saga identifier. </value>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the final state of the saga.
	/// </summary>
	/// <value> The final saga state. </value>
	public SagaState FinalState { get; init; }

	/// <summary>
	/// Gets or initializes the saga data.
	/// </summary>
	/// <value> The saga data payload. </value>
	public TSagaData Data { get; init; } = null!;

	/// <summary>
	/// Gets a value indicating whether the saga completed successfully.
	/// </summary>
	/// <value> <see langword="true" /> when the saga completed successfully; otherwise, <see langword="false" />. </value>
	public bool IsSuccess => FinalState == SagaState.Completed;

	/// <summary>
	/// Gets or initializes the error message if the saga failed.
	/// </summary>
	/// <value> The error message or <see langword="null" />. </value>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets or initializes the exception that occurred.
	/// </summary>
	/// <value> The exception instance or <see langword="null" />. </value>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Gets or initializes the duration of the saga execution.
	/// </summary>
	/// <value> The saga execution duration. </value>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets or initializes the list of activities performed during the saga.
	/// </summary>
	/// <value> The list of saga activities. </value>
	public IReadOnlyList<SagaActivity> Activities { get; init; } = [];
}
