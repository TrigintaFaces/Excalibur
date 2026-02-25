// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using SagaOrchestration.Steps;
using SagaOrchestration.Timeouts;

namespace SagaOrchestration.Sagas;

/// <summary>
/// Interface for saga state persistence.
/// </summary>
public interface ISagaStateStore
{
	/// <summary>
	/// Saves the saga state.
	/// </summary>
	Task SaveAsync(OrderSagaData data, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the saga state by ID.
	/// </summary>
	Task<OrderSagaData?> GetAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all sagas matching a status.
	/// </summary>
	Task<List<OrderSagaData>> GetByStatusAsync(SagaStatus status, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all sagas.
	/// </summary>
	Task<List<OrderSagaData>> GetAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Interface for timeout persistence.
/// </summary>
public interface ITimeoutStore
{
	/// <summary>
	/// Saves a timeout entry.
	/// </summary>
	Task SaveAsync(TimeoutEntry entry, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a timeout by saga ID and timeout ID.
	/// </summary>
	Task<TimeoutEntry?> GetAsync(string sagaId, string timeoutId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all pending timeouts for a saga.
	/// </summary>
	Task<List<TimeoutEntry>> GetPendingAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all due timeouts.
	/// </summary>
	Task<List<TimeoutEntry>> GetAllDueAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Updates the status of a timeout.
	/// </summary>
	Task UpdateStatusAsync(string sagaId, string timeoutId, TimeoutStatus status, CancellationToken cancellationToken);

	/// <summary>
	/// Cancels all pending timeouts for a saga.
	/// </summary>
	Task CancelAllForSagaAsync(string sagaId, CancellationToken cancellationToken);
}

/// <summary>
/// Order fulfillment saga that orchestrates the complete order flow.
/// </summary>
/// <remarks>
/// <para>
/// This saga demonstrates:
/// <list type="bullet">
///   <item>Multi-step orchestration (ReserveInventory → ProcessPayment → ShipOrder)</item>
///   <item>LIFO compensation (ShipOrder → ProcessPayment → ReserveInventory)</item>
///   <item>Timeout scheduling for inventory reservation</item>
///   <item>State persistence after each step</item>
///   <item>Retry policies for transient failures</item>
/// </list>
/// </para>
/// <para>
/// Execution Flow:
/// <code>
///   Start → ReserveInventory → ProcessPayment → ShipOrder → Complete
///                                    │
///                                    └── (on failure) ──→ Compensate
///                                                              │
///                                                              ↓
///                                                     ReserveInventory ← ProcessPayment
/// </code>
/// </para>
/// </remarks>
public sealed partial class OrderFulfillmentSaga
{
	private readonly ILogger<OrderFulfillmentSaga> _logger;
	private readonly ISagaStateStore _stateStore;
	private readonly ITimeoutStore _timeoutStore;
	private readonly IReadOnlyList<ISagaStep> _steps;
	private int _timeoutCounter;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderFulfillmentSaga"/> class.
	/// </summary>
	public OrderFulfillmentSaga(
		ILogger<OrderFulfillmentSaga> logger,
		ISagaStateStore stateStore,
		ITimeoutStore timeoutStore,
		IEnumerable<ISagaStep> steps)
	{
		_logger = logger;
		_stateStore = stateStore;
		_timeoutStore = timeoutStore;
		_steps = steps.ToList();
	}

	/// <summary>
	/// Starts the saga for a new order.
	/// </summary>
	public async Task StartAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);

		data.SagaId = $"saga-{Guid.NewGuid():N}";
		data.Status = SagaStatus.Running;
		data.CreatedAt = DateTimeOffset.UtcNow;
		data.LastUpdatedAt = DateTimeOffset.UtcNow;

		LogSagaStarted(_logger, data.SagaId, data.OrderId);

		await _stateStore.SaveAsync(data, cancellationToken).ConfigureAwait(false);

		// Schedule inventory reservation timeout
		var timeoutId = await RequestTimeoutAsync<InventoryReservationTimeout>(
			data.SagaId,
			TimeSpan.FromMinutes(5),
			cancellationToken).ConfigureAwait(false);

		LogTimeoutScheduled(_logger, data.SagaId, typeof(InventoryReservationTimeout).Name, timeoutId);

		// Execute all steps
		var success = await ExecuteStepsAsync(data, cancellationToken).ConfigureAwait(false);

		if (success)
		{
			// Cancel the inventory timeout since we completed successfully
			_ = await CancelTimeoutAsync(data.SagaId, timeoutId, cancellationToken).ConfigureAwait(false);
			LogTimeoutCancelled(_logger, data.SagaId, timeoutId);

			data.Status = SagaStatus.Completed;
			LogSagaCompleted(_logger, data.SagaId, data.OrderId);
		}
		else
		{
			// Failure occurred, compensation already handled in ExecuteStepsAsync
			LogSagaFailed(_logger, data.SagaId, data.OrderId, data.FailureReason ?? "Unknown");
		}

		await _stateStore.SaveAsync(data, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Resumes a saga from persisted state (e.g., after process restart).
	/// </summary>
	public async Task<bool> ResumeAsync(string sagaId, CancellationToken cancellationToken)
	{
		var data = await _stateStore.GetAsync(sagaId, cancellationToken).ConfigureAwait(false);
		if (data == null)
		{
			LogSagaNotFound(_logger, sagaId);
			return false;
		}

		LogSagaResuming(_logger, sagaId, data.CompletedSteps.Count);

		// Find where we left off and continue
		var remainingSteps = _steps.Skip(data.CompletedSteps.Count).ToList();
		if (remainingSteps.Count == 0)
		{
			LogSagaAlreadyComplete(_logger, sagaId);
			return true;
		}

		// Continue execution from where we left off
		foreach (var step in remainingSteps)
		{
			if (!await ExecuteStepWithCompensationAsync(data, step, cancellationToken).ConfigureAwait(false))
			{
				return false;
			}
		}

		data.Status = SagaStatus.Completed;
		await _stateStore.SaveAsync(data, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <summary>
	/// Schedules a timeout for this saga.
	/// </summary>
	public async Task<string> RequestTimeoutAsync<TTimeout>(
		string sagaId,
		TimeSpan delay,
		CancellationToken cancellationToken)
		where TTimeout : class
	{
		var timeoutId = $"timeout-{Interlocked.Increment(ref _timeoutCounter) - 1}";
		var entry = new TimeoutEntry
		{
			Id = timeoutId,
			SagaId = sagaId,
			TimeoutType = typeof(TTimeout),
			DueAt = DateTimeOffset.UtcNow + delay,
			Status = TimeoutStatus.Pending,
		};

		await _timeoutStore.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
		return timeoutId;
	}

	/// <summary>
	/// Cancels a scheduled timeout.
	/// </summary>
	public async Task<bool> CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken)
	{
		var entry = await _timeoutStore.GetAsync(sagaId, timeoutId, cancellationToken).ConfigureAwait(false);
		if (entry == null || entry.Status != TimeoutStatus.Pending)
		{
			return false;
		}

		await _timeoutStore.UpdateStatusAsync(sagaId, timeoutId, TimeoutStatus.Cancelled, cancellationToken)
			.ConfigureAwait(false);
		return true;
	}

	private async Task<bool> ExecuteStepsAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		foreach (var step in _steps)
		{
			if (!await ExecuteStepWithCompensationAsync(data, step, cancellationToken).ConfigureAwait(false))
			{
				return false;
			}
		}

		return true;
	}

	private async Task<bool> ExecuteStepWithCompensationAsync(
		OrderSagaData data,
		ISagaStep step,
		CancellationToken cancellationToken)
	{
		LogStepExecuting(_logger, data.SagaId, step.Name);

		var success = await step.ExecuteAsync(data, cancellationToken).ConfigureAwait(false);

		if (success)
		{
			data.CompletedSteps.Add(step.Name);
			data.Version++;
			data.LastUpdatedAt = DateTimeOffset.UtcNow;
			await _stateStore.SaveAsync(data, cancellationToken).ConfigureAwait(false);

			LogStepCompleted(_logger, data.SagaId, step.Name);
			return true;
		}

		// Step failed - trigger compensation
		LogStepFailed(_logger, data.SagaId, step.Name, data.FailureReason ?? "Unknown");

		await CompensateAsync(data, cancellationToken).ConfigureAwait(false);
		return false;
	}

	private async Task CompensateAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		data.Status = SagaStatus.Compensating;
		await _stateStore.SaveAsync(data, cancellationToken).ConfigureAwait(false);

		LogCompensationStarting(_logger, data.SagaId, data.CompletedSteps.Count);

		var hasFailures = false;

		// Compensate in LIFO (reverse) order
		foreach (var stepName in data.CompletedSteps.AsEnumerable().Reverse().ToList())
		{
			var step = _steps.FirstOrDefault(s => s.Name == stepName);
			if (step == null)
			{
				LogCompensationStepNotFound(_logger, data.SagaId, stepName);
				hasFailures = true;
				continue;
			}

			LogCompensating(_logger, data.SagaId, stepName);

			var compensated = await step.CompensateAsync(data, cancellationToken).ConfigureAwait(false);
			if (compensated)
			{
				LogCompensationSucceeded(_logger, data.SagaId, stepName);
			}
			else
			{
				LogCompensationFailed(_logger, data.SagaId, stepName);
				hasFailures = true;
				// Continue compensating remaining steps even if one fails
			}
		}

		data.Status = hasFailures ? SagaStatus.PartiallyCompensated : SagaStatus.Compensated;
		data.LastUpdatedAt = DateTimeOffset.UtcNow;
		await _stateStore.SaveAsync(data, cancellationToken).ConfigureAwait(false);

		if (hasFailures)
		{
			LogCompensationPartial(_logger, data.SagaId);
		}
		else
		{
			LogCompensationComplete(_logger, data.SagaId);
		}
	}

	#region Logging

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} started for order {OrderId}")]
	private static partial void LogSagaStarted(ILogger logger, string sagaId, string orderId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} completed successfully for order {OrderId}")]
	private static partial void LogSagaCompleted(ILogger logger, string sagaId, string orderId);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} failed for order {OrderId}: {Reason}")]
	private static partial void LogSagaFailed(ILogger logger, string sagaId, string orderId, string reason);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} not found")]
	private static partial void LogSagaNotFound(ILogger logger, string sagaId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Resuming saga {SagaId} with {CompletedSteps} steps already completed")]
	private static partial void LogSagaResuming(ILogger logger, string sagaId, int completedSteps);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} already complete")]
	private static partial void LogSagaAlreadyComplete(ILogger logger, string sagaId);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Saga {SagaId} executing step {StepName}")]
	private static partial void LogStepExecuting(ILogger logger, string sagaId, string stepName);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} step {StepName} completed")]
	private static partial void LogStepCompleted(ILogger logger, string sagaId, string stepName);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} step {StepName} failed: {Reason}")]
	private static partial void LogStepFailed(ILogger logger, string sagaId, string stepName, string reason);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} starting compensation for {StepCount} steps")]
	private static partial void LogCompensationStarting(ILogger logger, string sagaId, int stepCount);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Saga {SagaId} compensating step {StepName}")]
	private static partial void LogCompensating(ILogger logger, string sagaId, string stepName);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} compensation for step {StepName} succeeded")]
	private static partial void LogCompensationSucceeded(ILogger logger, string sagaId, string stepName);

	[LoggerMessage(Level = LogLevel.Error, Message = "Saga {SagaId} compensation for step {StepName} failed")]
	private static partial void LogCompensationFailed(ILogger logger, string sagaId, string stepName);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} compensation step {StepName} not found")]
	private static partial void LogCompensationStepNotFound(ILogger logger, string sagaId, string stepName);

	[LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} compensation complete")]
	private static partial void LogCompensationComplete(ILogger logger, string sagaId);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} compensation partially complete (some steps failed)")]
	private static partial void LogCompensationPartial(ILogger logger, string sagaId);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Saga {SagaId} scheduled timeout {TimeoutType}, ID: {TimeoutId}")]
	private static partial void LogTimeoutScheduled(ILogger logger, string sagaId, string timeoutType, string timeoutId);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Saga {SagaId} cancelled timeout {TimeoutId}")]
	private static partial void LogTimeoutCancelled(ILogger logger, string sagaId, string timeoutId);

	#endregion Logging
}

