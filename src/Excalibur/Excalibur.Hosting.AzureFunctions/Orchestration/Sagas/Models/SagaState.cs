// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Saga.Models;

namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Represents the current state of a saga orchestration.
/// </summary>
public class SagaState
{
	/// <summary>
	/// Gets or sets the unique identifier for this saga instance.
	/// </summary>
	/// <value>
	/// The unique identifier for this saga instance.
	/// </value>
	public string SagaId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the current step index in the saga execution.
	/// </summary>
	/// <value>
	/// The current step index in the saga execution.
	/// </value>
	public int CurrentStepIndex { get; set; }

	/// <summary>
	/// Gets or sets the current status of the saga.
	/// </summary>
	/// <value>
	/// The current status of the saga.
	/// </value>
	public SagaStatus Status { get; set; } = SagaStatus.Created;

	/// <summary>
	/// Gets the data associated with this saga instance.
	/// </summary>
	/// <value>
	/// The data associated with this saga instance.
	/// </value>
	public ConcurrentDictionary<string, object> Data { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets the list of completed step names.
	/// </summary>
	/// <value>
	/// The list of completed step names.
	/// </value>
	public ICollection<string> CompletedSteps { get; } = [];

	/// <summary>
	/// Gets the list of compensated step names.
	/// </summary>
	/// <value>
	/// The list of compensated step names.
	/// </value>
	public ICollection<string> CompensatedSteps { get; } = [];

	/// <summary>
	/// Gets or sets any error information if the saga failed.
	/// </summary>
	/// <value>
	/// Any error information if the saga failed.
	/// </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the saga started.
	/// </summary>
	/// <value>
	/// The timestamp when the saga started.
	/// </value>
	public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the timestamp when the saga completed (if applicable).
	/// </summary>
	/// <value>
	/// The timestamp when the saga completed (if applicable).
	/// </value>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID for tracing across the saga execution.
	/// </summary>
	/// <value>
	/// The correlation ID for tracing across the saga execution.
	/// </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets the synchronization lock for thread-safe state mutation during parallel step execution.
	/// </summary>
	internal object SyncLock { get; } = new();
}
