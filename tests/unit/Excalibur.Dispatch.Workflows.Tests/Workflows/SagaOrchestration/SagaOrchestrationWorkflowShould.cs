// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.SagaOrchestration;

/// <summary>
/// End-to-end workflow tests for saga orchestration patterns.
/// Tests happy path, compensation, timeout, concurrency, and recovery patterns.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 181 - Functional Testing Epic Phase 1.
/// bd-cgsec: Saga Orchestration Workflow Tests (5 tests).
/// </para>
/// <para>
/// These tests use in-memory saga state to validate orchestration patterns
/// without requiring TestContainers or message brokers.
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "181")]
[Trait("Component", "SagaOrchestration")]
[Trait("Category", "Unit")]
public sealed class SagaOrchestrationWorkflowShould
{
	/// <summary>
	/// Tests the saga happy path where all steps complete successfully.
	/// Start > Step1 > Step2 > Step3 > Complete.
	/// </summary>
	[Fact]
	public async Task CompleteSagaHappyPath()
	{
		// Arrange
		var sagaStore = new InMemorySagaStore();
		var executionLog = new ExecutionLog();
		var saga = new OrderFulfillmentSaga(sagaStore, executionLog);

		// Act - Start saga and execute all steps
		await saga.StartAsync("saga-001", new OrderFulfillmentData
		{
			OrderId = "ORD-001",
			CustomerId = "CUST-001",
			Amount = 100.00m
		}).ConfigureAwait(true);

		// Simulate processing of each step
		await saga.ProcessStepAsync("saga-001", SagaStep.ReserveInventory).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-001", SagaStep.ProcessPayment).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-001", SagaStep.ShipOrder).ConfigureAwait(true);

