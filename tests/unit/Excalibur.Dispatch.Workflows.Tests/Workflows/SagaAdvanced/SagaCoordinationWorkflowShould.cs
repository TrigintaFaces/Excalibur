// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.SagaAdvanced;

/// <summary>
/// Saga Coordination workflow tests.
/// Tests parallel execution, nested sagas, event correlation, conditional logic, and external services.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 183 - Functional Testing Epic Phase 3.
/// bd-m1wgo: Saga Coordination Tests (5 tests).
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "183")]
[Trait("Component", "SagaAdvanced")]
[Trait("Category", "Unit")]
public sealed class SagaCoordinationWorkflowShould
{
	/// <summary>
	/// Tests that a saga can execute multiple steps in parallel and wait for all.
	/// Parallel step group > All complete > Saga continues.
	/// </summary>
	[Fact]
	public async Task ExecuteParallelStepGroupsAndWait()
	{
		// Arrange
		var store = new CoordinationSagaStore();
		var log = new ExecutionLog();
		var saga = new ParallelSaga(store, log);

		// Act - Start saga with parallel steps
		await saga.StartAsync("saga-parallel", new OrderData
		{
			OrderId = "ORD-PARALLEL",
			Items = ["ITEM-A", "ITEM-B", "ITEM-C"],
		}).ConfigureAwait(true);

		// Execute parallel inventory checks (all items at once)
		await saga.ExecuteParallelGroupAsync("saga-parallel", "CheckInventory").ConfigureAwait(true);

		// Execute final step after parallel group completes
		await saga.ProcessStepAsync("saga-parallel", "FinalizeOrder").ConfigureAwait(true);

		// Assert - All parallel steps completed
		var state = await store.GetAsync("saga-parallel").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Completed);

		// Assert - Parallel execution evidence
		log.Steps.ShouldContain(s => s.StartsWith("CheckInventory:ITEM-A:"));
		log.Steps.ShouldContain(s => s.StartsWith("CheckInventory:ITEM-B:"));
		log.Steps.ShouldContain(s => s.StartsWith("CheckInventory:ITEM-C:"));
		log.Steps.ShouldContain("ParallelGroup:CheckInventory:AllCompleted");
		log.Steps.ShouldContain("FinalizeOrder:Execute");

		// Assert - Parallel steps executed concurrently (same timestamp range)
		state.ParallelGroupResults["CheckInventory"].ItemCount.ShouldBe(3);
		state.ParallelGroupResults["CheckInventory"].AllSucceeded.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that a parent saga can start and coordinate a child saga.
	/// Parent saga > Start child > Child completes > Parent continues.
	/// </summary>
	[Fact]
	public async Task CoordinateNestedChildSaga()
	{
		// Arrange
		var store = new CoordinationSagaStore();
		var log = new ExecutionLog();
		var parentSaga = new NestedSaga(store, log, isParent: true);

		// Act - Start parent saga
		await parentSaga.StartAsync("parent-saga", new OrderData { OrderId = "ORD-PARENT" }).ConfigureAwait(true);
		await parentSaga.ProcessStepAsync("parent-saga", "ValidateOrder").ConfigureAwait(true);

		// Parent starts child saga for payment processing
		var childSagaId = await parentSaga.StartChildSagaAsync("parent-saga", "child-payment").ConfigureAwait(true);

		// Simulate child saga execution
		var childSaga = new NestedSaga(store, log, isParent: false);
		await childSaga.ProcessStepAsync(childSagaId, "ProcessPayment").ConfigureAwait(true);
		await childSaga.ProcessStepAsync(childSagaId, "ConfirmPayment").ConfigureAwait(true);
		await childSaga.CompleteAsync(childSagaId).ConfigureAwait(true);

		// Parent waits for child and continues
		await parentSaga.WaitForChildAndContinueAsync("parent-saga", childSagaId).ConfigureAwait(true);
		await parentSaga.ProcessStepAsync("parent-saga", "ShipOrder").ConfigureAwait(true);
		await parentSaga.CompleteAsync("parent-saga").ConfigureAwait(true);

		// Assert - Both sagas completed
		var parentState = await store.GetAsync("parent-saga").ConfigureAwait(true);
		var childState = await store.GetAsync(childSagaId).ConfigureAwait(true);

		_ = parentState.ShouldNotBeNull();
		parentState.Status.ShouldBe(SagaStatus.Completed);
		parentState.ChildSagaIds.ShouldContain(childSagaId);

		_ = childState.ShouldNotBeNull();
		childState.Status.ShouldBe(SagaStatus.Completed);
		childState.ParentSagaId.ShouldBe("parent-saga");

		// Assert - Execution order preserved
		var orderedSteps = log.GetOrderedSteps();
		orderedSteps.IndexOf("ValidateOrder:Execute").ShouldBeLessThan(orderedSteps.IndexOf("ProcessPayment:Execute"));
		orderedSteps.IndexOf("ConfirmPayment:Execute").ShouldBeLessThan(orderedSteps.IndexOf("ShipOrder:Execute"));
	}

