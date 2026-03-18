// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Transaction;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// Transactional Handlers Example
// ============================================================================
//
// This example demonstrates how handlers participate in transactions:
//
//   1. Register a Func<SqlConnection> connection factory
//   2. Configure the pipeline with UseTransaction()
//   3. Handler uses the connection factory -- connections auto-enlist
//      in the ambient TransactionScope created by TransactionMiddleware
//   4. On success, TransactionMiddleware commits
//   5. On exception, TransactionMiddleware rolls back
//
// Flow:
//
//   Dispatch(TransferFunds)
//        |
//        v
//   TransactionMiddleware
//     [begins TransactionScope]
//        |
//        v
//   TransferFundsHandler
//     [DebitAccount via DataRequest -- auto-enlists in transaction]
//     [CreditAccount via DataRequest -- auto-enlists in transaction]
//        |
//        v
//   TransactionMiddleware
//     [commits on success / rolls back on exception]
//
// Key points:
// - DO NOT manage SqlTransaction manually -- use TransactionScope via middleware
// - Connections from the factory auto-enlist in the ambient TransactionScope
// - Multiple DataRequest operations are atomic within a single handler
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Step 1: Register the connection factory
var connectionString = builder.Configuration["ConnectionStrings:Default"]
	?? "Server=(localdb)\\mssqllocaldb;Database=TransactionalHandlersSample;Trusted_Connection=True;";

builder.Services.AddSingleton<Func<SqlConnection>>(() => new SqlConnection(connectionString));

// Step 2: Configure the dispatch pipeline with transactions
builder.Services.AddDispatch(dispatch =>
{
	// Transaction middleware wraps handler execution in TransactionScope.
	// Only applies to Action messages (commands), not events.
	dispatch.UseTransaction();

	// Register handlers from this assembly
	dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

Console.WriteLine("TransactionalHandlers sample configured successfully.");
Console.WriteLine();
Console.WriteLine("Pipeline: UseTransaction() -> TransferFundsHandler");
Console.WriteLine();
Console.WriteLine("How it works:");
Console.WriteLine("  1. TransactionMiddleware begins TransactionScope");
Console.WriteLine("  2. Handler creates connections via Func<SqlConnection> factory");
Console.WriteLine("  3. Connections auto-enlist in the ambient TransactionScope");
Console.WriteLine("  4. Both DebitAccount and CreditAccount execute atomically");
Console.WriteLine("  5. TransactionMiddleware commits on success, rolls back on exception");
Console.WriteLine();
Console.WriteLine("See docs-site/docs/patterns/transactions.md for the full guide.");