/// <summary>
/// In-memory implementation of <see cref="ISagaStateStore"/> for demonstration.
/// </summary>
public sealed class InMemorySagaStateStore : ISagaStateStore
{
	private readonly ConcurrentDictionary<string, OrderSagaData> _states = new();

	/// <inheritdoc/>
	public Task SaveAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		data.LastUpdatedAt = DateTimeOffset.UtcNow;
		_states[data.SagaId] = data;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<OrderSagaData?> GetAsync(string sagaId, CancellationToken cancellationToken)
	{
		_ = _states.TryGetValue(sagaId, out var data);
		return Task.FromResult(data);
	}

	/// <inheritdoc/>
	public Task<List<OrderSagaData>> GetByStatusAsync(SagaStatus status, CancellationToken cancellationToken)
	{
		var matching = _states.Values.Where(s => s.Status == status).ToList();
		return Task.FromResult(matching);
	}

	/// <inheritdoc/>
	public Task<List<OrderSagaData>> GetAllAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(_states.Values.ToList());
	}

	/// <summary>
	/// Backdates the last update time for testing stuck saga detection.
	/// </summary>
	public void BackdateLastUpdate(string sagaId, TimeSpan age)
	{
		if (_states.TryGetValue(sagaId, out var state))
		{
			state.LastUpdatedAt = DateTimeOffset.UtcNow - age;
		}
	}
}

