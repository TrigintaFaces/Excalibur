// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

// =============================================================================
// E-commerce Order Versioning Example
// =============================================================================
// This example demonstrates the Universal Message Upcasting feature for
// evolving e-commerce order domain events through multiple versions.
//
// Scenario: An e-commerce platform's OrderPlacedEvent has evolved:
//   V1 (Launch):     OrderId, Total
//   V2 (Loyalty):    + CustomerId
//   V3 (Tax Compliance): Split Total → Subtotal + Tax
//
// The upcasting pipeline automatically transforms V1 events to V3 during
// aggregate replay, maintaining backward compatibility with historical data.
// =============================================================================

using EcommerceOrderVersioning.Events;
using EcommerceOrderVersioning.Upcasters;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     E-commerce Order Event Versioning Example                 ║");
Console.WriteLine("║     Demonstrating V1 → V2 → V3 Multi-Hop Upcasting           ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure Services with Message Upcasting
// =============================================================================
Console.WriteLine("📦 Step 1: Configuring DI with message upcasting...");

var services = new ServiceCollection();

// Register the upcasting pipeline with our order upcasters
services.AddMessageUpcasting(builder =>
{
	// Register upcasters explicitly (alternative: use assembly scanning)
	_ = builder.RegisterUpcaster(
		new OrderPlacedV1ToV2Upcaster());

	_ = builder.RegisterUpcaster(
		new OrderPlacedV2ToV3Upcaster());

	// Enable auto-upcasting during event store replay
	_ = builder.EnableAutoUpcastOnReplay();
});

var serviceProvider = services.BuildServiceProvider();
var pipeline = serviceProvider.GetRequiredService<IUpcastingPipeline>();

Console.WriteLine("   ✅ IUpcastingPipeline registered");
Console.WriteLine("   ✅ V1→V2 and V2→V3 upcasters registered");
Console.WriteLine();

// =============================================================================
// Step 2: Simulate Historical Events (as they would be stored in event store)
// =============================================================================
Console.WriteLine("📜 Step 2: Simulating historical events from different eras...");

// Imagine these events were stored at different points in time
var legacyOrderV1 = new OrderPlacedEventV1 { OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Total = 99.99m };
Console.WriteLine($"   [V1] Legacy order from 2023: OrderId={legacyOrderV1.OrderId}, Total=${legacyOrderV1.Total}");

var midPeriodOrderV2 = new OrderPlacedEventV2
{
	OrderId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
	CustomerId = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC"),
	Total = 149.99m
};
Console.WriteLine(
	$"   [V2] Mid-period order from 2024: OrderId={midPeriodOrderV2.OrderId}, CustomerId={midPeriodOrderV2.CustomerId}, Total=${midPeriodOrderV2.Total}");

var currentOrderV3 = new OrderPlacedEventV3
{
	OrderId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
	CustomerId = Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD"),
	Subtotal = 199.99m,
	Tax = 16.00m
};
Console.WriteLine(
	$"   [V3] Current order from 2025: OrderId={currentOrderV3.OrderId}, Subtotal=${currentOrderV3.Subtotal}, Tax=${currentOrderV3.Tax}, Total=${currentOrderV3.Total}");
Console.WriteLine();

// =============================================================================
// Step 3: Demonstrate Multi-Hop Upcasting (V1 → V3)
// =============================================================================
Console.WriteLine("🔄 Step 3: Upcasting V1 event to latest version (V3)...");
Console.WriteLine("   The pipeline automatically finds the shortest path: V1 → V2 → V3");

var upcastedFromV1 = (OrderPlacedEventV3)pipeline.Upcast(legacyOrderV1);

Console.WriteLine();
Console.WriteLine("   📊 Transformation Result (V1 → V3):");
Console.WriteLine($"      Before: OrderId={legacyOrderV1.OrderId}, Total=${legacyOrderV1.Total}, Version={legacyOrderV1.Version}");
Console.WriteLine(
	$"      After:  OrderId={upcastedFromV1.OrderId}, CustomerId={upcastedFromV1.CustomerId}, Subtotal=${upcastedFromV1.Subtotal}, Tax=${upcastedFromV1.Tax}, Version={upcastedFromV1.Version}");
Console.WriteLine();
Console.WriteLine("   ✅ CustomerId defaulted to Guid.Empty (legacy order)");
Console.WriteLine("   ✅ Tax defaulted to $0.00 (historical tax unknown)");
Console.WriteLine("   ✅ Subtotal equals original Total");
Console.WriteLine();

// =============================================================================
// Step 4: Demonstrate Single-Hop Upcasting (V2 → V3)
// =============================================================================
Console.WriteLine("🔄 Step 4: Upcasting V2 event to latest version (V3)...");

var upcastedFromV2 = (OrderPlacedEventV3)pipeline.Upcast(midPeriodOrderV2);

Console.WriteLine();
Console.WriteLine("   📊 Transformation Result (V2 → V3):");
Console.WriteLine(
	$"      Before: OrderId={midPeriodOrderV2.OrderId}, CustomerId={midPeriodOrderV2.CustomerId}, Total=${midPeriodOrderV2.Total}, Version={midPeriodOrderV2.Version}");
Console.WriteLine(
	$"      After:  OrderId={upcastedFromV2.OrderId}, CustomerId={upcastedFromV2.CustomerId}, Subtotal=${upcastedFromV2.Subtotal}, Tax=${upcastedFromV2.Tax}, Version={upcastedFromV2.Version}");
Console.WriteLine();
Console.WriteLine("   ✅ CustomerId preserved from V2");
Console.WriteLine("   ✅ Tax defaulted to $0.00 (historical tax unknown)");
Console.WriteLine();

// =============================================================================
// Step 5: Demonstrate No-Op for Current Version
// =============================================================================
Console.WriteLine("🔄 Step 5: Attempting to upcast current version (V3 → V3)...");

var upcastedFromV3 = pipeline.Upcast(currentOrderV3);

Console.WriteLine();
Console.WriteLine($"   📊 Result: Same instance returned (no transformation needed)");
Console.WriteLine($"      Version: {((IVersionedMessage)upcastedFromV3).Version}");
Console.WriteLine($"      Same instance: {ReferenceEquals(currentOrderV3, upcastedFromV3)}");
Console.WriteLine();

// =============================================================================
// Summary
// =============================================================================
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("📋 Summary:");
Console.WriteLine("   • V1 events (legacy): Upcasted through V2 to V3");
Console.WriteLine("   • V2 events (mid-period): Upcasted directly to V3");
Console.WriteLine("   • V3 events (current): Pass through unchanged");
Console.WriteLine();
Console.WriteLine("🎯 Key Benefits:");
Console.WriteLine("   • BFS algorithm finds shortest transformation path");
Console.WriteLine("   • Near-zero passthrough (~1ns) and fast cached lookups (~15ns)");
Console.WriteLine("   • Type-safe upcasters catch errors at compile time");
Console.WriteLine("   • Transparent defaults for missing data (CustomerId, Tax)");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
