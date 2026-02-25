// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Models;

namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a distributed saga transaction.
/// </summary>
/// <typeparam name="TData"> The type of data associated with the saga. </typeparam>
public interface ICloudNativeSaga<TData>
	where TData : class
{
	/// <summary>
	/// Gets the unique identifier of the saga.
	/// </summary>
	/// <value>the unique identifier of the saga.</value>
	string SagaId { get; }

	/// <summary>
	/// Gets the data associated with the saga.
	/// </summary>
	/// <value>the data associated with the saga.</value>
	TData Data { get; }

	/// <summary>
	/// Gets the current status of the saga.
	/// </summary>
	/// <value>the current status of the saga.</value>
	SagaStatus Status { get; }

	/// <summary>
	/// Gets the list of steps in the saga.
	/// </summary>
	/// <value>the list of steps in the saga.</value>
	IReadOnlyList<ISagaStep<TData>> Steps { get; }

	/// <summary>
	/// Gets the timestamp when the saga started.
	/// </summary>
	/// <value>the timestamp when the saga started.</value>
	DateTime StartedAt { get; }

	/// <summary>
	/// Gets the timestamp when the saga completed, if applicable.
	/// </summary>
	/// <value>the timestamp when the saga completed, if applicable., or <see langword="null"/> if not specified.</value>
	DateTime? CompletedAt { get; }

	/// <summary>
	/// Gets the current step index being executed.
	/// </summary>
	/// <value>the current step index being executed.</value>
	int CurrentStepIndex { get; }

	/// <summary>
	/// Gets any error that occurred during saga execution.
	/// </summary>
	/// <value>any error that occurred during saga execution., or <see langword="null"/> if not specified.</value>
	string? ErrorMessage { get; }

	/// <summary>
	/// Gets the correlation ID for tracking across services.
	/// </summary>
	/// <value>the correlation ID for tracking across services.</value>
	string CorrelationId { get; }
}