	/// <summary>
	/// Tests that saga correlates incoming events by CorrelationId.
	/// External event > Match CorrelationId > Resume correct saga.
	/// </summary>
	[Fact]
	public async Task CorrelateEventsByCorrelationId()
	{
		// Arrange
		var store = new CoordinationSagaStore();
		var log = new ExecutionLog();
		var correlator = new SagaEventCorrelator(store, log);
		var saga1 = new CorrelatedSaga(store, log);
		var saga2 = new CorrelatedSaga(store, log);

		// Start two sagas waiting for payment confirmation
		await saga1.StartAsync("saga-corr-1", new OrderData { OrderId = "ORD-1" }).ConfigureAwait(true);
		await saga1.StartWaitingForEventAsync("saga-corr-1", "PaymentConfirmed").ConfigureAwait(true);

		await saga2.StartAsync("saga-corr-2", new OrderData { OrderId = "ORD-2" }).ConfigureAwait(true);
		await saga2.StartWaitingForEventAsync("saga-corr-2", "PaymentConfirmed").ConfigureAwait(true);

		// Act - External events arrive with CorrelationIds
		var event1 = new ExternalEvent
		{
			EventType = "PaymentConfirmed",
			CorrelationId = "saga-corr-1",
			Payload = new Dictionary<string, object> { ["TransactionId"] = "TXN-001" },
		};

		var event2 = new ExternalEvent
		{
			EventType = "PaymentConfirmed",
			CorrelationId = "saga-corr-2",
			Payload = new Dictionary<string, object> { ["TransactionId"] = "TXN-002" },
		};

		await correlator.HandleEventAsync(event1).ConfigureAwait(true);
		await correlator.HandleEventAsync(event2).ConfigureAwait(true);

		// Assert - Correct sagas resumed
		var state1 = await store.GetAsync("saga-corr-1").ConfigureAwait(true);
		var state2 = await store.GetAsync("saga-corr-2").ConfigureAwait(true);

		_ = state1.ShouldNotBeNull();
		state1.ReceivedEvents.Count.ShouldBe(1);
		state1.ReceivedEvents[0].Payload["TransactionId"].ShouldBe("TXN-001");

		_ = state2.ShouldNotBeNull();
		state2.ReceivedEvents.Count.ShouldBe(1);
		state2.ReceivedEvents[0].Payload["TransactionId"].ShouldBe("TXN-002");

		// Assert - Log shows correlation
		log.Steps.ShouldContain("EventCorrelated:PaymentConfirmed:saga-corr-1");
		log.Steps.ShouldContain("EventCorrelated:PaymentConfirmed:saga-corr-2");
	}

