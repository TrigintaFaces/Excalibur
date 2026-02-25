// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Provides context information and orchestration capabilities for workflow execution.
/// </summary>
public interface IWorkflowContext
{
	/// <summary>
	/// Gets the unique identifier for this workflow execution instance.
	/// </summary>
	/// <value>
	/// The unique identifier for this workflow execution instance.
	/// </value>
	string InstanceId { get; }

	/// <summary>
	/// Gets the correlation identifier for tracking across services.
	/// </summary>
	/// <value>
	/// The correlation identifier for tracking across services.
	/// </value>
	string? CorrelationId { get; }

	/// <summary>
	/// Gets or sets the current step identifier in the workflow.
	/// </summary>
	/// <value>
	/// The current step identifier in the workflow.
	/// </value>
	string? CurrentStepId { get; set; }

	/// <summary>
	/// Gets the workflow execution start time.
	/// </summary>
	/// <value>
	/// The workflow execution start time.
	/// </value>
	DateTimeOffset StartedAt { get; }

	/// <summary>
	/// Gets the collection of custom properties for this workflow execution.
	/// </summary>
	/// <value>
	/// The collection of custom properties for this workflow execution.
	/// </value>
	IDictionary<string, object?> Properties { get; }

	/// <summary>
	/// Schedules a step to execute at a specific time.
	/// </summary>
	/// <param name="stepId"> The unique identifier for the step. </param>
	/// <param name="scheduledTime"> The time when the step should execute. </param>
	/// <param name="stepData"> Optional data to pass to the step. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ScheduleStepAsync(string stepId, DateTimeOffset scheduledTime, object? stepData,
		CancellationToken cancellationToken);

	/// <summary>
	/// Schedules a step to execute after a specific delay.
	/// </summary>
	/// <param name="stepId"> The unique identifier for the step. </param>
	/// <param name="delay"> The delay before executing the step. </param>
	/// <param name="stepData"> Optional data to pass to the step. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ScheduleStepAsync(string stepId, TimeSpan delay, object? stepData, CancellationToken cancellationToken);

	/// <summary>
	/// Waits for an external event to occur before continuing workflow execution.
	/// </summary>
	/// <param name="eventName"> The name of the event to wait for. </param>
	/// <param name="timeout"> Optional timeout for waiting for the event. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task that completes when the event occurs, containing the event data. </returns>
	Task<object?> WaitForEventAsync(string eventName, TimeSpan? timeout, CancellationToken cancellationToken);

	/// <summary>
	/// Raises an event that can be consumed by other workflow steps or external systems.
	/// </summary>
	/// <param name="eventName"> The name of the event to raise. </param>
	/// <param name="eventData"> Optional data to include with the event. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RaiseEventAsync(string eventName, object? eventData, CancellationToken cancellationToken);

	/// <summary>
	/// Creates a checkpoint in workflow execution for fault tolerance.
	/// </summary>
	/// <param name="checkpointData"> Data to save at this checkpoint. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task CreateCheckpointAsync(object? checkpointData, CancellationToken cancellationToken);
}
