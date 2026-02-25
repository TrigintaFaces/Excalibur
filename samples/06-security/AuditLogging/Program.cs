// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Compliance Audit Logging Sample
// ================================
// This sample demonstrates compliance audit logging for SOC2, HIPAA, and GDPR:
// - Command/query logging with timestamps
// - User identity capture
// - Sensitive field redaction (PII, PCI)
// - Structured audit log output
//
// Prerequisites:
// 1. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using AuditLoggingSample.Messages;
using AuditLoggingSample.Middleware;

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Security;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var builder = new HostApplicationBuilder(args);

// Add appsettings.json configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch with audit logging middleware
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer for message payloads
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

// ============================================================
// Configure security auditing
// ============================================================
// Add security auditing services (uses InMemory store by default)
builder.Services.AddSecurityAuditing(builder.Configuration);

// Register audit logging middleware
builder.Services.AddSingleton<IDispatchMiddleware, AuditLoggingMiddleware>();

// ============================================================
// Configure outbox/inbox for reliable messaging
// ============================================================
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var securityEventLogger = host.Services.GetRequiredService<ISecurityEventLogger>();
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

// Add user context for audit logging
context.SetItem("User:MessageId", "user-12345");
context.SetItem("Client:IP", "192.168.1.100");
context.SetItem("Client:UserAgent", "AuditLoggingSample/1.0");

logger.LogInformation("Starting Compliance Audit Logging Sample...");
logger.LogInformation("Demonstrating audit logging with PII redaction");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demonstrate command logging with PII redaction
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Command Audit Logging Demo ===");

// Create an order (demonstrates basic audit logging)
var createOrder = new CreateOrderCommand(
	OrderId: "ORD-2026-001",
	CustomerId: "CUST-12345",
	CustomerEmail: "john.doe@example.com",
	TotalAmount: 299.99m,
	CreditCardLast4: "1234");

logger.LogInformation("Dispatching CreateOrderCommand...");
await dispatcher.DispatchAsync(createOrder, context, cancellationToken: default).ConfigureAwait(false);

// Short delay for visibility
await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demonstrate PII redaction in audit logs
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== PII Redaction Demo ===");

// Update customer with sensitive data (demonstrates field redaction)
var updateCustomer = new UpdateCustomerCommand(
	CustomerId: "CUST-12345",
	Name: "John Doe",
	Email: "john.doe@example.com",
	PhoneNumber: "+1-555-123-4567",
	SocialSecurityNumber: "123-45-6789");

logger.LogInformation("Dispatching UpdateCustomerCommand with PII...");
logger.LogInformation("  Original Email: john.doe@example.com");
logger.LogInformation("  Original Phone: +1-555-123-4567");
logger.LogInformation("  Original SSN: 123-45-6789");
logger.LogInformation("Watch the audit log - these values will be REDACTED");

await dispatcher.DispatchAsync(updateCustomer, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demonstrate sensitive operation logging
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Sensitive Operation Logging Demo ===");

// Delete an order (demonstrates elevated severity for destructive operations)
var deleteOrder = new DeleteOrderCommand(
	OrderId: "ORD-2026-001",
	Reason: "Customer requested cancellation",
	DeletedBy: "admin@company.com");

logger.LogInformation("Dispatching DeleteOrderCommand (sensitive operation)...");
await dispatcher.DispatchAsync(deleteOrder, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demonstrate direct security event logging
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Security Event Logging Demo ===");

// Log authentication success
await securityEventLogger.LogSecurityEventAsync(
	SecurityEventType.AuthenticationSuccess,
	"User successfully authenticated via SSO",
	SecuritySeverity.Low,
	CancellationToken.None, context).ConfigureAwait(false);

logger.LogInformation("Logged: AuthenticationSuccess (Low severity)");

// Log authorization failure (simulated)
await securityEventLogger.LogSecurityEventAsync(
	SecurityEventType.AuthorizationFailure,
	"User attempted to access admin panel without privileges",
	SecuritySeverity.High,
	CancellationToken.None, context).ConfigureAwait(false);

logger.LogInformation("Logged: AuthorizationFailure (High severity)");

// Log suspicious activity
await securityEventLogger.LogSecurityEventAsync(
	SecurityEventType.SuspiciousActivity,
	"Multiple failed login attempts detected from same IP",
	SecuritySeverity.Critical,
	CancellationToken.None, context).ConfigureAwait(false);

logger.LogInformation("Logged: SuspiciousActivity (Critical severity)");

// Log rate limit exceeded
await securityEventLogger.LogSecurityEventAsync(
	SecurityEventType.RateLimitExceeded,
	"API rate limit exceeded: 100 requests/minute",
	SecuritySeverity.Medium,
	CancellationToken.None, context).ConfigureAwait(false);

logger.LogInformation("Logged: RateLimitExceeded (Medium severity)");

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Summary of compliance patterns
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Compliance Patterns Summary ===");
logger.LogInformation("");
logger.LogInformation("SOC2 Controls Demonstrated:");
logger.LogInformation("  CC6.1 - Logical access security (user identity tracking)");
logger.LogInformation("  CC6.6 - Audit logging of system activities");
logger.LogInformation("  CC6.7 - Security event monitoring");
logger.LogInformation("");
logger.LogInformation("HIPAA Safeguards Demonstrated:");
logger.LogInformation("  164.312(b) - Audit controls for PHI access");
logger.LogInformation("  164.312(c) - Integrity controls (tamper-evident logs)");
logger.LogInformation("  164.312(d) - Person/entity authentication tracking");
logger.LogInformation("");
logger.LogInformation("GDPR Requirements Demonstrated:");
logger.LogInformation("  Art. 30 - Records of processing activities");
logger.LogInformation("  Art. 32 - Security of processing (pseudonymization via redaction)");
logger.LogInformation("  Art. 33 - Breach notification support (security event tracking)");
logger.LogInformation("");
logger.LogInformation("Redaction Patterns:");
logger.LogInformation("  - Email addresses: [EMAIL REDACTED]");
logger.LogInformation("  - Phone numbers: [PHONE REDACTED]");
logger.LogInformation("  - Credit card numbers: [CARD REDACTED]");
logger.LogInformation("  - Social Security Numbers: [SSN REDACTED]");
logger.LogInformation("  - Named sensitive fields: [REDACTED]");
logger.LogInformation("");
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