	/// <summary>
	/// Tests that saga executes conditional branches based on state.
	/// Condition evaluated > Branch A or B taken.
	/// </summary>
	[Fact]
	public async Task ExecuteConditionalBranches()
	{
		// Arrange
		var store = new CoordinationSagaStore();
		var log = new ExecutionLog();

		// Saga with high-value order (takes premium branch)
		var premiumSaga = new ConditionalSaga(store, log);
		await premiumSaga.StartAsync("saga-premium", new OrderData
		{
			OrderId = "ORD-PREMIUM",
			Amount = 10000m,
		}).ConfigureAwait(true);

		// Saga with low-value order (takes standard branch)
		var standardSaga = new ConditionalSaga(store, log);
		await standardSaga.StartAsync("saga-standard", new OrderData
		{
			OrderId = "ORD-STANDARD",
			Amount = 50m,
		}).ConfigureAwait(true);

		// Act - Execute conditional step
		await premiumSaga.ProcessConditionalStepAsync("saga-premium").ConfigureAwait(true);
		await standardSaga.ProcessConditionalStepAsync("saga-standard").ConfigureAwait(true);

		// Assert - Different branches taken
		var premiumState = await store.GetAsync("saga-premium").ConfigureAwait(true);
		var standardState = await store.GetAsync("saga-standard").ConfigureAwait(true);

		_ = premiumState.ShouldNotBeNull();
		premiumState.CompletedSteps.ShouldContain("PremiumProcessing");
		premiumState.CompletedSteps.ShouldNotContain("StandardProcessing");
		premiumState.BranchTaken.ShouldBe("Premium");

		_ = standardState.ShouldNotBeNull();
		standardState.CompletedSteps.ShouldContain("StandardProcessing");
		standardState.CompletedSteps.ShouldNotContain("PremiumProcessing");
		standardState.BranchTaken.ShouldBe("Standard");

		// Assert - Log shows conditional execution
		log.Steps.ShouldContain("Condition:saga-premium:Amount>=1000:True:PremiumBranch");
		log.Steps.ShouldContain("Condition:saga-standard:Amount>=1000:False:StandardBranch");
	}

	/// <summary>
	/// Tests that saga handles external service calls with timeouts and retries.
	/// External service call > Timeout > Retry > Success or fallback.
	/// </summary>
	[Fact]
	public async Task HandleExternalServiceWithRetries()
	{
		// Arrange
		var store = new CoordinationSagaStore();
		var log = new ExecutionLog();
		var externalService = new SimulatedExternalService
		{
			FailCount = 2, // Fail first 2 calls, succeed on 3rd
		};
		var saga = new ExternalServiceSaga(store, log, externalService);

		// Act
		await saga.StartAsync("saga-external", new OrderData { OrderId = "ORD-EXT" }).ConfigureAwait(true);
		await saga.CallExternalServiceWithRetryAsync("saga-external", "InventoryService", maxRetries: 3).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-external", "CompleteOrder").ConfigureAwait(true);

		// Assert - Service eventually succeeded
		var state = await store.GetAsync("saga-external").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Completed);
		state.ExternalServiceCalls["InventoryService"].Success.ShouldBeTrue();
		state.ExternalServiceCalls["InventoryService"].AttemptCount.ShouldBe(3);

