// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Middleware.Transaction;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// Production Pipeline Example
// ============================================================================
//
// This example demonstrates the canonical production middleware pipeline:
//
//   Incoming Message
//        |
//        v
//   +-----------------------+
//   | Security Stack        |  Authentication -> Authorization -> Tenant
//   +-----------------------+
//        |
//        v
//   +-----------------------+
//   | Resilience Stack      |  Timeout -> Retry -> CircuitBreaker
//   +-----------------------+
//        |
//        v
//   +-----------------------+
//   | Validation Stack      |  Validation -> ExceptionMapping
//   +-----------------------+
//        |
//        v
//   +-----------------------+
//   | Transaction           |  TransactionScope wraps handler
//   +-----------------------+
//        |
//        v
//   +-----------------------+
//   | Inbox                 |  Idempotency (inside transaction)
//   +-----------------------+
//        |
//        v
//   +-----------------------+
//   | Outbox                |  Reliable messaging (inside transaction)
//   +-----------------------+
//        |
//        v
//   +-------+
//   |Handler|
//   +-------+
//
// The middleware stacks (UseSecurityStack, UseResilienceStack, UseValidationStack)
// were introduced in Sprint 656 (ADR-220) to simplify production configuration.
//
// Pipeline ordering matters:
// - Security runs first to reject unauthorized requests early
// - Resilience wraps everything below for timeout/retry/circuit breaker
// - Validation ensures message payloads are correct before business logic
// - Transaction wraps the handler + inbox + outbox in a single TransactionScope
// - Inbox provides idempotency inside the transaction
// - Outbox captures integration events inside the transaction
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Register the dispatch pipeline with the full production middleware stack
builder.Services.AddDispatch(dispatch =>
{
	// --- Security: authenticate, authorize, resolve tenant ---
	dispatch.UseSecurityStack();

	// --- Resilience: timeout, retry, circuit breaker ---
	dispatch.UseResilienceStack();

	// --- Validation: payload validation, exception mapping ---
	dispatch.UseValidationStack();

	// --- Transaction: wraps handler execution in TransactionScope ---
	dispatch.UseTransaction();

	// --- Inbox: idempotent message processing (inside transaction) ---
	dispatch.UseInbox();

	// --- Outbox: reliable integration event publishing (inside transaction) ---
	dispatch.UseOutbox();

	// Register handlers from this assembly
	dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

Console.WriteLine("Production Pipeline sample configured successfully.");
Console.WriteLine();
Console.WriteLine("Middleware pipeline order:");
Console.WriteLine("  1. UseSecurityStack()     = Authentication -> Authorization -> TenantIdentity");
Console.WriteLine("  2. UseResilienceStack()   = Timeout -> Retry -> CircuitBreaker");
Console.WriteLine("  3. UseValidationStack()   = Validation -> ExceptionMapping");
Console.WriteLine("  4. UseTransaction()       = TransactionScope wraps handler");
Console.WriteLine("  5. UseInbox()             = Idempotent message processing");
Console.WriteLine("  6. UseOutbox()            = Reliable integration event publishing");
