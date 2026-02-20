// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.SagaCompensation;

/// <summary>
/// Functional tests for saga compensation workflows.
/// Tests multi-step compensation, LIFO order, partial compensation, retry, and dead letter handling.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 197 - Saga Orchestration Advanced Tests.
/// bd-w500v: Saga Compensation Tests (5 tests).
/// </para>
/// <para>
/// These tests verify that saga compensation follows the correct patterns:
/// - Compensation executes in reverse (LIFO) order
/// - Partial failures are handled gracefully
/// - Transient failures trigger retry
/// - Exhausted retries route to dead letter queue
/// </para>
/// </remarks>
[FunctionalTest]
public sealed class SagaCompensationWorkflowTests : FunctionalTestBase
{
	/// <inheritdoc/>
	protected override TimeSpan TestTimeout => TestTimeouts.Functional;

	/// <summary>
	/// Test 1: Verifies that all completed steps are compensated when a later step fails.
	/// </summary>
	[Fact]
	public async Task Saga_Compensates_All_Steps_On_Failure()
	{
		// Arrange
		var log = new ExecutionLog();
		var saga = new CompensatingSaga(log)
		{
			FailOnStep = "Step4",
		};

		// Act - Execute 4 steps where step 4 fails
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync("saga-comp-001").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-001", "Step1").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-001", "Step2").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-001", "Step3").ConfigureAwait(true);

			// This step fails, triggering compensation
			await saga.ExecuteStepAsync("saga-comp-001", "Step4").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Steps 1-3 were executed
		var events = log.Events;
		events.ShouldContain("Step1:Execute");
		events.ShouldContain("Step2:Execute");
		events.ShouldContain("Step3:Execute");
		events.ShouldContain("Step4:Failed");

		// Assert - Steps 3-2-1 were compensated (reverse order)
		events.ShouldContain("Step3:Compensate");
		events.ShouldContain("Step2:Compensate");
		events.ShouldContain("Step1:Compensate");

