// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.SagaAdvanced;

/// <summary>
/// Long-Running Saga workflow tests.
/// Tests timeout, crash recovery, state persistence, retry, and dead letter patterns.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 183 - Functional Testing Epic Phase 3.
/// bd-2vp3b: Long-Running Saga Tests (5 tests).
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "183")]
[Trait("Component", "SagaAdvanced")]
[Trait("Category", "Unit")]
public sealed class LongRunningSagaWorkflowShould
{
	/// <summary>
	/// Tests that a saga exceeding its configured timeout triggers compensation.
	/// Saga timeout > Compensation runs for completed steps.
	/// </summary>
	[Fact]
	public async Task TriggerCompensationOnSagaTimeout()
	{
		// Arrange
		var store = new EnhancedSagaStore();
		var log = new ExecutionLog();
		var saga = new AdvancedOrderSaga(store, log)
		{
			TimeoutOnStep = SagaStep.ProcessPayment,
			StepTimeout = TimeSpan.FromMilliseconds(50),
		};

		// Act - Start saga and process steps
		await saga.StartAsync("saga-timeout", new OrderData { OrderId = "ORD-TIMEOUT" }).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-timeout", SagaStep.ReserveInventory).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-timeout", SagaStep.ProcessPayment).ConfigureAwait(true); // Will timeout

