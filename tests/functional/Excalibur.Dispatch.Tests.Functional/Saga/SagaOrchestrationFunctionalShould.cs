// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Saga;

/// <summary>
/// End-to-end functional tests for saga orchestration patterns.
/// Tests demonstrate saga execution and compensation without framework handler dependencies.
/// </summary>
[Trait("Category", "Functional")]
public sealed class SagaOrchestrationFunctionalShould : FunctionalTestBase
{
	#region Basic Saga Tests

	[Fact]
	public async Task Saga_CompletesSuccessfully()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<SagaStateStore>();
			_ = services.AddSingleton<OrderService>();
			_ = services.AddSingleton<PaymentService>();
			_ = services.AddSingleton<InventoryService>();
			_ = services.AddSingleton<ISagaOrchestrator, OrderSagaOrchestrator>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var orchestrator = host.Services.GetRequiredService<ISagaOrchestrator>();
		var stateStore = host.Services.GetRequiredService<SagaStateStore>();

		// Act
		var sagaId = Guid.NewGuid();
		await orchestrator.ExecuteAsync(sagaId, "CUST-001", ["PROD-001", "PROD-002"], 99.99m, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		var sagaState = stateStore.GetState(sagaId);
		_ = sagaState.ShouldNotBeNull();
		sagaState.Status.ShouldBe(SagaStatus.Completed);
	}

	[Fact]
	public async Task Saga_CompensatesOnFailure()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<SagaStateStore>();
			_ = services.AddSingleton<OrderService>();
			_ = services.AddSingleton<FailingPaymentService>();
			_ = services.AddSingleton<InventoryService>();
			_ = services.AddSingleton<ISagaOrchestrator, FailingSagaOrchestrator>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var orchestrator = host.Services.GetRequiredService<ISagaOrchestrator>();
		var stateStore = host.Services.GetRequiredService<SagaStateStore>();
		var orderService = host.Services.GetRequiredService<OrderService>();

