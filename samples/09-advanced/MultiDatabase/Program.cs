// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Multi-Database Example
// ============================================================================
//
// This example demonstrates how to register multiple Func<SqlConnection>
// factories for different databases in one application using keyed services.
//
// Pattern:
//   1. Register each database connection factory as a keyed service
//   2. Inject the specific factory using [FromKeyedServices("key")]
//   3. Each handler/service gets the correct database connection
//
// This is useful when your application needs to:
//   - Read from a reporting database and write to an operational database
//   - Access a legacy database alongside a new database
//   - Query across multiple bounded contexts with separate databases
//
// ============================================================================

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// ============================================================
// Step 1: Define connection strings (from configuration)
// ============================================================
var ordersConnectionString = builder.Configuration["ConnectionStrings:Orders"]
	?? "Server=(localdb)\\mssqllocaldb;Database=OrdersDb;Trusted_Connection=True;";

var inventoryConnectionString = builder.Configuration["ConnectionStrings:Inventory"]
	?? "Server=(localdb)\\mssqllocaldb;Database=InventoryDb;Trusted_Connection=True;";

var reportingConnectionString = builder.Configuration["ConnectionStrings:Reporting"]
	?? "Server=(localdb)\\mssqllocaldb;Database=ReportingDb;Trusted_Connection=True;";

// ============================================================
// Step 2: Register keyed connection factories
// ============================================================
// Each database gets its own Func<SqlConnection> factory, registered
// with a string key. Handlers inject the specific factory they need.

builder.Services.AddKeyedSingleton<Func<SqlConnection>>(
	"Orders",
	(_, _) => () => new SqlConnection(ordersConnectionString));

builder.Services.AddKeyedSingleton<Func<SqlConnection>>(
	"Inventory",
	(_, _) => () => new SqlConnection(inventoryConnectionString));

builder.Services.AddKeyedSingleton<Func<SqlConnection>>(
	"Reporting",
	(_, _) => () => new SqlConnection(reportingConnectionString));

// ============================================================
// Step 3: Also register a default (non-keyed) factory
// ============================================================
// The default factory is used by framework components (outbox, event store)
// and handlers that don't specify a key.
builder.Services.AddSingleton<Func<SqlConnection>>(
	() => new SqlConnection(ordersConnectionString));

var app = builder.Build();

Console.WriteLine("MultiDatabase sample configured successfully.");
Console.WriteLine();
Console.WriteLine("Registered connection factories:");
Console.WriteLine($"  [Orders]    -> {ordersConnectionString}");
Console.WriteLine($"  [Inventory] -> {inventoryConnectionString}");
Console.WriteLine($"  [Reporting] -> {reportingConnectionString}");
Console.WriteLine($"  [Default]   -> {ordersConnectionString}");
Console.WriteLine();
Console.WriteLine("Usage in handlers:");
Console.WriteLine();
Console.WriteLine("  // Inject a specific database's connection factory");
Console.WriteLine("  public class TransferInventoryHandler(");
Console.WriteLine("      [FromKeyedServices(\"Orders\")] Func<SqlConnection> ordersDb,");
Console.WriteLine("      [FromKeyedServices(\"Inventory\")] Func<SqlConnection> inventoryDb)");
Console.WriteLine("  {");
Console.WriteLine("      public async Task Handle(TransferInventory cmd, CancellationToken ct)");
Console.WriteLine("      {");
Console.WriteLine("          using var ordersConn = ordersDb();");
Console.WriteLine("          using var inventoryConn = inventoryDb();");
Console.WriteLine("          // Both connections auto-enlist in ambient TransactionScope");
Console.WriteLine("      }");
Console.WriteLine("  }");
Console.WriteLine();
Console.WriteLine("  // Or use the default (non-keyed) factory");
Console.WriteLine("  public class SimpleHandler(Func<SqlConnection> connectionFactory)");
Console.WriteLine("  {");
Console.WriteLine("      // Uses the default Orders database");
Console.WriteLine("  }");
Console.WriteLine();
Console.WriteLine("See docs-site/docs/data-providers/multi-database.md for the full guide.");