		// Assert - Saga timed out and compensation ran
		var state = await store.GetAsync("saga-timeout").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Compensated); // Final status after timeout + compensation

		log.Steps.ShouldContain("ReserveInventory:Execute");
		log.Steps.ShouldContain("ProcessPayment:Timeout");
		log.Steps.ShouldContain("ReserveInventory:Compensate");
	}

	/// <summary>
	/// Tests that a saga can resume from persisted state after a simulated crash.
	/// Process crash > Saga resumes from correct step position.
	/// </summary>
	[Fact]
	public async Task ResumeSagaAfterProcessCrash()
	{
		// Arrange - Start saga and complete some steps
		var store = new EnhancedSagaStore();
		var log1 = new ExecutionLog();
		var saga1 = new AdvancedOrderSaga(store, log1);

		await saga1.StartAsync("saga-crash", new OrderData { OrderId = "ORD-CRASH" }).ConfigureAwait(true);
		await saga1.ProcessStepAsync("saga-crash", SagaStep.ReserveInventory).ConfigureAwait(true);
		await saga1.ProcessStepAsync("saga-crash", SagaStep.ProcessPayment).ConfigureAwait(true);

		// Simulate crash - store state is persisted
		var stateBeforeCrash = await store.GetAsync("saga-crash").ConfigureAwait(true);
		_ = stateBeforeCrash.ShouldNotBeNull();
		stateBeforeCrash.Status.ShouldBe(SagaStatus.InProgress);
		stateBeforeCrash.CompletedSteps.Count.ShouldBe(2);

		// Act - New saga instance resumes from persisted state
		var log2 = new ExecutionLog();
		var saga2 = new AdvancedOrderSaga(store, log2);
		await saga2.ResumeAsync("saga-crash").ConfigureAwait(true);
		await saga2.ProcessStepAsync("saga-crash", SagaStep.ShipOrder).ConfigureAwait(true);

		// Assert - Saga completed from resume point
		var stateAfterResume = await store.GetAsync("saga-crash").ConfigureAwait(true);
		_ = stateAfterResume.ShouldNotBeNull();
		stateAfterResume.Status.ShouldBe(SagaStatus.Completed);

		// Only the remaining step was executed
		log2.Steps.ShouldContain("Resume:FromStep:ProcessPayment");
		log2.Steps.ShouldContain("ShipOrder:Execute");
		log2.Steps.ShouldNotContain("ReserveInventory:Execute");
		log2.Steps.ShouldNotContain("ProcessPayment:Execute");
	}

	/// <summary>
	/// Tests that saga state survives restart with correct position and data.
	/// State persisted > Restart > Data intact.
	/// </summary>
	[Fact]
	public async Task PersistSagaStateAcrossRestart()
	{
		// Arrange
		var store = new EnhancedSagaStore();
		var log = new ExecutionLog();
		var saga = new AdvancedOrderSaga(store, log);

		var orderData = new OrderData
		{
			OrderId = "ORD-PERSIST",
			CustomerId = "CUST-PERSIST",
			Amount = 999.99m,
			Notes = "Important order",
		};

		// Act - Start and partially complete
		await saga.StartAsync("saga-persist", orderData).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-persist", SagaStep.ReserveInventory).ConfigureAwait(true);

		// Simulate restart - get state from store
		var persistedState = await store.GetAsync("saga-persist").ConfigureAwait(true);

		// Assert - State was persisted correctly
		_ = persistedState.ShouldNotBeNull();
		persistedState.SagaId.ShouldBe("saga-persist");
		persistedState.Status.ShouldBe(SagaStatus.InProgress);
		persistedState.CurrentStep.ShouldBe(SagaStep.ReserveInventory);
		persistedState.CompletedSteps.ShouldContain(SagaStep.ReserveInventory);

		// Data was preserved
		_ = persistedState.Data.ShouldNotBeNull();
		persistedState.Data.OrderId.ShouldBe("ORD-PERSIST");
		persistedState.Data.CustomerId.ShouldBe("CUST-PERSIST");
		persistedState.Data.Amount.ShouldBe(999.99m);
		persistedState.Data.Notes.ShouldBe("Important order");

		// Step history was recorded
		persistedState.StepHistory.Count.ShouldBe(1);
		persistedState.StepHistory[0].Step.ShouldBe(SagaStep.ReserveInventory);
		persistedState.StepHistory[0].Status.ShouldBe(StepStatus.Completed);
	}

	/// <summary>
	/// Tests that transient step failures trigger retry with eventual success.
	/// Transient failure > Retry policy > Eventual success.
	/// </summary>
	[Fact]
	public async Task RetryTransientStepFailures()
	{
		// Arrange
		var store = new EnhancedSagaStore();
		var log = new ExecutionLog();
		var saga = new AdvancedOrderSaga(store, log)
		{
			TransientFailCount = 2, // Fail first 2 attempts, succeed on 3rd
			FailOnStep = SagaStep.ProcessPayment,
			MaxRetries = 3,
			RetryDelayMs = 10,
		};

		// Act
		await saga.StartAsync("saga-retry", new OrderData { OrderId = "ORD-RETRY" }).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-retry", SagaStep.ReserveInventory).ConfigureAwait(true);
		await saga.ProcessStepWithRetryAsync("saga-retry", SagaStep.ProcessPayment).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-retry", SagaStep.ShipOrder).ConfigureAwait(true);

		// Assert - Saga completed after retries
		var state = await store.GetAsync("saga-retry").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Completed);

		// Assert - Retry attempts were logged
		var attempts = log.Steps.Count(s => s.StartsWith("ProcessPayment:Attempt:"));
		attempts.ShouldBe(3); // 2 failures + 1 success

		log.Steps.ShouldContain("ProcessPayment:Attempt:1:Failed");
		log.Steps.ShouldContain("ProcessPayment:Attempt:2:Failed");
		log.Steps.ShouldContain("ProcessPayment:Attempt:3:Success");
	}

	/// <summary>
	/// Tests that permanent step failures move the saga to dead letter after retries exhausted.
	/// All retries exhausted > Dead letter queue.
	/// </summary>
	[Fact]
	public async Task MoveToDeadLetterAfterMaxRetries()
	{
		// Arrange
		var store = new EnhancedSagaStore();
		var log = new ExecutionLog();
		var deadLetter = new DeadLetterQueue();
		var saga = new AdvancedOrderSaga(store, log, deadLetter)
		{
			TransientFailCount = int.MaxValue, // Always fail
			FailOnStep = SagaStep.ProcessPayment,
			MaxRetries = 3,
			RetryDelayMs = 10,
		};

		// Act
		await saga.StartAsync("saga-dlq", new OrderData { OrderId = "ORD-DLQ" }).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-dlq", SagaStep.ReserveInventory).ConfigureAwait(true);
		await saga.ProcessStepWithRetryAsync("saga-dlq", SagaStep.ProcessPayment).ConfigureAwait(true);

		// Assert - Saga moved to dead letter
		var state = await store.GetAsync("saga-dlq").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.DeadLettered);

		// Assert - Dead letter queue received the saga
		deadLetter.Items.Count.ShouldBe(1);
		var dlqItem = deadLetter.Items[0];
		dlqItem.SagaId.ShouldBe("saga-dlq");
		dlqItem.FailedStep.ShouldBe(SagaStep.ProcessPayment);
		dlqItem.AttemptCount.ShouldBe(3);
		dlqItem.Reason.ShouldContain("Max retries exhausted");

		// Assert - Compensation ran for completed steps
		log.Steps.ShouldContain("ReserveInventory:Compensate");
	}

	#region Test Infrastructure

	internal enum SagaStatus
	{
		Pending,
		InProgress,
		Completed,
		Compensating,
		Compensated,
		Failed,
		TimedOut,
		DeadLettered,
	}

	internal enum SagaStep
	{
		None,
		ReserveInventory,
		ProcessPayment,
		ShipOrder,
	}

	internal enum StepStatus
	{
		Pending,
		InProgress,
		Completed,
		Failed,
		Compensated,
	}

	internal sealed class ExecutionLog
	{
		private readonly ConcurrentQueue<string> _orderedSteps = new();
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step)
		{
			Steps.Add(step);
			_orderedSteps.Enqueue(step);
		}

		public List<string> GetOrderedSteps() => [.. _orderedSteps];
	}

	internal sealed class OrderData
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public string Notes { get; init; } = string.Empty;
	}

	internal sealed class StepHistoryEntry
	{
		public SagaStep Step { get; init; }
		public StepStatus Status { get; init; }
		public DateTimeOffset StartedAt { get; init; }
		public DateTimeOffset? CompletedAt { get; init; }
	}

	internal sealed class SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public string Version { get; set; } = "1.0";
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public SagaStep CurrentStep { get; set; } = SagaStep.None;
		public List<SagaStep> CompletedSteps { get; } = [];
		public List<StepHistoryEntry> StepHistory { get; } = [];
		public OrderData Data { get; init; } = new();
		public Dictionary<string, object> Metadata { get; } = [];
		public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
		public DateTimeOffset? CompletedAt { get; set; }
	}

	internal sealed class EnhancedSagaStore
	{
		private readonly ConcurrentDictionary<string, SagaState> _sagas = new();

		public Task SaveAsync(SagaState state)
		{
			_sagas[state.SagaId] = state;
			return Task.CompletedTask;
		}

		public Task<SagaState?> GetAsync(string sagaId)
		{
			_ = _sagas.TryGetValue(sagaId, out var state);
			return Task.FromResult(state);
		}

		public Task<List<SagaState>> GetAllAsync()
		{
			return Task.FromResult(_sagas.Values.ToList());
		}

		public Task<List<SagaState>> GetByStatusAsync(SagaStatus status)
		{
			return Task.FromResult(_sagas.Values.Where(s => s.Status == status).ToList());
		}
	}

	internal sealed class DeadLetterItem
	{
		public string SagaId { get; init; } = string.Empty;
		public SagaStep FailedStep { get; init; }
		public int AttemptCount { get; init; }
		public string Reason { get; init; } = string.Empty;
		public DateTimeOffset EnqueuedAt { get; init; }
	}

	internal sealed class DeadLetterQueue
	{
		public List<DeadLetterItem> Items { get; } = [];

		public void Enqueue(string sagaId, SagaStep failedStep, int attempts, string reason)
		{
			Items.Add(new DeadLetterItem
			{
				SagaId = sagaId,
				FailedStep = failedStep,
				AttemptCount = attempts,
				Reason = reason,
				EnqueuedAt = DateTimeOffset.UtcNow,
			});
		}
	}

	internal sealed class AdvancedOrderSaga
	{
		private readonly EnhancedSagaStore _store;
		private readonly ExecutionLog _log;
		private readonly DeadLetterQueue? _deadLetter;

		private int _currentAttempts;

		public AdvancedOrderSaga(EnhancedSagaStore store, ExecutionLog log, DeadLetterQueue? deadLetter = null)
		{
			_store = store;
			_log = log;
			_deadLetter = deadLetter;
		}

		public SagaStep? FailOnStep { get; init; }
		public SagaStep? TimeoutOnStep { get; init; }
		public TimeSpan StepTimeout { get; init; } = TimeSpan.FromSeconds(30);
		public int TransientFailCount { get; init; }
		public int MaxRetries { get; init; } = 3;
		public int RetryDelayMs { get; init; } = 100;

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new SagaState
			{
				SagaId = sagaId,
				Status = SagaStatus.Pending,
				Data = data,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}");
		}

		public async Task ResumeAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			_log.Log($"Resume:FromStep:{state.CurrentStep}");
		}

		public async Task ProcessStepAsync(string sagaId, SagaStep step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			state.CurrentStep = step;

			// Check for timeout
			if (TimeoutOnStep == step)
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(StepTimeout + TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
				_log.Log($"{step}:Timeout");
				state.Status = SagaStatus.TimedOut;
				await CompensateAsync(state).ConfigureAwait(false);
				return;
			}

			// Execute step
			_log.Log($"{step}:Execute");
			state.CompletedSteps.Add(step);
			state.StepHistory.Add(new StepHistoryEntry
			{
				Step = step,
				Status = StepStatus.Completed,
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
			});

			// Check if complete
			if (step == SagaStep.ShipOrder)
			{
				state.Status = SagaStatus.Completed;
				state.CompletedAt = DateTimeOffset.UtcNow;
			}

			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public async Task ProcessStepWithRetryAsync(string sagaId, SagaStep step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			state.CurrentStep = step;
			_currentAttempts = 0;

			while (_currentAttempts < MaxRetries)
			{
				_currentAttempts++;

				if (FailOnStep == step && _currentAttempts <= TransientFailCount)
				{
					_log.Log($"{step}:Attempt:{_currentAttempts}:Failed");
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(RetryDelayMs).ConfigureAwait(false);
					continue;
				}

				// Success
				_log.Log($"{step}:Attempt:{_currentAttempts}:Success");
				state.CompletedSteps.Add(step);
				state.StepHistory.Add(new StepHistoryEntry
				{
					Step = step,
					Status = StepStatus.Completed,
					StartedAt = DateTimeOffset.UtcNow,
					CompletedAt = DateTimeOffset.UtcNow,
				});
				await _store.SaveAsync(state).ConfigureAwait(false);
				return;
			}

			// Max retries exhausted - dead letter
			_log.Log($"{step}:MaxRetriesExhausted");
			state.Status = SagaStatus.DeadLettered;
			await CompensateAsync(state).ConfigureAwait(false);

			_deadLetter?.Enqueue(sagaId, step, _currentAttempts, "Max retries exhausted");
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		private async Task CompensateAsync(SagaState state)
		{
			state.Status = state.Status == SagaStatus.DeadLettered ? SagaStatus.DeadLettered : SagaStatus.Compensating;

			foreach (var completedStep in state.CompletedSteps.AsEnumerable().Reverse())
			{
				_log.Log($"{completedStep}:Compensate");
			}

			if (state.Status != SagaStatus.DeadLettered)
			{
				state.Status = SagaStatus.Compensated;
			}

			state.CompletedAt = DateTimeOffset.UtcNow;
			await _store.SaveAsync(state).ConfigureAwait(false);
		}
	}

	#endregion Test Infrastructure
}