		// Assert - Final state is compensated
		saga.GetStatus("saga-comp-001").ShouldBe(SagaStatus.Compensated);
	}

	/// <summary>
	/// Test 2: Verifies that compensation executes in reverse (LIFO) order.
	/// </summary>
	[Fact]
	public async Task Saga_Compensation_Order_Is_Reverse_Of_Execution()
	{
		// Arrange
		var log = new ExecutionLog();
		var saga = new CompensatingSaga(log)
		{
			FailOnStep = "StepC",
		};

		// Act - Execute A, B, then C fails
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync("saga-comp-002").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-002", "StepA").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-002", "StepB").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-002", "StepC").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Verify strict LIFO order
		var events = log.Events.ToList();

		// Find indices of compensation events
		var compAIndex = events.FindIndex(e => e == "StepA:Compensate");
		var compBIndex = events.FindIndex(e => e == "StepB:Compensate");

		// B should be compensated before A (reverse order)
		compBIndex.ShouldBeLessThan(compAIndex);
	}

	/// <summary>
	/// Test 3: Verifies partial compensation continues when a compensation step fails.
	/// </summary>
	[Fact]
	public async Task Saga_Partial_Compensation_When_Compensation_Fails()
	{
		// Arrange
		var log = new ExecutionLog();
		var dlq = new DeadLetterQueue();
		var saga = new CompensatingSaga(log, dlq)
		{
			FailOnStep = "Step3",
			FailCompensationFor = "Step2", // Compensation for Step2 will fail
		};

		// Act - Execute steps, then fail and attempt compensation
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync("saga-comp-003").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-003", "Step1").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-003", "Step2").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-003", "Step3").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Step 2 compensation failed but Step 1 still ran
		log.Events.ShouldContain("Step2:CompensationFailed");
		log.Events.ShouldContain("Step1:Compensate");

		// Assert - Failed compensation was logged to DLQ
		dlq.Items.ShouldContain(item =>
			item.SagaId == "saga-comp-003" && item.Step == "Step2");
	}

	/// <summary>
	/// Test 4: Verifies that transient compensation failures trigger retry.
	/// </summary>
	[Fact]
	public async Task Saga_Compensation_Retry_On_Transient_Failure()
	{
		// Arrange
		var log = new ExecutionLog();
		var saga = new CompensatingSaga(log)
		{
			FailOnStep = "Step2",
			TransientCompensationFailures = 2, // Fail twice, succeed on 3rd try
		};

		// Act
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync("saga-comp-004").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-004", "Step1").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-004", "Step2").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Compensation was retried and eventually succeeded
		var retryAttempts = log.Events.Count(e => e.Contains("Step1:CompensationRetry"));
		retryAttempts.ShouldBe(2); // Failed twice

		// Assert - Final compensation succeeded
		log.Events.ShouldContain("Step1:Compensate");
		saga.GetStatus("saga-comp-004").ShouldBe(SagaStatus.Compensated);
	}

	/// <summary>
	/// Test 5: Verifies that exhausted retry attempts route to dead letter queue.
	/// </summary>
	[Fact]
	public async Task Saga_Dead_Letter_When_Compensation_Exhausted()
	{
		// Arrange
		var log = new ExecutionLog();
		var dlq = new DeadLetterQueue();
		var saga = new CompensatingSaga(log, dlq)
		{
			FailOnStep = "Step2",
			PermanentCompensationFailure = "Step1", // Always fails, exhausts retries
			MaxRetries = 3,
		};

		// Act
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync("saga-comp-005").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-005", "Step1").ConfigureAwait(true);
			await saga.ExecuteStepAsync("saga-comp-005", "Step2").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Retries were attempted
		var retryCount = log.Events.Count(e => e.Contains("Step1:CompensationRetry"));
		retryCount.ShouldBe(3); // MaxRetries

		// Assert - After exhausting retries, routed to DLQ
		dlq.Items.ShouldContain(item =>
			item.SagaId == "saga-comp-005" &&
			item.Step == "Step1" &&
			item.Reason.Contains("MaxRetries"));

		// Assert - Saga is in partially compensated state
		saga.GetStatus("saga-comp-005").ShouldBe(SagaStatus.PartiallyCompensated);
	}

	#region Test Infrastructure

	/// <summary>
	/// Saga status.
	/// </summary>
	public enum SagaStatus
	{
		Pending,
		InProgress,
		Completed,
		Compensating,
		Compensated,
		PartiallyCompensated,
		Failed,
	}

	/// <summary>
	/// Execution log for tracking events with preserved order.
	/// </summary>
	public sealed class ExecutionLog
	{
		private readonly List<string> _events = new();
		private readonly object _lock = new();

		public IReadOnlyList<string> Events
		{
			get
			{
				lock (_lock)
				{
					return _events.ToList();
				}
			}
		}

		public void Log(string evt)
		{
			lock (_lock)
			{
				_events.Add(evt);
			}
		}
	}

	/// <summary>
	/// Dead letter queue item.
	/// </summary>
	public sealed record DeadLetterItem(string SagaId, string Step, string Reason, DateTimeOffset Timestamp);

	/// <summary>
	/// Dead letter queue for failed compensations.
	/// </summary>
	public sealed class DeadLetterQueue
	{
		public ConcurrentBag<DeadLetterItem> Items { get; } = new();

		public void Add(string sagaId, string step, string reason)
		{
			Items.Add(new DeadLetterItem(sagaId, step, reason, DateTimeOffset.UtcNow));
		}
	}

	/// <summary>
	/// Saga state.
	/// </summary>
	public sealed class SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public List<string> CompletedSteps { get; } = new();
		public List<string> CompensatedSteps { get; } = new();
		public List<string> FailedCompensations { get; } = new();
	}

	/// <summary>
	/// Compensating saga for testing.
	/// </summary>
	public sealed class CompensatingSaga
	{
		private readonly ExecutionLog _log;
		private readonly DeadLetterQueue? _dlq;
		private readonly ConcurrentDictionary<string, SagaState> _states = new();
		private readonly ConcurrentDictionary<string, int> _compensationAttempts = new();

		public CompensatingSaga(ExecutionLog log, DeadLetterQueue? dlq = null)
		{
			_log = log;
			_dlq = dlq;
		}

		public string? FailOnStep { get; init; }
		public string? FailCompensationFor { get; init; }
		public string? PermanentCompensationFailure { get; init; }
		public int TransientCompensationFailures { get; init; }
		public int MaxRetries { get; init; } = 3;

		public Task StartAsync(string sagaId)
		{
			_states[sagaId] = new SagaState { SagaId = sagaId, Status = SagaStatus.InProgress };
			return Task.CompletedTask;
		}

		public SagaStatus GetStatus(string sagaId) =>
			_states.TryGetValue(sagaId, out var state) ? state.Status : SagaStatus.Pending;

		public async Task ExecuteStepAsync(string sagaId, string step)
		{
			var state = _states[sagaId];

			if (FailOnStep == step)
			{
				_log.Log($"{step}:Failed");
				state.Status = SagaStatus.Compensating;
				await CompensateAsync(sagaId).ConfigureAwait(false);
				return;
			}

			_log.Log($"{step}:Execute");
			state.CompletedSteps.Add(step);
		}

		private async Task CompensateAsync(string sagaId)
		{
			var state = _states[sagaId];
			var hasFailures = false;

			// Compensate in reverse order
			foreach (var step in state.CompletedSteps.AsEnumerable().Reverse().ToList())
			{
				var compensated = await TryCompensateStepAsync(sagaId, step).ConfigureAwait(false);
				if (compensated)
				{
					state.CompensatedSteps.Add(step);
				}
				else
				{
					state.FailedCompensations.Add(step);
					hasFailures = true;
				}
			}

			state.Status = hasFailures ? SagaStatus.PartiallyCompensated : SagaStatus.Compensated;
		}

		private async Task<bool> TryCompensateStepAsync(string sagaId, string step)
		{
			var attemptKey = $"{sagaId}:{step}";

			// Handle permanent failures
			if (PermanentCompensationFailure == step)
			{
				for (var retry = 0; retry < MaxRetries; retry++)
				{
					_log.Log($"{step}:CompensationRetry");
					await Task.Delay(10).ConfigureAwait(false);
				}

				_dlq?.Add(sagaId, step, $"MaxRetries ({MaxRetries}) exceeded");
				return false;
			}

			// Handle configured compensation failure
			if (FailCompensationFor == step)
			{
				_log.Log($"{step}:CompensationFailed");
				_dlq?.Add(sagaId, step, "Compensation failed");
				return false;
			}

			// Handle transient failures
			var attempts = _compensationAttempts.AddOrUpdate(attemptKey, 1, (_, c) => c + 1);
			if (TransientCompensationFailures > 0 && attempts <= TransientCompensationFailures)
			{
				_log.Log($"{step}:CompensationRetry");
				await Task.Delay(10).ConfigureAwait(false);
				return await TryCompensateStepAsync(sagaId, step).ConfigureAwait(false);
			}

			_log.Log($"{step}:Compensate");
			return true;
		}
	}

	#endregion Test Infrastructure
}
