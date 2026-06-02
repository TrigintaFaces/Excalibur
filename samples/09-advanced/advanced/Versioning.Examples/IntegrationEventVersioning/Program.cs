// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

// =============================================================================
// Integration Event Versioning Example
// =============================================================================
// This example demonstrates the Universal Message Upcasting feature for
// cross-service integration events in a multi-tenant e-commerce platform.
//
// Scenario: A ProductPriceChanged integration event has evolved:
//   V1 (Single-tenant): ProductId, NewPrice
//   V2 (Multi-tenant):  + TenantId
//   V3 (International): + Currency, OldPrice, ChangePercentage
//
// Challenge: In microservices, different services may be at different versions.
// The consuming service (e.g., Cart Service) needs to handle messages from
// producers at any version level.
//
// Solution: The UpcastingMessageBusDecorator automatically transforms incoming
// integration events to the latest version before delivery to handlers.
// =============================================================================

using Excalibur.Dispatch;

using IntegrationEventVersioning.Events;
using IntegrationEventVersioning.Upcasters;

using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     Integration Event Versioning Example                      ║");
Console.WriteLine("║     Cross-Service Compatibility for Multi-Tenant Platform    ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure Services (Simulating the Cart Service)
// =============================================================================
Console.WriteLine("🛒 Step 1: Configuring Cart Service with message upcasting...");
Console.WriteLine("   The Cart Service always expects ProductPriceChangedV3,");
Console.WriteLine("   but must handle V1/V2 messages from legacy producers.");

var services = new ServiceCollection();

services.AddMessageUpcasting(builder =>
{
	_ = builder.ScanAssembly(typeof(ProductPriceChangedV1ToV2Upcaster).Assembly);
	// Note: In real scenario, also call builder.EnableMessageBusDecorator()
	// to wrap IMessageBus with automatic upcasting on receive
});

var serviceProvider = services.BuildServiceProvider();
var pipeline = serviceProvider.GetRequiredService<IUpcastingPipeline>();

Console.WriteLine("   ✅ IUpcastingPipeline configured");
Console.WriteLine("   ✅ Ready to receive events from any producer version");
Console.WriteLine();

// =============================================================================
// Step 2: Simulate Messages from Different Service Versions
// =============================================================================
Console.WriteLine("📡 Step 2: Simulating messages from services at different versions...");
Console.WriteLine();

// V1: From a legacy Catalog Service that hasn't been upgraded
var v1Message = new ProductPriceChangedV1 { ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"), NewPrice = 29.99m };
Console.WriteLine("   [V1] Message from Legacy Catalog Service (pre-multi-tenant):");
Console.WriteLine($"       ProductId: {v1Message.ProductId}");
Console.WriteLine($"       NewPrice:  ${v1Message.NewPrice}");
Console.WriteLine();

// V2: From a Catalog Service deployed after multi-tenant support
var v2Message = new ProductPriceChangedV2
{
	TenantId = "acme-corp",
	ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
	NewPrice = 49.99m
};
Console.WriteLine("   [V2] Message from Multi-Tenant Catalog Service:");
Console.WriteLine($"       TenantId:  \"{v2Message.TenantId}\"");
Console.WriteLine($"       ProductId: {v2Message.ProductId}");
Console.WriteLine($"       NewPrice:  ${v2Message.NewPrice}");
Console.WriteLine();

// V3: From the latest Catalog Service with international support
var v3Message = new ProductPriceChangedV3
{
	TenantId = "globex-intl",
	ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
	OldPrice = 100.00m,
	NewPrice = 79.99m,
	Currency = "EUR"
};
Console.WriteLine("   [V3] Message from Latest Catalog Service (international):");
Console.WriteLine($"       TenantId:  \"{v3Message.TenantId}\"");
Console.WriteLine($"       ProductId: {v3Message.ProductId}");
Console.WriteLine($"       OldPrice:  €{v3Message.OldPrice}");
Console.WriteLine($"       NewPrice:  €{v3Message.NewPrice}");
Console.WriteLine($"       Currency:  \"{v3Message.Currency}\"");
Console.WriteLine($"       Change:    {v3Message.ChangePercentage}%");
Console.WriteLine();

// =============================================================================
// Step 3: Demonstrate Upcasting in Message Handler Context
// =============================================================================
Console.WriteLine("🔄 Step 3: Processing messages through the upcasting pipeline...");
Console.WriteLine("   (This happens automatically via UpcastingMessageBusDecorator)");
Console.WriteLine();

// Simulate what UpcastingMessageBusDecorator does before delivering to handler
void SimulateMessageHandler(IDispatchMessage receivedMessage)
{
	// The decorator upcasts before delivery
	var upcasted = (ProductPriceChangedV3)pipeline.Upcast(receivedMessage);

	Console.WriteLine($"   📥 Handler received ProductPriceChangedV3:");
	Console.WriteLine($"       TenantId:  \"{upcasted.TenantId}\"");
	Console.WriteLine($"       ProductId: {upcasted.ProductId}");
	Console.WriteLine($"       OldPrice:  {(upcasted.OldPrice.HasValue ? $"${upcasted.OldPrice}" : "null")}");
	Console.WriteLine($"       NewPrice:  ${upcasted.NewPrice}");
	Console.WriteLine($"       Currency:  \"{upcasted.Currency}\"");
	Console.WriteLine($"       Change:    {upcasted.ChangePercentage}%");

	// Show migration artifacts
	if (upcasted.TenantId == ProductPriceChangedV1ToV2Upcaster.DefaultTenantId)
	{
		Console.WriteLine($"       ⚠️ Legacy tenant (upcasted from V1)");
	}

	if (upcasted.OldPrice == upcasted.NewPrice)
	{
		Console.WriteLine($"       ⚠️ No price history (upcasted from V1/V2)");
	}

	Console.WriteLine();
}

Console.WriteLine("   --- Processing V1 message ---");
SimulateMessageHandler(v1Message);

Console.WriteLine("   --- Processing V2 message ---");
SimulateMessageHandler(v2Message);

Console.WriteLine("   --- Processing V3 message ---");
SimulateMessageHandler(v3Message);

// =============================================================================
// Summary: Cross-Service Compatibility
// =============================================================================
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("📋 Summary - Cross-Service Integration Patterns:");
Console.WriteLine();
Console.WriteLine("   🔀 Version Mixing Scenario:");
Console.WriteLine("      • Catalog Service (producer): May be at V1, V2, or V3");
Console.WriteLine("      • Cart Service (consumer): Always expects V3");
Console.WriteLine("      • UpcastingMessageBusDecorator: Bridges the gap");
Console.WriteLine();
Console.WriteLine("   📊 Migration Artifacts (identifiable in V3):");
Console.WriteLine("      • TenantId=\"default\" → Originated as V1");
Console.WriteLine("      • OldPrice==NewPrice → No price history (V1 or V2 origin)");
Console.WriteLine("      • Currency=\"USD\" → May be default from V1/V2 upcast");
Console.WriteLine();
Console.WriteLine("   🎯 Benefits for Microservices:");
Console.WriteLine("      • Independent service deployment (no lockstep upgrades)");
Console.WriteLine("      • Consumer-side transformation (producer doesn't change)");
Console.WriteLine("      • Full backward compatibility during rolling upgrades");
Console.WriteLine("      • Clear audit trail of message origins");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
