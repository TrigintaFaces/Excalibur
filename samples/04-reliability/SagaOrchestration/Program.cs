// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SagaOrchestration.Configuration;
using SagaOrchestration.Monitoring;
using SagaOrchestration.Sagas;

// ============================================================================
// Saga Orchestration Example
// ============================================================================
//
// This example demonstrates advanced saga patterns:
//
//   1. TIMEOUT SCHEDULING
//      ─────────────────────────────────────────────────────────
//      Saga Start → RequestTimeoutAsync<InventoryReservationTimeout>(5min)
//                           │
//                           ▼
//      [If saga completes] → CancelTimeoutAsync (timeout NOT delivered)
//      [If saga times out] → Timeout delivered → Trigger compensation
//
//   2. MULTI-STEP COMPENSATION (LIFO ORDER)
//      ─────────────────────────────────────────────────────────
//      Execute: ReserveInventory → ProcessPayment → ShipOrder
//                                        │
//                                        └── (failure)
//                                              │
//                                              ▼
//      Compensate: ShipOrder → ProcessPayment → ReserveInventory
//                     ↑            ↑               ↑
//                   (LIFO - reverse order of execution)
//
//   3. STATE PERSISTENCE
//      ─────────────────────────────────────────────────────────
//      After each step → SaveAsync(state) → State persisted
//                                             │
//      [If process restarts]                  ▼
//                                        ResumeAsync(sagaId)
//                                             │
//                                             ▼
//                                   Continue from checkpoint
//
//   4. MONITORING DASHBOARD
//      ─────────────────────────────────────────────────────────
//      GetActiveSagasAsync() → Running sagas
//      GetStuckSagasAsync()  → Sagas with no progress
//      GetDashboardAsync()   → Aggregates: running/completed/failed/rate
//
// ============================================================================

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║         SAGA ORCHESTRATION PATTERNS EXAMPLE               ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Run the demonstrations
await DemonstrateHappyPathAsync();
await DemonstrateCompensationAsync();
await DemonstrateDashboardAsync();

Console.WriteLine();
Console.WriteLine("Example complete. Press any key to exit.");
Console.ReadKey();

// ============================================================================
// Demonstration 1: Happy Path (all steps succeed)
// ============================================================================

static async Task DemonstrateHappyPathAsync()
{
	Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
	Console.WriteLine("│  DEMO 1: Happy Path (All Steps Succeed)                 │");
	Console.WriteLine("└─────────────────────────────────────────────────────────┘");
	Console.WriteLine();

	var builder = Host.CreateApplicationBuilder();
	_ = builder.Services.AddSagaOrchestration();
	_ = builder.Services.AddLogging(logging =>
	{
		_ = logging.AddConsole();
		_ = logging.SetMinimumLevel(LogLevel.Information);
	});

	using var host = builder.Build();
	using var scope = host.Services.CreateScope();

	var saga = scope.ServiceProvider.GetRequiredService<OrderFulfillmentSaga>();

	var orderData = new OrderSagaData
	{
		OrderId = "ORD-2024-001",
		CustomerId = "CUST-12345",
		TotalAmount = 299.99m,
		InventorySku = "SKU-WIDGET-100",
	};

	Console.WriteLine($"Starting saga for Order: {orderData.OrderId}");
	Console.WriteLine($"Customer: {orderData.CustomerId}, Amount: {orderData.TotalAmount:C}");
	Console.WriteLine();

	await saga.StartAsync(orderData, CancellationToken.None);

	Console.WriteLine();
	Console.WriteLine($"Result: {orderData.Status}");
	Console.WriteLine($"Completed Steps: {string.Join(" → ", orderData.CompletedSteps)}");
	Console.WriteLine($"Tracking Number: {orderData.ShipmentTrackingNumber}");
	Console.WriteLine();
}

// ============================================================================
// Demonstration 2: Compensation (payment fails, triggers rollback)
// ============================================================================

