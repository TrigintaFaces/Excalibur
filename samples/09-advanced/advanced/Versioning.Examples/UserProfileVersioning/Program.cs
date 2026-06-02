// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

// =============================================================================
// User Profile Versioning Example
// =============================================================================
// This example demonstrates the Universal Message Upcasting feature for
// evolving user profile domain events through multiple versions with a focus
// on GDPR compliance patterns.
//
// Scenario: A SaaS platform's UserProfileUpdatedEvent has evolved:
//   V1 (Launch):        UserId, Name (single field)
//   V2 (UX):            Split Name → FirstName + LastName
//   V3 (GDPR):          + ConsentGiven, ConsentDate
//   V4 (Privacy):       + Email, IsEmailEncrypted
//
// The example shows how to handle sensitive data migration while maintaining
// compliance with data protection regulations.
// =============================================================================

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using UserProfileVersioning.Events;
using UserProfileVersioning.Upcasters;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     User Profile Event Versioning Example                     ║");
Console.WriteLine("║     Demonstrating V1 → V4 Multi-Hop Upcasting (GDPR Focus)   ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure Services with Assembly Scanning
// =============================================================================
Console.WriteLine("📦 Step 1: Configuring DI with assembly scanning...");

var services = new ServiceCollection();

// Use assembly scanning to auto-discover upcasters
services.AddMessageUpcasting(builder =>
{
	// Scan this assembly for all IMessageUpcaster implementations
	_ = builder.ScanAssembly(typeof(UserProfileV1ToV2Upcaster).Assembly);

	_ = builder.EnableAutoUpcastOnReplay();
});

var serviceProvider = services.BuildServiceProvider();
var pipeline = serviceProvider.GetRequiredService<IUpcastingPipeline>();

Console.WriteLine("   ✅ Assembly scanning discovered all upcasters");
Console.WriteLine("   ✅ V1→V2→V3→V4 upcasting chain registered");
Console.WriteLine();

// =============================================================================
// Step 2: Simulate Historical User Profiles
// =============================================================================
Console.WriteLine("👤 Step 2: Simulating historical user profiles...");

// V1: Original user from platform launch (2020)
var legacyUserV1 = new UserProfileUpdatedEventV1 { UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "John Smith" };
Console.WriteLine($"   [V1] Legacy user (2020): Name=\"{legacyUserV1.Name}\"");

// V2: User from name split era (2022)
var v2User = new UserProfileUpdatedEventV2
{
	UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
	FirstName = "Jane",
	LastName = "Doe"
};
Console.WriteLine($"   [V2] Mid-era user (2022): FirstName=\"{v2User.FirstName}\", LastName=\"{v2User.LastName}\"");

// V3: User from GDPR compliance era (2023)
var v3User = new UserProfileUpdatedEventV3
{
	UserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
	FirstName = "Alice",
	LastName = "Johnson",
	ConsentGiven = true,
	ConsentDate = new DateTimeOffset(2023, 5, 25, 10, 30, 0, TimeSpan.Zero)
};
Console.WriteLine($"   [V3] GDPR-era user (2023): Consent={v3User.ConsentGiven}, ConsentDate={v3User.ConsentDate:yyyy-MM-dd}");

// V4: Current user with encrypted email (2025)
var v4User = new UserProfileUpdatedEventV4
{
	UserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
	FirstName = "Bob",
	LastName = "Williams",
	ConsentGiven = true,
	ConsentDate = new DateTimeOffset(2025, 1, 15, 14, 0, 0, TimeSpan.Zero),
	Email = "bob.williams@example.com",
	IsEmailEncrypted = false
};
Console.WriteLine($"   [V4] Current user (2025): Email=\"{v4User.Email}\", Encrypted={v4User.IsEmailEncrypted}");
Console.WriteLine();

// =============================================================================
// Step 3: Demonstrate Full V1 → V4 Upcasting
// =============================================================================
Console.WriteLine("🔄 Step 3: Upcasting V1 event to latest version (V4)...");
Console.WriteLine("   Path: V1 → V2 (name split) → V3 (GDPR) → V4 (email)");

var upcastedFromV1 = (UserProfileUpdatedEventV4)pipeline.Upcast(legacyUserV1);

Console.WriteLine();
Console.WriteLine("   📊 Transformation Result (V1 → V4):");
Console.WriteLine($"      Original: Name=\"{legacyUserV1.Name}\"");
Console.WriteLine($"      Result:");
Console.WriteLine($"        FirstName: \"{upcastedFromV1.FirstName}\"");
Console.WriteLine($"        LastName:  \"{upcastedFromV1.LastName}\"");
Console.WriteLine($"        Consent:   {upcastedFromV1.ConsentGiven} (grandfathered)");
Console.WriteLine(
	$"        ConsentDate: {(upcastedFromV1.ConsentDate.HasValue ? upcastedFromV1.ConsentDate.Value.ToString("yyyy-MM-dd") : "null")} (no explicit date)");
Console.WriteLine($"        Email:     \"{upcastedFromV1.Email}\" (must re-verify)");
Console.WriteLine();

// =============================================================================
// Step 4: Demonstrate V2 → V4 Upcasting (Skip name split)
// =============================================================================
Console.WriteLine("🔄 Step 4: Upcasting V2 event to V4 (skip name split)...");

var upcastedFromV2 = (UserProfileUpdatedEventV4)pipeline.Upcast(v2User);

Console.WriteLine();
Console.WriteLine("   📊 Transformation Result (V2 → V4):");
Console.WriteLine($"      FirstName preserved: \"{upcastedFromV2.FirstName}\"");
Console.WriteLine($"      LastName preserved:  \"{upcastedFromV2.LastName}\"");
Console.WriteLine($"      Consent: {upcastedFromV2.ConsentGiven} (grandfathered at V2→V3)");
Console.WriteLine($"      Email:   \"{upcastedFromV2.Email}\" (must re-verify)");
Console.WriteLine();

// =============================================================================
// Step 5: Demonstrate V3 → V4 Upcasting (Email only)
// =============================================================================
Console.WriteLine("🔄 Step 5: Upcasting V3 event to V4 (email addition only)...");

var upcastedFromV3 = (UserProfileUpdatedEventV4)pipeline.Upcast(v3User);

Console.WriteLine();
Console.WriteLine("   📊 Transformation Result (V3 → V4):");
Console.WriteLine($"      Consent preserved: {upcastedFromV3.ConsentGiven}");
Console.WriteLine($"      ConsentDate preserved: {upcastedFromV3.ConsentDate:yyyy-MM-dd}");
Console.WriteLine($"      Email added: \"{upcastedFromV3.Email}\" (user must verify)");
Console.WriteLine();

// =============================================================================
// Summary: GDPR Compliance Patterns
// =============================================================================
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("📋 Summary - GDPR Compliance Patterns:");
Console.WriteLine();
Console.WriteLine("   🔐 Consent Tracking:");
Console.WriteLine("      • Legacy users (V1/V2): Grandfathered with implied consent");
Console.WriteLine("      • GDPR-era users (V3+): Explicit consent with timestamp");
Console.WriteLine("      • ConsentDate=null indicates pre-GDPR registration");
Console.WriteLine();
Console.WriteLine("   📧 Email Privacy:");
Console.WriteLine("      • Legacy users: Email must be re-verified after migration");
Console.WriteLine("      • Current users: Email can be encrypted at rest");
Console.WriteLine("      • IsEmailEncrypted flag enables field-level encryption");
Console.WriteLine();
Console.WriteLine("   🎯 Key Benefits:");
Console.WriteLine("      • Full audit trail through event sourcing");
Console.WriteLine("      • Deterministic transformations (same input = same output)");
Console.WriteLine("      • Type-safe upcasters prevent data corruption");
Console.WriteLine("      • No data loss during schema evolution");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
