// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.SagaMonitoring;

/// <summary>
/// Functional tests for saga monitoring workflows.
/// Tests status queries, stuck detection, active saga listing, failed saga queries, and dashboard aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 197 - Saga Orchestration Advanced Tests.
/// bd-wxjcb: Saga Monitoring Tests (5 tests).
/// </para>
/// <para>
/// These tests verify that saga monitoring and observability patterns work correctly.
/// </para>
/// </remarks>
[FunctionalTest]
public sealed class SagaMonitoringWorkflowTests : FunctionalTestBase
{
	/// <inheritdoc/>
	protected override TimeSpan TestTimeout => TestTimeouts.Functional;

	/// <summary>
	/// Test 1: Verifies that saga status can be queried by ID.
	/// </summary>
	[Fact]
	public async Task Saga_Status_Can_Be_Queried_By_Id()
	{
		// Arrange
		var store = new MonitorableSagaStore();
		var orchestrator = new SagaOrchestrator(store);
		var sagaId = "saga-mon-001";

		// Act
		var statusHistory = await RunWithTimeoutAsync(async _ =>
		{
			var history = new List<SagaStatus>();

			// Start saga
			await orchestrator.StartSagaAsync(sagaId, "Order").ConfigureAwait(true);
			history.Add(await orchestrator.GetSagaStatusAsync(sagaId).ConfigureAwait(true));

			// Execute step
			await orchestrator.ExecuteStepAsync(sagaId, "Step1").ConfigureAwait(true);
			history.Add(await orchestrator.GetSagaStatusAsync(sagaId).ConfigureAwait(true));

			// Complete saga
			await orchestrator.CompleteSagaAsync(sagaId).ConfigureAwait(true);
			history.Add(await orchestrator.GetSagaStatusAsync(sagaId).ConfigureAwait(true));

			return history;
		}).ConfigureAwait(true);

		// Assert - Status transitions correctly
		statusHistory[0].ShouldBe(SagaStatus.Running);
		statusHistory[1].ShouldBe(SagaStatus.Running);
		statusHistory[2].ShouldBe(SagaStatus.Completed);
	}