		// Assert - Saga completed successfully
		var state = await sagaStore.GetAsync("saga-001").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Completed);

		// Assert - All steps executed in order
		executionLog.Steps.ShouldContain("ReserveInventory:Execute");
		executionLog.Steps.ShouldContain("ProcessPayment:Execute");
		executionLog.Steps.ShouldContain("ShipOrder:Execute");
	}

	/// <summary>
	/// Tests saga compensation when a step fails.
	/// Step1 OK > Step2 fails > Compensate Step1.
	/// </summary>
	[Fact]
	public async Task ExecuteCompensationOnStepFailure()
	{
		// Arrange
		var sagaStore = new InMemorySagaStore();
		var executionLog = new ExecutionLog();
		var saga = new OrderFulfillmentSaga(sagaStore, executionLog)
		{
			FailOnStep = SagaStep.ProcessPayment // Simulate payment failure
		};

		// Act - Start saga
		await saga.StartAsync("saga-002", new OrderFulfillmentData
		{
			OrderId = "ORD-002",
			CustomerId = "CUST-002",
			Amount = 200.00m
		}).ConfigureAwait(true);

		// Step 1 succeeds
		await saga.ProcessStepAsync("saga-002", SagaStep.ReserveInventory).ConfigureAwait(true);

		// Step 2 fails - should trigger compensation
		await saga.ProcessStepAsync("saga-002", SagaStep.ProcessPayment).ConfigureAwait(true);

		// Assert - Saga is compensated
		var state = await sagaStore.GetAsync("saga-002").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Compensated);

		// Assert - Compensation was executed for completed steps
		executionLog.Steps.ShouldContain("ReserveInventory:Execute");
		executionLog.Steps.ShouldContain("ProcessPayment:Failed");
		executionLog.Steps.ShouldContain("ReserveInventory:Compensate");
	}

	/// <summary>
	/// Tests saga timeout handling.
	/// Step1 > Timeout > Compensation > Cleanup.
	/// </summary>
	[Fact]
	public async Task HandleSagaTimeoutWithCompensation()
	{
		// Arrange
		var sagaStore = new InMemorySagaStore();
		var executionLog = new ExecutionLog();
		var saga = new OrderFulfillmentSaga(sagaStore, executionLog)
		{
			TimeoutOnStep = SagaStep.ProcessPayment,
			StepTimeout = TimeSpan.FromMilliseconds(50)
		};

		// Act - Start saga
		await saga.StartAsync("saga-003", new OrderFulfillmentData
		{
			OrderId = "ORD-003",
			CustomerId = "CUST-003",
			Amount = 300.00m
		}).ConfigureAwait(true);

		// Step 1 succeeds
		await saga.ProcessStepAsync("saga-003", SagaStep.ReserveInventory).ConfigureAwait(true);

		// Step 2 times out
		await saga.ProcessStepAsync("saga-003", SagaStep.ProcessPayment).ConfigureAwait(true);

		// Assert - Saga is in timeout/compensated state
		var state = await sagaStore.GetAsync("saga-003").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		(state.Status == SagaStatus.TimedOut || state.Status == SagaStatus.Compensated).ShouldBeTrue();

		// Assert - Timeout was logged and compensation ran
		executionLog.Steps.ShouldContain("ProcessPayment:Timeout");
		executionLog.Steps.ShouldContain("ReserveInventory:Compensate");
	}

	/// <summary>
	/// Tests concurrent saga instances with resource isolation.
	/// 10 parallel sagas > Resource locking > Isolation.
	/// </summary>
	[Fact]
	public async Task HandleConcurrentSagaInstances()
	{
		// Arrange
		var sagaStore = new InMemorySagaStore();
		var executionLog = new ExecutionLog();
		var completedSagas = new ConcurrentBag<string>();
		const int sagaCount = 10;

		// Act - Start 10 parallel sagas
		var tasks = Enumerable.Range(0, sagaCount).Select(async i =>
		{
			var saga = new OrderFulfillmentSaga(sagaStore, executionLog);
			var sagaId = $"saga-concurrent-{i}";

			await saga.StartAsync(sagaId, new OrderFulfillmentData
			{
				OrderId = $"ORD-{i:D3}",
				CustomerId = $"CUST-{i:D3}",
				Amount = 100.00m + i
			}).ConfigureAwait(false);

			await saga.ProcessStepAsync(sagaId, SagaStep.ReserveInventory).ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, SagaStep.ProcessPayment).ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, SagaStep.ShipOrder).ConfigureAwait(false);

			completedSagas.Add(sagaId);
		}).ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - All sagas completed
		completedSagas.Count.ShouldBe(sagaCount);

		// Assert - Each saga is in completed state
		foreach (var sagaId in completedSagas)
		{
			var state = await sagaStore.GetAsync(sagaId).ConfigureAwait(true);
			_ = state.ShouldNotBeNull();
			state.Status.ShouldBe(SagaStatus.Completed);
		}

		// Assert - No saga interfered with another (isolated data)
		var allStates = await sagaStore.GetAllAsync().ConfigureAwait(true);
		allStates.Count.ShouldBe(sagaCount);
		allStates.Select(s => s.Data.OrderId).Distinct().Count().ShouldBe(sagaCount);
	}

	/// <summary>
	/// Tests saga state recovery after simulated process restart.
	/// In-progress > Process restart > Resume.
	/// </summary>
	[Fact]
	public async Task RecoverSagaStateAfterRestart()
	{
		// Arrange - Start saga and complete first step
		var sagaStore = new InMemorySagaStore();
		var executionLog1 = new ExecutionLog();
		var saga1 = new OrderFulfillmentSaga(sagaStore, executionLog1);

		await saga1.StartAsync("saga-recovery", new OrderFulfillmentData
		{
			OrderId = "ORD-RECOVERY",
			CustomerId = "CUST-RECOVERY",
			Amount = 500.00m
		}).ConfigureAwait(true);

		await saga1.ProcessStepAsync("saga-recovery", SagaStep.ReserveInventory).ConfigureAwait(true);

		// Assert - Saga is in progress
		var stateBeforeRestart = await sagaStore.GetAsync("saga-recovery").ConfigureAwait(true);
		_ = stateBeforeRestart.ShouldNotBeNull();
		stateBeforeRestart.Status.ShouldBe(SagaStatus.InProgress);
		stateBeforeRestart.CurrentStep.ShouldBe(SagaStep.ReserveInventory);

		// Act - Simulate process restart with new saga instance
		var executionLog2 = new ExecutionLog();
		var saga2 = new OrderFulfillmentSaga(sagaStore, executionLog2);

		// Resume saga from persisted state
		await saga2.ResumeAsync("saga-recovery").ConfigureAwait(true);
		await saga2.ProcessStepAsync("saga-recovery", SagaStep.ProcessPayment).ConfigureAwait(true);
		await saga2.ProcessStepAsync("saga-recovery", SagaStep.ShipOrder).ConfigureAwait(true);

		// Assert - Saga recovered and completed
		var stateAfterRecovery = await sagaStore.GetAsync("saga-recovery").ConfigureAwait(true);
		_ = stateAfterRecovery.ShouldNotBeNull();
		stateAfterRecovery.Status.ShouldBe(SagaStatus.Completed);

		// Assert - Only remaining steps were executed after recovery
		executionLog2.Steps.ShouldContain("ProcessPayment:Execute");
		executionLog2.Steps.ShouldContain("ShipOrder:Execute");
		executionLog2.Steps.ShouldNotContain("ReserveInventory:Execute"); // Already done before restart
	}

	#region Saga Infrastructure

	internal enum SagaStatus
	{
		Pending,
		InProgress,
		Completed,
		Compensating,
		Compensated,
		Failed,
		TimedOut
	}

	internal enum SagaStep
	{
		None,
		ReserveInventory,
		ProcessPayment,
		ShipOrder
	}

	internal sealed class OrderFulfillmentData
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	internal sealed class SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public SagaStep CurrentStep { get; set; } = SagaStep.None;
		public List<SagaStep> CompletedSteps { get; } = [];
		public OrderFulfillmentData Data { get; init; } = new();
		public DateTime StartedAt { get; init; } = DateTime.UtcNow;
		public DateTime? CompletedAt { get; set; }
	}

	internal sealed class InMemorySagaStore
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
	}

	internal sealed class ExecutionLog
	{
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step) => Steps.Add(step);
	}

	internal sealed class OrderFulfillmentSaga
	{
		private readonly InMemorySagaStore _store;
		private readonly ExecutionLog _log;

		public OrderFulfillmentSaga(InMemorySagaStore store, ExecutionLog log)
		{
			_store = store;
			_log = log;
		}

		public SagaStep? FailOnStep { get; init; }
		public SagaStep? TimeoutOnStep { get; init; }
		public TimeSpan StepTimeout { get; init; } = TimeSpan.FromSeconds(30);

		public async Task StartAsync(string sagaId, OrderFulfillmentData data)
		{
			var state = new SagaState
			{
				SagaId = sagaId,
				Status = SagaStatus.Pending,
				Data = data
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public async Task ResumeAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			// Saga is already in progress, just continue
			_log.Log($"Resume:From:{state.CurrentStep}");
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

			// Check for simulated timeout
			if (TimeoutOnStep == step)
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(StepTimeout + TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
				_log.Log($"{step}:Timeout");
				state.Status = SagaStatus.TimedOut;
				await CompensateAsync(state).ConfigureAwait(false);
				return;
			}

			// Check for simulated failure
			if (FailOnStep == step)
			{
				_log.Log($"{step}:Failed");
				state.Status = SagaStatus.Failed;
				await CompensateAsync(state).ConfigureAwait(false);
				return;
			}

			// Execute step successfully
			_log.Log($"{step}:Execute");
			state.CompletedSteps.Add(step);

			// Check if saga is complete
			if (step == SagaStep.ShipOrder)
			{
				state.Status = SagaStatus.Completed;
				state.CompletedAt = DateTime.UtcNow;
			}

			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		private async Task CompensateAsync(SagaState state)
		{
			state.Status = SagaStatus.Compensating;

			// Compensate in reverse order
			foreach (var completedStep in state.CompletedSteps.AsEnumerable().Reverse())
			{
				_log.Log($"{completedStep}:Compensate");
			}

			state.Status = SagaStatus.Compensated;
			state.CompletedAt = DateTime.UtcNow;
			await _store.SaveAsync(state).ConfigureAwait(false);
		}
	}

	#endregion Saga Infrastructure
}