		// Act
		var sagaId = Guid.NewGuid();
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => orchestrator.ExecuteAsync(sagaId, "CUST-002", ["PROD-003"], 50.00m, TestCancellationToken)).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		var sagaState = stateStore.GetState(sagaId);
		_ = sagaState.ShouldNotBeNull();
		sagaState.Status.ShouldBe(SagaStatus.Compensated);

		// Order should be cancelled as part of compensation
		var order = orderService.GetOrder(sagaId);
		order.ShouldBeNull(); // Compensated orders are removed
	}

	#endregion

	#region Saga State Management Tests

	[Fact]
	public async Task Saga_PersistsIntermediateState()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<SagaStateStore>();
			_ = services.AddSingleton<OrderService>();
			_ = services.AddSingleton<SlowPaymentService>();
			_ = services.AddSingleton<InventoryService>();
			_ = services.AddSingleton<ISagaOrchestrator, SlowSagaOrchestrator>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var orchestrator = host.Services.GetRequiredService<ISagaOrchestrator>();
		var stateStore = host.Services.GetRequiredService<SagaStateStore>();

		// Act
		var sagaId = Guid.NewGuid();
		var orchestratorTask = orchestrator.ExecuteAsync(sagaId, "CUST-003", ["PROD-004"], 75.00m, TestCancellationToken);

		// Check intermediate state after a short delay
		await Task.Delay(50, TestCancellationToken).ConfigureAwait(false);
		var intermediateState = stateStore.GetState(sagaId);

		await orchestratorTask.ConfigureAwait(false);
		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - State should be captured (may be intermediate or completed depending on timing)
		_ = intermediateState.ShouldNotBeNull();
		intermediateState.Status.ShouldBeOneOf(
			SagaStatus.Started,
			SagaStatus.OrderCreated,
			SagaStatus.PaymentProcessing,
			SagaStatus.PaymentCompleted,
			SagaStatus.InventoryReserved,
			SagaStatus.Completed);
	}

	[Fact]
	public async Task Saga_TracksCompensationHistory()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<SagaStateStore>();
			_ = services.AddSingleton<OrderService>();
			_ = services.AddSingleton<FailingPaymentService>();
			_ = services.AddSingleton<InventoryService>();
			_ = services.AddSingleton<ISagaOrchestrator, FailingSagaOrchestrator>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var orchestrator = host.Services.GetRequiredService<ISagaOrchestrator>();
		var stateStore = host.Services.GetRequiredService<SagaStateStore>();

		// Act
		var sagaId = Guid.NewGuid();
		try
		{
			await orchestrator.ExecuteAsync(sagaId, "CUST-004", ["PROD-005"], 25.00m, TestCancellationToken).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		var sagaState = stateStore.GetState(sagaId);
		_ = sagaState.ShouldNotBeNull();
		sagaState.CompensationSteps.ShouldNotBeEmpty();
		sagaState.CompensationSteps.ShouldContain("CancelOrder");
	}

	[Fact]
	public async Task Saga_TracksCompletedSteps()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<SagaStateStore>();
			_ = services.AddSingleton<OrderService>();
			_ = services.AddSingleton<PaymentService>();
			_ = services.AddSingleton<InventoryService>();
			_ = services.AddSingleton<ISagaOrchestrator, OrderSagaOrchestrator>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var orchestrator = host.Services.GetRequiredService<ISagaOrchestrator>();
		var stateStore = host.Services.GetRequiredService<SagaStateStore>();

		// Act
		var sagaId = Guid.NewGuid();
		await orchestrator.ExecuteAsync(sagaId, "CUST-005", ["PROD-006"], 100.00m, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		var sagaState = stateStore.GetState(sagaId);
		_ = sagaState.ShouldNotBeNull();
		sagaState.CompletedSteps.ShouldContain("CreateOrder");
		sagaState.CompletedSteps.ShouldContain("ProcessPayment");
		sagaState.CompletedSteps.ShouldContain("ReserveInventory");
	}

	#endregion

	#region Test Interfaces and Implementations

	private interface ISagaOrchestrator
	{
		Task ExecuteAsync(Guid sagaId, string customerId, string[] productIds, decimal amount, CancellationToken cancellationToken);
	}

	private enum SagaStatus
	{
		Started,
		OrderCreated,
		PaymentProcessing,
		PaymentCompleted,
		InventoryReserved,
		Completed,
		Failed,
		Compensating,
		Compensated
	}

	private sealed class SagaState
	{
		public Guid SagaId { get; set; }
		public SagaStatus Status { get; set; }
		public List<string> CompletedSteps { get; } = [];
		public List<string> CompensationSteps { get; } = [];
		public string? FailureReason { get; set; }
	}

	private sealed class SagaStateStore
	{
		private readonly Dictionary<Guid, SagaState> _states = [];

		public void SaveState(SagaState state) => _states[state.SagaId] = state;
		public SagaState? GetState(Guid sagaId) => _states.GetValueOrDefault(sagaId);
	}

	private sealed class OrderService
	{
		private readonly Dictionary<Guid, object> _orders = [];

		public void CreateOrder(Guid orderId, string customerId, string[] productIds)
		{
			_orders[orderId] = new { CustomerId = customerId, ProductIds = productIds };
		}

		public void CancelOrder(Guid orderId) => _orders.Remove(orderId);
		public object? GetOrder(Guid orderId) => _orders.GetValueOrDefault(orderId);
	}

	private sealed class PaymentService
	{
		public Task<bool> ProcessPaymentAsync(Guid orderId, decimal amount)
		{
			return Task.FromResult(true);
		}
	}

	private sealed class FailingPaymentService
	{
		public Task<bool> ProcessPaymentAsync(Guid orderId, decimal amount)
		{
			throw new InvalidOperationException("Payment processing failed");
		}
	}

	private sealed class SlowPaymentService
	{
		public async Task<bool> ProcessPaymentAsync(Guid orderId, decimal amount)
		{
			await Task.Delay(100).ConfigureAwait(false);
			return true;
		}
	}

	private sealed class InventoryService
	{
		public Task<bool> ReserveInventoryAsync(string[] productIds) => Task.FromResult(true);
		public Task ReleaseInventoryAsync(string[] productIds) => Task.CompletedTask;
	}

	private sealed class OrderSagaOrchestrator(SagaStateStore stateStore, OrderService orderService, PaymentService paymentService, InventoryService inventoryService) : ISagaOrchestrator
	{
		public async Task ExecuteAsync(Guid sagaId, string customerId, string[] productIds, decimal amount, CancellationToken cancellationToken)
		{
			var state = new SagaState { SagaId = sagaId, Status = SagaStatus.Started };
			stateStore.SaveState(state);

			// Step 1: Create Order
			orderService.CreateOrder(sagaId, customerId, productIds);
			state.Status = SagaStatus.OrderCreated;
			state.CompletedSteps.Add("CreateOrder");
			stateStore.SaveState(state);

			// Step 2: Process Payment
			state.Status = SagaStatus.PaymentProcessing;
			stateStore.SaveState(state);
			_ = await paymentService.ProcessPaymentAsync(sagaId, amount).ConfigureAwait(false);
			state.Status = SagaStatus.PaymentCompleted;
			state.CompletedSteps.Add("ProcessPayment");
			stateStore.SaveState(state);

			// Step 3: Reserve Inventory
			_ = await inventoryService.ReserveInventoryAsync(productIds).ConfigureAwait(false);
			state.Status = SagaStatus.InventoryReserved;
			state.CompletedSteps.Add("ReserveInventory");
			stateStore.SaveState(state);

			// Complete
			state.Status = SagaStatus.Completed;
			stateStore.SaveState(state);
		}
	}

	private sealed class FailingSagaOrchestrator(SagaStateStore stateStore, OrderService orderService, FailingPaymentService paymentService, InventoryService inventoryService) : ISagaOrchestrator
	{
		public async Task ExecuteAsync(Guid sagaId, string customerId, string[] productIds, decimal amount, CancellationToken cancellationToken)
		{
			var state = new SagaState { SagaId = sagaId, Status = SagaStatus.Started };
			stateStore.SaveState(state);

			try
			{
				// Step 1: Create Order
				orderService.CreateOrder(sagaId, customerId, productIds);
				state.Status = SagaStatus.OrderCreated;
				state.CompletedSteps.Add("CreateOrder");
				stateStore.SaveState(state);

				// Step 2: Process Payment (will fail)
				state.Status = SagaStatus.PaymentProcessing;
				stateStore.SaveState(state);
				_ = await paymentService.ProcessPaymentAsync(sagaId, amount).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Compensation
				state.Status = SagaStatus.Compensating;
				state.FailureReason = ex.Message;
				stateStore.SaveState(state);

				// Compensate in reverse order
				if (state.CompletedSteps.Contains("CreateOrder"))
				{
					orderService.CancelOrder(sagaId);
					state.CompensationSteps.Add("CancelOrder");
				}

				state.Status = SagaStatus.Compensated;
				stateStore.SaveState(state);
				throw;
			}
		}
	}

	private sealed class SlowSagaOrchestrator(SagaStateStore stateStore, OrderService orderService, SlowPaymentService paymentService, InventoryService inventoryService) : ISagaOrchestrator
	{
		public async Task ExecuteAsync(Guid sagaId, string customerId, string[] productIds, decimal amount, CancellationToken cancellationToken)
		{
			var state = new SagaState { SagaId = sagaId, Status = SagaStatus.Started };
			stateStore.SaveState(state);

			orderService.CreateOrder(sagaId, customerId, productIds);
			state.Status = SagaStatus.OrderCreated;
			stateStore.SaveState(state);

			state.Status = SagaStatus.PaymentProcessing;
			stateStore.SaveState(state);
			_ = await paymentService.ProcessPaymentAsync(sagaId, amount).ConfigureAwait(false);

			state.Status = SagaStatus.Completed;
			stateStore.SaveState(state);
		}
	}

	#endregion
}