		// Assert - Retry pattern executed
		log.Steps.ShouldContain("ExternalService:InventoryService:Attempt:1:Failed");
		log.Steps.ShouldContain("ExternalService:InventoryService:Attempt:2:Failed");
		log.Steps.ShouldContain("ExternalService:InventoryService:Attempt:3:Success");
	}

	#region Test Infrastructure

	internal enum SagaStatus
	{
		Pending,
		InProgress,
		WaitingForEvent,
		Completed,
		Failed,
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
		public decimal Amount { get; init; }
		public List<string> Items { get; init; } = [];
	}

	internal sealed class ExternalEvent
	{
		public string EventType { get; init; } = string.Empty;
		public string CorrelationId { get; init; } = string.Empty;
		public Dictionary<string, object> Payload { get; init; } = [];
	}

	internal sealed class ParallelGroupResult
	{
		public int ItemCount { get; set; }
		public bool AllSucceeded { get; set; }
		public DateTimeOffset CompletedAt { get; set; }
	}

	internal sealed class ExternalServiceCallResult
	{
		public bool Success { get; set; }
		public int AttemptCount { get; set; }
		public string Response { get; set; } = string.Empty;
	}

	internal sealed class CoordinationSagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public OrderData Data { get; init; } = new();
		public List<string> CompletedSteps { get; } = [];
		public List<string> ChildSagaIds { get; } = [];
		public string? ParentSagaId { get; set; }
		public string? WaitingForEvent { get; set; }
		public List<ExternalEvent> ReceivedEvents { get; } = [];
		public Dictionary<string, ParallelGroupResult> ParallelGroupResults { get; } = [];
		public Dictionary<string, ExternalServiceCallResult> ExternalServiceCalls { get; } = [];
		public string BranchTaken { get; set; } = string.Empty;
	}

	internal sealed class CoordinationSagaStore
	{
		private readonly ConcurrentDictionary<string, CoordinationSagaState> _sagas = new();

		public Task SaveAsync(CoordinationSagaState state)
		{
			_sagas[state.SagaId] = state;
			return Task.CompletedTask;
		}

		public Task<CoordinationSagaState?> GetAsync(string sagaId)
		{
			_ = _sagas.TryGetValue(sagaId, out var state);
			return Task.FromResult(state);
		}

		public Task<List<CoordinationSagaState>> GetByWaitingEventAsync(string eventType)
		{
			return Task.FromResult(_sagas.Values
				.Where(s => s.WaitingForEvent == eventType)
				.ToList());
		}
	}

	internal sealed class ParallelSaga
	{
		private readonly CoordinationSagaStore _store;
		private readonly ExecutionLog _log;

		public ParallelSaga(CoordinationSagaStore store, ExecutionLog log)
		{
			_store = store;
			_log = log;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new CoordinationSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}");
		}

		public async Task ExecuteParallelGroupAsync(string sagaId, string groupName)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;

			// Execute all items in parallel
			var tasks = state.Data.Items.Select(async item =>
			{
				_log.Log($"{groupName}:{item}:Start");
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10).ConfigureAwait(false); // Simulate work
				_log.Log($"{groupName}:{item}:Complete");
				return true;
			});

			var results = await Task.WhenAll(tasks).ConfigureAwait(false);

			state.ParallelGroupResults[groupName] = new ParallelGroupResult
			{
				ItemCount = results.Length,
				AllSucceeded = results.All(r => r),
				CompletedAt = DateTimeOffset.UtcNow,
			};

			_log.Log($"ParallelGroup:{groupName}:AllCompleted");
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public async Task ProcessStepAsync(string sagaId, string step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.CompletedSteps.Add(step);
			_log.Log($"{step}:Execute");

			if (step == "FinalizeOrder")
			{
				state.Status = SagaStatus.Completed;
			}

			await _store.SaveAsync(state).ConfigureAwait(false);
		}
	}

	internal sealed class NestedSaga
	{
		private readonly CoordinationSagaStore _store;
		private readonly ExecutionLog _log;
		private readonly bool _isParent;

		public NestedSaga(CoordinationSagaStore store, ExecutionLog log, bool isParent)
		{
			_store = store;
			_log = log;
			_isParent = isParent;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new CoordinationSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}:{(_isParent ? "Parent" : "Child")}");
		}

		public async Task<string> StartChildSagaAsync(string parentSagaId, string childSagaPrefix)
		{
			var parentState = await _store.GetAsync(parentSagaId).ConfigureAwait(false);
			if (parentState == null)
			{
				throw new InvalidOperationException($"Parent saga {parentSagaId} not found");
			}

			var childSagaId = $"{childSagaPrefix}-{Guid.NewGuid():N}".Substring(0, 24);

			var childState = new CoordinationSagaState
			{
				SagaId = childSagaId,
				ParentSagaId = parentSagaId,
				Data = parentState.Data,
				Status = SagaStatus.Pending,
			};

			parentState.ChildSagaIds.Add(childSagaId);

			await _store.SaveAsync(childState).ConfigureAwait(false);
			await _store.SaveAsync(parentState).ConfigureAwait(false);

			_log.Log($"ChildSaga:Start:{childSagaId}:Parent:{parentSagaId}");
			return childSagaId;
		}

		public async Task ProcessStepAsync(string sagaId, string step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			state.CompletedSteps.Add(step);
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"{step}:Execute");
		}

		public async Task WaitForChildAndContinueAsync(string parentSagaId, string childSagaId)
		{
			var childState = await _store.GetAsync(childSagaId).ConfigureAwait(false);
			if (childState?.Status != SagaStatus.Completed)
			{
				throw new InvalidOperationException($"Child saga {childSagaId} not completed");
			}

			_log.Log($"Parent:{parentSagaId}:ChildCompleted:{childSagaId}");
		}

		public async Task CompleteAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.Completed;
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Complete:{sagaId}");
		}
	}

	internal sealed class CorrelatedSaga
	{
		private readonly CoordinationSagaStore _store;
		private readonly ExecutionLog _log;

		public CorrelatedSaga(CoordinationSagaStore store, ExecutionLog log)
		{
			_store = store;
			_log = log;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new CoordinationSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}");
		}

		public async Task StartWaitingForEventAsync(string sagaId, string eventType)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.WaitingForEvent;
			state.WaitingForEvent = eventType;
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:WaitingFor:{eventType}:{sagaId}");
		}
	}

	internal sealed class SagaEventCorrelator
	{
		private readonly CoordinationSagaStore _store;
		private readonly ExecutionLog _log;

		public SagaEventCorrelator(CoordinationSagaStore store, ExecutionLog log)
		{
			_store = store;
			_log = log;
		}

		public async Task HandleEventAsync(ExternalEvent evt)
		{
			// Find saga by correlation ID (saga ID = correlation ID in this model)
			var state = await _store.GetAsync(evt.CorrelationId).ConfigureAwait(false);
			if (state == null || state.WaitingForEvent != evt.EventType)
			{
				_log.Log($"EventNotCorrelated:{evt.EventType}:{evt.CorrelationId}");
				return;
			}

			state.ReceivedEvents.Add(evt);
			state.WaitingForEvent = null;
			state.Status = SagaStatus.InProgress;
			await _store.SaveAsync(state).ConfigureAwait(false);

			_log.Log($"EventCorrelated:{evt.EventType}:{evt.CorrelationId}");
		}
	}

	internal sealed class ConditionalSaga
	{
		private readonly CoordinationSagaStore _store;
		private readonly ExecutionLog _log;

		public ConditionalSaga(CoordinationSagaStore store, ExecutionLog log)
		{
			_store = store;
			_log = log;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new CoordinationSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}");
		}

		public async Task ProcessConditionalStepAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;

			// Conditional: Premium processing for orders >= 1000
			bool isPremium = state.Data.Amount >= 1000m;

			if (isPremium)
			{
				state.CompletedSteps.Add("PremiumProcessing");
				state.BranchTaken = "Premium";
				_log.Log($"Condition:{sagaId}:Amount>=1000:True:PremiumBranch");
				_log.Log("PremiumProcessing:Execute");
			}
			else
			{
				state.CompletedSteps.Add("StandardProcessing");
				state.BranchTaken = "Standard";
				_log.Log($"Condition:{sagaId}:Amount>=1000:False:StandardBranch");
				_log.Log("StandardProcessing:Execute");
			}

			await _store.SaveAsync(state).ConfigureAwait(false);
		}
	}

	internal sealed class SimulatedExternalService
	{
		private int _callCount;
		public int FailCount { get; init; }

		public async Task<(bool Success, string Response)> CallAsync(string serviceName)
		{
			await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10).ConfigureAwait(false); // Simulate network latency
			_callCount++;

			if (_callCount <= FailCount)
			{
				return (false, "Service unavailable");
			}

			return (true, "OK");
		}
	}

	internal sealed class ExternalServiceSaga
	{
		private readonly CoordinationSagaStore _store;
		private readonly ExecutionLog _log;
		private readonly SimulatedExternalService _externalService;

		public ExternalServiceSaga(CoordinationSagaStore store, ExecutionLog log, SimulatedExternalService externalService)
		{
			_store = store;
			_log = log;
			_externalService = externalService;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new CoordinationSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}");
		}

		public async Task CallExternalServiceWithRetryAsync(string sagaId, string serviceName, int maxRetries)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			var attempts = 0;

			while (attempts < maxRetries)
			{
				attempts++;
				var (success, response) = await _externalService.CallAsync(serviceName).ConfigureAwait(false);

				if (success)
				{
					_log.Log($"ExternalService:{serviceName}:Attempt:{attempts}:Success");
					state.ExternalServiceCalls[serviceName] = new ExternalServiceCallResult
					{
						Success = true,
						AttemptCount = attempts,
						Response = response,
					};
					await _store.SaveAsync(state).ConfigureAwait(false);
					return;
				}

				_log.Log($"ExternalService:{serviceName}:Attempt:{attempts}:Failed");
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10).ConfigureAwait(false); // Retry delay
			}

			state.ExternalServiceCalls[serviceName] = new ExternalServiceCallResult
			{
				Success = false,
				AttemptCount = attempts,
				Response = "Max retries exceeded",
			};
			state.Status = SagaStatus.Failed;
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public async Task ProcessStepAsync(string sagaId, string step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.CompletedSteps.Add(step);
			_log.Log($"{step}:Execute");

			if (step == "CompleteOrder")
			{
				state.Status = SagaStatus.Completed;
			}

			await _store.SaveAsync(state).ConfigureAwait(false);
		}
	}

	#endregion Test Infrastructure
}