/// <summary>
/// In-memory implementation of <see cref="ITimeoutStore"/> for demonstration.
/// </summary>
public sealed class InMemoryTimeoutStore : ITimeoutStore
{
	private readonly ConcurrentDictionary<string, List<TimeoutEntry>> _timeouts = new();

	/// <inheritdoc/>
	public Task SaveAsync(TimeoutEntry entry, CancellationToken cancellationToken)
	{
		var list = _timeouts.GetOrAdd(entry.SagaId, _ => new List<TimeoutEntry>());
		lock (list)
		{
			list.Add(entry);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<TimeoutEntry?> GetAsync(string sagaId, string timeoutId, CancellationToken cancellationToken)
	{
		if (_timeouts.TryGetValue(sagaId, out var list))
		{
			lock (list)
			{
				return Task.FromResult(list.FirstOrDefault(t => t.Id == timeoutId));
			}
		}

		return Task.FromResult<TimeoutEntry?>(null);
	}

	/// <inheritdoc/>
	public Task<List<TimeoutEntry>> GetPendingAsync(string sagaId, CancellationToken cancellationToken)
	{
		if (_timeouts.TryGetValue(sagaId, out var list))
		{
			lock (list)
			{
				return Task.FromResult(list.Where(t => t.Status == TimeoutStatus.Pending).ToList());
			}
		}

		return Task.FromResult(new List<TimeoutEntry>());
	}

	/// <inheritdoc/>
	public Task<List<TimeoutEntry>> GetAllDueAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;
		var due = new List<TimeoutEntry>();
		foreach (var kvp in _timeouts)
		{
			lock (kvp.Value)
			{
				due.AddRange(kvp.Value.Where(t =>
					t.Status == TimeoutStatus.Pending && t.DueAt <= now));
			}
		}

		return Task.FromResult(due);
	}

	/// <inheritdoc/>
	public Task UpdateStatusAsync(string sagaId, string timeoutId, TimeoutStatus status, CancellationToken cancellationToken)
	{
		if (_timeouts.TryGetValue(sagaId, out var list))
		{
			lock (list)
			{
				var entry = list.FirstOrDefault(t => t.Id == timeoutId);
				if (entry != null)
				{
					entry.Status = status;
				}
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task CancelAllForSagaAsync(string sagaId, CancellationToken cancellationToken)
	{
		if (_timeouts.TryGetValue(sagaId, out var list))
		{
			lock (list)
			{
				foreach (var entry in list.Where(t => t.Status == TimeoutStatus.Pending))
				{
					entry.Status = TimeoutStatus.Cancelled;
				}
			}
		}

		return Task.CompletedTask;
	}
}
