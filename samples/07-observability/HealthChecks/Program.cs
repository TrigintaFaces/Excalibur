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

// A top-level sample program wires every feature it demonstrates in one place, so it legitimately
// touches more types than a production class should. Matches the other samples in this tree.
#pragma warning disable CA1506 // Sample has high coupling by design

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

// Unhandled exceptions become a generic RFC 9457 Problem Details 500; the exception message is
// not written to the response. (Excalibur.Hosting.Web's AddGlobalExceptionHandler() adds status-code
// mapping for framework exceptions, such as 404 for ResourceNotFoundException.)
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// Map health endpoints with different configurations

// Full health check - all checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
	ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
	Predicate = _ => true,
});

// Liveness probe - only checks that indicate the app is alive
// Used by Kubernetes to know if the container should be restarted
app.MapHealthChecks("/health/live", HealthProbeOptions.For("live"));

// Readiness probe - checks dependencies
// Used by Kubernetes to know if the container can receive traffic
app.MapHealthChecks("/health/ready", HealthProbeOptions.For("ready"));

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

// Printed once the host is actually listening. A startup failure never reaches this line, so it
// separates "started and serving" from "died or hung during startup" for anyone -- or anything --
// watching the output.
app.Lifetime.ApplicationStarted.Register(static () =>
	Console.WriteLine("Sample ready: health endpoints listening. Press Ctrl+C to stop."));

app.Run();

#pragma warning restore CA1506
