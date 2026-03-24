// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Inbox Idempotency Example
// ============================================================================
//
// This example demonstrates how UseInbox()/UseIdempotency() middleware
// prevents duplicate message processing:
//
//   Incoming Message (with MessageId)
//        |
//        v
//   +-----------------------+
//   | UseIdempotency()      |  Checks if MessageId was already processed
//   +-----------------------+
//        |
//        v (first time)       --> short-circuit (duplicate)
//   +-------+
//   |Handler|
//   +-------+
//
// Key concepts:
// - UseInbox() and UseIdempotency() are aliases -- both register InboxMiddleware
// - The middleware tracks processed message IDs in an IInboxStore
// - Duplicate messages are silently skipped (no exception, no re-processing)
// - Use InMemory store for dev/test; SQL Server or Postgres for production
//
// ============================================================================

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Inbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configure the dispatch pipeline with idempotency middleware
builder.Services.AddDispatch(dispatch =>
{
	// UseIdempotency() is an alias for UseInbox() -- both provide deduplication.
	// Place it early in the pipeline so duplicates are rejected before
	// validation, transaction, or handler execution.
	dispatch.UseIdempotency();

	// Register handlers from this assembly
	dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Configure the inbox store (in-memory for demo)
builder.Services.AddExcaliburInbox(inbox =>
{
	inbox.UseInMemory();
});

var app = builder.Build();

Console.WriteLine("InboxIdempotency sample configured successfully.");
Console.WriteLine();
Console.WriteLine("Pipeline: UseIdempotency() -> Handler");
Console.WriteLine();
Console.WriteLine("How it works:");
Console.WriteLine("  1. Each message carries a unique MessageId (via IDispatchEvent.EventId)");
Console.WriteLine("  2. UseIdempotency() middleware checks the inbox store for the MessageId");
Console.WriteLine("  3. First time: message is processed, MessageId is recorded");
Console.WriteLine("  4. Duplicate: message is silently skipped (no exception)");
Console.WriteLine();
Console.WriteLine("Middleware aliases:");
Console.WriteLine("  dispatch.UseInbox()        -- full name");
Console.WriteLine("  dispatch.UseIdempotency()  -- alias (same middleware)");
Console.WriteLine();
Console.WriteLine("Production setup:");
Console.WriteLine("  services.AddExcaliburInbox(inbox => inbox.UseSqlServer(opts => opts.ConnectionString = connectionString));");
Console.WriteLine("  services.AddExcaliburInbox(inbox => inbox.UsePostgres(pg => pg.ConnectionString = connectionString));");
Console.WriteLine();
Console.WriteLine("Recommended pipeline order:");
Console.WriteLine("  dispatch.UseSecurityStack();");
Console.WriteLine("  dispatch.UseResilienceStack();");
Console.WriteLine("  dispatch.UseIdempotency();     // Reject duplicates early");
Console.WriteLine("  dispatch.UseValidationStack();");
Console.WriteLine("  dispatch.UseTransaction();");
Console.WriteLine("  dispatch.UseOutbox();");
Console.WriteLine();
Console.WriteLine("See samples/09-advanced/ProductionPipeline for the full pipeline example.");
