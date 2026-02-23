// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Default implementation of <see cref="IWorkflowContext" /> providing workflow orchestration capabilities.
/// </summary>
/// <remarks>
/// <para>Initializes a new instance of the <see cref="WorkflowContext" /> class.</para>
/// <para><strong>Preview limitation:</strong> This implementation uses in-memory state only.
/// It does not provide durable scheduling, real step dispatch, or persistent checkpoints.
/// A production workflow orchestration implementation is planned.</para>
/// </remarks>
/// <param name="instanceId"> The unique identifier for this workflow execution instance. </param>
/// <param name="correlationId"> Optional correlation identifier for tracking across services. </param>
[Obsolete("Preview: This implementation uses in-memory state only. See documentation for limitations.")]
public class WorkflowContext(string instanceId, string? correlationId = null) : IWorkflowContext
{
	private readonly ConcurrentDictionary<string, object?> _properties = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _pendingEvents = new(StringComparer.Ordinal);
	private readonly ConcurrentBag<Task> _scheduledSteps = [];
	private long _checkpointSequence;

	/// <inheritdoc />
	public string InstanceId { get; } = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

	/// <inheritdoc />
	public string? CorrelationId { get; } = correlationId;

	/// <inheritdoc />
	public string? CurrentStepId { get; set; }

	/// <inheritdoc />
	public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public IDictionary<string, object?> Properties => _properties;

	/// <inheritdoc />
	public async Task ScheduleStepAsync(string stepId, DateTimeOffset scheduledTime, object? stepData,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

		// Calculate delay from current time
		var delay = scheduledTime - DateTimeOffset.UtcNow;
		if (delay <= TimeSpan.Zero)
		{
			// Execute immediately if scheduled time is in the past
			await ExecuteStepAsync(stepId, stepData, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await ScheduleStepAsync(stepId, delay, stepData, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task ScheduleStepAsync(string stepId, TimeSpan delay, object? stepData,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

		if (delay <= TimeSpan.Zero)
		{
			// Execute immediately if no delay
			await ExecuteStepAsync(stepId, stepData, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// Schedule for later execution with tracking
			var task = Task.Factory.StartNew(
					async () =>
					{
						await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
						await ExecuteStepAsync(stepId, stepData, cancellationToken).ConfigureAwait(false);
					},
					cancellationToken,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap();
			_scheduledSteps.Add(task);
		}
	}

	/// <inheritdoc />
	public async Task<object?> WaitForEventAsync(string eventName, TimeSpan? timeout, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

		var tcs = _pendingEvents.GetOrAdd(eventName, _ => new TaskCompletionSource<object?>());

		if (timeout.HasValue)
		{
			using var timeoutCts = new CancellationTokenSource(timeout.Value);
			using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				await using var registration = combinedCts.Token.Register(() => tcs.TrySetCanceled());
				return await tcs.Task.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
			{
				throw new TimeoutException($"Timeout waiting for event '{eventName}' after {timeout.Value}");
			}
		}

		await using var ctr = cancellationToken.Register(() => tcs.TrySetCanceled());
		return await tcs.Task.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task RaiseEventAsync(string eventName, object? eventData, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

		if (_pendingEvents.TryRemove(eventName, out var tcs))
		{
			_ = tcs.TrySetResult(eventData);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CreateCheckpointAsync(object? checkpointData, CancellationToken cancellationToken)
	{
		// Use atomic sequence number to prevent timestamp collisions when called multiple times within the same tick
		var sequence = Interlocked.Increment(ref _checkpointSequence);
		Properties[$"checkpoint_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{sequence}"] = checkpointData;
		return Task.CompletedTask;
	}

	/// <summary>
	/// Waits for all scheduled steps to complete. Call this to observe any exceptions from background step executions.
	/// </summary>
	/// <returns> A task representing the asynchronous operation. </returns>
	public Task WaitForScheduledStepsAsync() => Task.WhenAll(_scheduledSteps);

	/// <summary>
	/// Executes a workflow step. This is a simplified implementation that could be extended to integrate with a more sophisticated workflow engine.
	/// </summary>
	/// <param name="stepId"> The step identifier to execute. </param>
	/// <param name="stepData"> Optional data for the step. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	protected virtual Task ExecuteStepAsync(string stepId, object? stepData, CancellationToken cancellationToken)
	{
		// Set current step for tracking
		CurrentStepId = stepId;

		// In a real implementation, this would dispatch to the appropriate step handler For now, we just update the properties to indicate
		// the step was executed
		Properties[$"step_{stepId}_executed_at"] = DateTimeOffset.UtcNow;
		Properties[$"step_{stepId}_data"] = stepData;

		return Task.CompletedTask;
	}
}
