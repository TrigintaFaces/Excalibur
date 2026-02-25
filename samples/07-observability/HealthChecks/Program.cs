// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// Health Checks Observability Sample
// ===================================
// This sample demonstrates how to implement health checks for Kubernetes
// liveness/readiness probes and monitoring integration.
//
// Endpoints:
// - /health         - Full health check (all checks)
// - /health/live    - Liveness probe (minimal checks)
// - /health/ready   - Readiness probe (dependency checks)

using HealthChecks.UI.Client;

using HealthChecksSample.HealthChecks;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure health checks
builder.Services.AddHealthChecks()
	// Custom Dispatch health check
	.AddCheck<DispatchPipelineHealthCheck>(
		"dispatch_pipeline",
		tags: ["ready", "dispatch"])

	// Memory health check (warn if high memory usage)
	.AddProcessAllocatedMemoryHealthCheck(
		maximumMegabytesAllocated: 500,
		name: "memory",
		tags: ["live", "system"])

	// Disk storage health check
	.AddDiskStorageHealthCheck(
		setup: options => options.AddDrive("C:\\", 1024), // 1GB minimum free
		name: "disk",
		tags: ["ready", "system"])

	// External API health check (example)
	.AddUrlGroup(
		new Uri("https://httpbin.org/status/200"),
		name: "external_api",
		tags: ["ready", "external"],
		timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

// Map health endpoints with different configurations

// Full health check - all checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
	ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
	Predicate = _ => true,
});

// Liveness probe - only checks that indicate the app is alive
// Used by Kubernetes to know if the container should be restarted
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
	ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
	Predicate = check => check.Tags.Contains("live"),
	ResultStatusCodes =
	{
		[HealthStatus.Healthy] = StatusCodes.Status200OK,
		[HealthStatus.Degraded] = StatusCodes.Status200OK,
		[HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
	},
});

// Readiness probe - checks dependencies
// Used by Kubernetes to know if the container can receive traffic
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
	Predicate = check => check.Tags.Contains("ready"),
	ResultStatusCodes =
	{
		[HealthStatus.Healthy] = StatusCodes.Status200OK,
		[HealthStatus.Degraded] = StatusCodes.Status200OK,
		[HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
	},
});

// Root endpoint
app.MapGet("/", () => """
	Health Checks Sample
	====================
	Endpoints:
	  GET /health       - Full health check (all checks)
	  GET /health/live  - Liveness probe (for Kubernetes)
	  GET /health/ready - Readiness probe (for Kubernetes)
	""");

Console.WriteLine("Health Checks Sample");
Console.WriteLine("====================");
Console.WriteLine();
Console.WriteLine("Endpoints:");
Console.WriteLine("  GET /health       - Full health check");
Console.WriteLine("  GET /health/live  - Liveness probe");
Console.WriteLine("  GET /health/ready - Readiness probe");
Console.WriteLine();
Console.WriteLine("Kubernetes configuration:");
Console.WriteLine(@"
  livenessProbe:
    httpGet:
      path: /health/live
      port: 8080
    initialDelaySeconds: 10
    periodSeconds: 30

  readinessProbe:
    httpGet:
      path: /health/ready
      port: 8080
    initialDelaySeconds: 5
    periodSeconds: 10
");

app.Run();