static async Task DemonstrateCompensationAsync()
{
	Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
	Console.WriteLine("│  DEMO 2: Compensation (Payment Fails → LIFO Rollback)   │");
	Console.WriteLine("└─────────────────────────────────────────────────────────┘");
	Console.WriteLine();

	var builder = Host.CreateApplicationBuilder();
	_ = builder.Services.AddSagaOrchestrationWithFailingPayment(); // Uses failing payment step
	_ = builder.Services.AddLogging(logging =>
	{
		_ = logging.AddConsole();
		_ = logging.SetMinimumLevel(LogLevel.Information);
	});

	using var host = builder.Build();
	using var scope = host.Services.CreateScope();

	var saga = scope.ServiceProvider.GetRequiredService<OrderFulfillmentSaga>();

	var orderData = new OrderSagaData
	{
		OrderId = "ORD-2024-002",
		CustomerId = "CUST-99999",
		TotalAmount = 1500.00m, // High amount triggers "insufficient funds"
		InventorySku = "SKU-EXPENSIVE-500",
	};

	Console.WriteLine($"Starting saga for Order: {orderData.OrderId}");
	Console.WriteLine($"Customer: {orderData.CustomerId}, Amount: {orderData.TotalAmount:C}");
	Console.WriteLine();
	Console.WriteLine("NOTE: Payment will fail, triggering compensation...");
	Console.WriteLine();

	await saga.StartAsync(orderData, CancellationToken.None);

	Console.WriteLine();
	Console.WriteLine($"Result: {orderData.Status}");
	Console.WriteLine($"Failure Reason: {orderData.FailureReason}");
	Console.WriteLine($"Steps Executed: {string.Join(" → ", orderData.CompletedSteps)}");
	Console.WriteLine("Compensation Order: ReserveInventory (LIFO - only completed step)");
	Console.WriteLine();
}

// ============================================================================
// Demonstration 3: Dashboard Monitoring
// ============================================================================

static async Task DemonstrateDashboardAsync()
{
	Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
	Console.WriteLine("│  DEMO 3: Monitoring Dashboard                           │");
	Console.WriteLine("└─────────────────────────────────────────────────────────┘");
	Console.WriteLine();

	var builder = Host.CreateApplicationBuilder();
	_ = builder.Services.AddSagaOrchestration();
	_ = builder.Services.AddLogging(logging =>
	{
		_ = logging.AddConsole();
		_ = logging.SetMinimumLevel(LogLevel.Warning); // Less noise for dashboard demo
	});

	using var host = builder.Build();

	var stateStore = host.Services.GetRequiredService<ISagaStateStore>();
	var dashboardService = host.Services.GetRequiredService<SagaDashboardService>();

	// Create some test sagas with different statuses
	Console.WriteLine("Creating test sagas...");

	// 3 completed sagas
	for (var i = 0; i < 3; i++)
	{
		var data = new OrderSagaData
		{
			SagaId = $"saga-completed-{i}",
			OrderId = $"ORD-COMP-{i}",
			CustomerId = "CUST-TEST",
			Status = SagaStatus.Completed,
			TotalAmount = 100m + (i * 50),
		};
		data.CompletedSteps.AddRange(["ReserveInventory", "ProcessPayment", "ShipOrder"]);
		await stateStore.SaveAsync(data, CancellationToken.None);
	}

	// 2 running sagas
	for (var i = 0; i < 2; i++)
	{
		var data = new OrderSagaData
		{
			SagaId = $"saga-running-{i}",
			OrderId = $"ORD-RUN-{i}",
			CustomerId = "CUST-TEST",
			Status = SagaStatus.Running,
			TotalAmount = 200m,
		};
		data.CompletedSteps.Add("ReserveInventory");
		await stateStore.SaveAsync(data, CancellationToken.None);
	}

	// 1 failed saga
	var failedData = new OrderSagaData
	{
		SagaId = "saga-failed-0",
		OrderId = "ORD-FAIL-0",
		CustomerId = "CUST-TEST",
		Status = SagaStatus.Failed,
		FailureReason = "Payment gateway timeout",
		TotalAmount = 500m,
	};
	failedData.CompletedSteps.Add("ReserveInventory");
	await stateStore.SaveAsync(failedData, CancellationToken.None);

	// 1 compensated saga
	var compensatedData = new OrderSagaData
	{
		SagaId = "saga-compensated-0",
		OrderId = "ORD-COMP-X",
		CustomerId = "CUST-TEST",
		Status = SagaStatus.Compensated,
		TotalAmount = 350m,
	};
	await stateStore.SaveAsync(compensatedData, CancellationToken.None);

	Console.WriteLine("Test sagas created: 3 completed, 2 running, 1 failed, 1 compensated");
	Console.WriteLine();

	// Print dashboard
	await dashboardService.PrintDashboardAsync(CancellationToken.None);

	// Check for stuck sagas (simulate by backdating)
	Console.WriteLine("Simulating stuck saga detection...");
	((InMemorySagaStateStore)stateStore).BackdateLastUpdate("saga-running-0", TimeSpan.FromMinutes(45));

	var stuckSagas = await dashboardService.GetStuckSagasAsync(TimeSpan.FromMinutes(30), CancellationToken.None);
	Console.WriteLine($"Stuck sagas detected: {stuckSagas.Count}");
	foreach (var stuck in stuckSagas)
	{
		Console.WriteLine($"  - {stuck.SagaId}: stuck for {stuck.StuckDuration?.TotalMinutes:F0} minutes");
	}

	Console.WriteLine();
}
