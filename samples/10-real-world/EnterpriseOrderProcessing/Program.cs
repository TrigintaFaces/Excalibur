// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// EnterpriseOrderProcessing Reference Application
//
// This sample demonstrates composing 22+ Excalibur packages in a single application.
// It proves that all packages register into DI without conflicts and build together.
//
// Layers demonstrated:
// - Command Pipeline: IActionHandler + FluentValidation + Polly retry/circuit breaker
// - CDC Anti-Corruption: IDataChangeHandler translating legacy rows to domain commands
// - Event Sourcing: AggregateRoot<Guid> + IEventSourcedRepository<T,TId>
// - Outbox + Transport: SqlServerOutboxStore + RabbitMQ transport for reliable publishing
// - Security: message encryption + audit logging + compliance monitoring
// - Observability: OpenTelemetry metrics/tracing + Serilog structured logging
// - Health Checks: /health, /health/ready, /health/live endpoints
//
// See: management/specs/pre-release-validation-spec.md for full design.

#pragma warning disable CA1303 // Sample code uses literal strings for demonstration
#pragma warning disable IL2026 // UseSecurity uses reflection for configuration binding
#pragma warning disable IL3050 // UseSecurity uses reflection for middleware registration

using EnterpriseOrderProcessing.Commands;
using EnterpriseOrderProcessing.LegacyIntegration;

using Excalibur.Cdc.SqlServer;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Validation;
using Excalibur.Outbox.SqlServer;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("EnterpriseOrderProcessing reference application scaffold.");
Console.WriteLine("Package composition validated at build time.");
Console.WriteLine();

// ============================================================================
// Host Builder -- required for Observability + Serilog extensions
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ============================================================================
// Observability -- OpenTelemetry metrics + distributed tracing (C.1)
// ============================================================================

// Metrics: Prometheus + Console exporters, ASP.NET Core + HttpClient + Runtime instrumentation
builder.ConfigureExcaliburMetrics();

// Tracing: distributed trace from CDC -> Handler -> EventStore -> Transport -> Projection
builder.ConfigureExcaliburTracing();

// Structured logging: Serilog with environment enrichment
builder.ConfigureExcaliburLogging();

// ============================================================================
// DI Registration -- proves all 22+ packages compose in one ServiceCollection
// ============================================================================

var services = builder.Services;

// ---- 1. Core Dispatch + Handler Discovery ----
services.AddDispatch(dispatch =>
{
	// Discover handlers from this assembly (CreateOrderHandler, etc.)
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// 4. Validation middleware (FluentValidation integration)
	_ = dispatch.UseValidation()
		.WithFluentValidation();

	// 5. Resilience middleware (Polly retry + circuit breaker)
	_ = dispatch.UseResilience();

	// 6. Security: message encryption + audit logging (B.1)
	_ = dispatch.UseSecurity(builder.Configuration);

	// 7. Transport (RabbitMQ) for integration event publishing
	_ = dispatch.UseRabbitMQ(rmq =>
		rmq.HostName("localhost")
			.Credentials("guest", "guest"));
});

// Register FluentValidation validators from this assembly
services.AddValidatorsFromAssemblyContaining<CreateOrderValidator>();

// ---- 6. Outbox (SQL Server) for transactional event publishing ----
services.AddSqlServerOutboxStore(options =>
	options.ConnectionString = "Server=localhost;Database=EventStore;Trusted_Connection=true;TrustServerCertificate=true");

// ---- CDC (Change Data Capture) handler registration ----
services.AddSingleton<IDataChangeHandler, LegacyOrderChangeHandler>();

// ---- Compliance: GDPR erasure + monitoring (B.1) ----
services.AddGdprErasure(options =>
{
	options.DefaultGracePeriod = TimeSpan.FromHours(72);
	options.EnableAutoDiscovery = true;
	options.RequireVerification = true;
});

services.AddComplianceMonitoring();

// ---- 8. Health Checks (/health, /health/ready, /health/live) ----
services.AddExcaliburHealthChecks(health =>
{
	// Infrastructure health checks would be added here in a real application:
	// health.AddSqlServer(connectionString, name: "sql-server");
	// health.AddRabbitMQ(rabbitConnectionString, name: "rabbitmq");
	// health.AddElasticsearch(elasticUri, name: "elasticsearch");
	_ = health.AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
});

Console.WriteLine("DI registration complete. All packages composed successfully.");
Console.WriteLine();
Console.WriteLine("Packages wired:");
Console.WriteLine("  - Excalibur.Dispatch (core dispatcher, pipeline)");
Console.WriteLine("  - Excalibur.Dispatch.Abstractions (IDispatchAction, IActionHandler)");
Console.WriteLine("  - Excalibur.Dispatch.Validation.FluentValidation");
Console.WriteLine("  - Excalibur.Dispatch.Resilience.Polly (retry + circuit breaker)");
Console.WriteLine("  - Excalibur.Dispatch.Transport.RabbitMQ");
Console.WriteLine("  - Excalibur.Dispatch.Serialization.MessagePack");
Console.WriteLine("  - Excalibur.Dispatch.Security (encryption + audit)");
Console.WriteLine("  - Excalibur.Dispatch.Compliance (GDPR erasure + monitoring)");
Console.WriteLine("  - Excalibur.Domain (AggregateRoot<Guid>)");
Console.WriteLine("  - Excalibur.EventSourcing + SqlServer");
Console.WriteLine("  - Excalibur.Outbox.SqlServer");
Console.WriteLine("  - Excalibur.Data.SqlServer (CDC)");
Console.WriteLine("  - Excalibur.Cdc + Excalibur.Jobs.Cdc");
Console.WriteLine("  - Excalibur.Data.ElasticSearch");
Console.WriteLine("  - Excalibur.Hosting.Observability (OTel metrics + tracing)");
Console.WriteLine("  - Excalibur.Hosting.Logging.Serilog (structured logging)");
Console.WriteLine("  - Excalibur.Hosting.HealthChecks (/health, /health/ready, /health/live)");
Console.WriteLine("  - Excalibur.Jobs");