	/// <summary>
	/// Test 2: Verifies that stuck sagas are detected based on inactivity threshold.
	/// </summary>
	[Fact]
	public async Task Saga_Stuck_Sagas_Are_Detected()
	{
		// Arrange
		var store = new MonitorableSagaStore();
		var orchestrator = new SagaOrchestrator(store);
		var monitor = new SagaMonitor(store);
		var stuckThreshold = TimeSpan.FromMilliseconds(100);

		// Act
		var stuckSagas = await RunWithTimeoutAsync(async _ =>
		{
			// Start saga and execute one step
			await orchestrator.StartSagaAsync("saga-stuck-001", "Order").ConfigureAwait(true);
			await orchestrator.ExecuteStepAsync("saga-stuck-001", "Step1").ConfigureAwait(true);

			// Simulate time passing by backdating the last update
			store.BackdateLastUpdate("saga-stuck-001", TimeSpan.FromMilliseconds(150));

			// Query for stuck sagas
			return await monitor.GetStuckSagasAsync(stuckThreshold).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Saga is flagged as stuck
		stuckSagas.ShouldContain(s => s.SagaId == "saga-stuck-001");
		stuckSagas.First().IsStuck.ShouldBeTrue();
	}

	/// <summary>
	/// Test 3: Verifies that active (running) sagas can be listed.
	/// </summary>
	[Fact]
	public async Task Saga_Active_Sagas_Can_Be_Listed()
	{
		// Arrange
		var store = new MonitorableSagaStore();
		var orchestrator = new SagaOrchestrator(store);

		// Act
		var activeSagas = await RunWithTimeoutAsync(async _ =>
		{
			// Start 5 sagas
			for (var i = 0; i < 5; i++)
			{
				await orchestrator.StartSagaAsync($"saga-active-{i:D3}", "Order").ConfigureAwait(true);
			}

			// Complete 2 of them
			await orchestrator.CompleteSagaAsync("saga-active-001").ConfigureAwait(true);
			await orchestrator.CompleteSagaAsync("saga-active-003").ConfigureAwait(true);

			// Query active sagas
			return await store.GetByStatusAsync(SagaStatus.Running).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - 3 sagas still running
		activeSagas.Count.ShouldBe(3);
		activeSagas.ShouldContain(s => s.SagaId == "saga-active-000");
		activeSagas.ShouldContain(s => s.SagaId == "saga-active-002");
		activeSagas.ShouldContain(s => s.SagaId == "saga-active-004");
	}

	/// <summary>
	/// Test 4: Verifies that failed sagas can be queried.
	/// </summary>
	[Fact]
	public async Task Saga_Failed_Sagas_Can_Be_Queried()
	{
		// Arrange
		var store = new MonitorableSagaStore();
		var orchestrator = new SagaOrchestrator(store);

		// Act
		var failedSagas = await RunWithTimeoutAsync(async _ =>
		{
			// Start 5 sagas
			for (var i = 0; i < 5; i++)
			{
				await orchestrator.StartSagaAsync($"saga-fail-{i:D3}", "Order").ConfigureAwait(true);
			}

			// Fail 3 of them
			await orchestrator.FailSagaAsync("saga-fail-000", "Payment declined").ConfigureAwait(true);
			await orchestrator.FailSagaAsync("saga-fail-002", "Inventory unavailable").ConfigureAwait(true);
			await orchestrator.FailSagaAsync("saga-fail-004", "Shipping error").ConfigureAwait(true);

			// Query failed sagas
			return await store.GetByStatusAsync(SagaStatus.Failed).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - 3 sagas failed
		failedSagas.Count.ShouldBe(3);
		failedSagas.ShouldContain(s => s.FailureReason == "Payment declined");
		failedSagas.ShouldContain(s => s.FailureReason == "Inventory unavailable");
		failedSagas.ShouldContain(s => s.FailureReason == "Shipping error");
	}

	/// <summary>
	/// Test 5: Verifies that dashboard aggregates are available.
	/// </summary>
	[Fact]
	public async Task Saga_Dashboard_Aggregates_Are_Available()
	{
		// Arrange
		var store = new MonitorableSagaStore();
		var orchestrator = new SagaOrchestrator(store);
		var monitor = new SagaMonitor(store);

		// Act
		var dashboard = await RunWithTimeoutAsync(async _ =>
		{
			// Create sagas with various states
			// 5 running
			for (var i = 0; i < 5; i++)
			{
				await orchestrator.StartSagaAsync($"saga-dash-running-{i}", "Order").ConfigureAwait(true);
			}

			// 3 completed
			for (var i = 0; i < 3; i++)
			{
				await orchestrator.StartSagaAsync($"saga-dash-completed-{i}", "Order").ConfigureAwait(true);
				await orchestrator.CompleteSagaAsync($"saga-dash-completed-{i}").ConfigureAwait(true);
			}

			// 2 failed
			for (var i = 0; i < 2; i++)
			{
				await orchestrator.StartSagaAsync($"saga-dash-failed-{i}", "Order").ConfigureAwait(true);
				await orchestrator.FailSagaAsync($"saga-dash-failed-{i}", "Error").ConfigureAwait(true);
			}

			// Get dashboard aggregates
			return await monitor.GetDashboardAsync().ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Aggregates are correct
		dashboard.TotalSagas.ShouldBe(10);
		dashboard.RunningCount.ShouldBe(5);
		dashboard.CompletedCount.ShouldBe(3);
		dashboard.FailedCount.ShouldBe(2);
		dashboard.SuccessRate.ShouldBe(0.6m); // 3 completed / (3 completed + 2 failed)
	}

	#region Test Infrastructure

	/// <summary>
	/// Saga status.
	/// </summary>
	public enum SagaStatus
	{
		Created,
		Running,
		Completed,
		Failed,
		Compensating,
		Compensated,
	}

	/// <summary>
	/// Saga state for monitoring.
	/// </summary>
	public sealed class SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public string SagaType { get; init; } = string.Empty;
		public SagaStatus Status { get; set; } = SagaStatus.Created;
		public string? FailureReason { get; set; }
		public List<string> CompletedSteps { get; } = new();
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
		public DateTimeOffset? CompletedAt { get; set; }
		public bool IsStuck { get; set; }
	}

	/// <summary>
	/// Dashboard aggregates.
	/// </summary>
	public sealed class DashboardAggregates
	{
		public int TotalSagas { get; init; }
		public int RunningCount { get; init; }
		public int CompletedCount { get; init; }
		public int FailedCount { get; init; }
		public int CompensatingCount { get; init; }
		public decimal SuccessRate { get; init; }
	}

	/// <summary>
	/// Monitorable saga store.
	/// </summary>
	public sealed class MonitorableSagaStore
	{
		private readonly ConcurrentDictionary<string, SagaState> _sagas = new();

		public Task SaveAsync(SagaState state)
		{
			state.LastUpdatedAt = DateTimeOffset.UtcNow;
			_sagas[state.SagaId] = state;
			return Task.CompletedTask;
		}

		public Task<SagaState?> GetAsync(string sagaId)
		{
			_ = _sagas.TryGetValue(sagaId, out var state);
			return Task.FromResult(state);
		}

		public Task<List<SagaState>> GetByStatusAsync(SagaStatus status)
		{
			var matching = _sagas.Values.Where(s => s.Status == status).ToList();
			return Task.FromResult(matching);
		}

		public Task<List<SagaState>> GetAllAsync()
		{
			return Task.FromResult(_sagas.Values.ToList());
		}

		public void BackdateLastUpdate(string sagaId, TimeSpan age)
		{
			if (_sagas.TryGetValue(sagaId, out var state))
			{
				state.LastUpdatedAt = DateTimeOffset.UtcNow - age;
			}
		}
	}

	/// <summary>
	/// Saga orchestrator for testing.
	/// </summary>
	public sealed class SagaOrchestrator
	{
		private readonly MonitorableSagaStore _store;

		public SagaOrchestrator(MonitorableSagaStore store)
		{
			_store = store;
		}

		public async Task StartSagaAsync(string sagaId, string sagaType)
		{
			var state = new SagaState
			{
				SagaId = sagaId,
				SagaType = sagaType,
				Status = SagaStatus.Running,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public async Task<SagaStatus> GetSagaStatusAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			return state?.Status ?? SagaStatus.Created;
		}

		public async Task ExecuteStepAsync(string sagaId, string step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state != null)
			{
				state.CompletedSteps.Add(step);
				await _store.SaveAsync(state).ConfigureAwait(false);
			}
		}

		public async Task CompleteSagaAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state != null)
			{
				state.Status = SagaStatus.Completed;
				state.CompletedAt = DateTimeOffset.UtcNow;
				await _store.SaveAsync(state).ConfigureAwait(false);
			}
		}

		public async Task FailSagaAsync(string sagaId, string reason)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state != null)
			{
				state.Status = SagaStatus.Failed;
				state.FailureReason = reason;
				state.CompletedAt = DateTimeOffset.UtcNow;
				await _store.SaveAsync(state).ConfigureAwait(false);
			}
		}

		public async Task CancelSagaAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state != null)
			{
				state.Status = SagaStatus.Compensating;
				await _store.SaveAsync(state).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Saga monitor for observability.
	/// </summary>
	public sealed class SagaMonitor
	{
		private readonly MonitorableSagaStore _store;

		public SagaMonitor(MonitorableSagaStore store)
		{
			_store = store;
		}

		public async Task<List<SagaState>> GetStuckSagasAsync(TimeSpan threshold)
		{
			var all = await _store.GetAllAsync().ConfigureAwait(false);
			var now = DateTimeOffset.UtcNow;

			return [.. all
				.Where(s => s.Status == SagaStatus.Running &&
							(now - s.LastUpdatedAt) > threshold)
				.Select(s =>
				{
					s.IsStuck = true;
					return s;
				})];
		}

		public async Task<DashboardAggregates> GetDashboardAsync()
		{
			var all = await _store.GetAllAsync().ConfigureAwait(false);

			var running = all.Count(s => s.Status == SagaStatus.Running);
			var completed = all.Count(s => s.Status == SagaStatus.Completed);
			var failed = all.Count(s => s.Status == SagaStatus.Failed);
			var compensating = all.Count(s => s.Status == SagaStatus.Compensating ||
												s.Status == SagaStatus.Compensated);

			var finishedCount = completed + failed;
			var successRate = finishedCount > 0 ? (decimal)completed / finishedCount : 0m;

			return new DashboardAggregates
			{
				TotalSagas = all.Count,
				RunningCount = running,
				CompletedCount = completed,
				FailedCount = failed,
				CompensatingCount = compensating,
				SuccessRate = Math.Round(successRate, 2),
			};
		}
	}

	#endregion Test Infrastructure
}
