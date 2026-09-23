// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ============================================================================
// Data Processing Background Service Sample
// ============================================================================
//
// This sample demonstrates running Excalibur.Data.DataProcessing as a
// background service (BackgroundService) instead of via Quartz jobs.
//
// Architecture:
//
//   POST /api/tasks/{recordType}
//       |
//       v
//   IDataOrchestrationManager.AddDataTaskForRecordTypeAsync()
//       |  (inserts DataTaskRequest row into DB)
//       v
//   DataProcessingHostedService  (polls on interval)
//       |
//       v
//   IDataOrchestrationManager.ProcessDataTasksAsync()
//       |  (selects pending tasks, resolves IDataProcessor per recordType)
//       v
//   DataProcessor<OrderRecord>.RunAsync()
//       |  (producer fetches batches -> channel -> consumer processes via handler)
//       v
//   OrderRecordHandler.ProcessAsync()
//
// ============================================================================

using System.Data;

using DataProcessingBackgroundService.Data;
using DataProcessingBackgroundService.Processing;

using Excalibur.Data.DataProcessing;

using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? throw new InvalidOperationException("Missing 'DefaultConnection' connection string.");

// -------------------------------------------------------------------
// 1. Register data processing infrastructure
// -------------------------------------------------------------------

// Register the orchestration database connection factory (keyed singleton).
// The DataOrchestrationManager resolves this via [FromKeyedServices].
builder.Services.AddKeyedSingleton(
	DataProcessingKeys.OrchestrationConnection,
	(_, _) => (Func<IDbConnection>)(() => new SqlConnection(connectionString)));

// Register the processor and handler explicitly (AOT-safe path).
builder.Services.AddDataProcessor<OrderDataProcessor>(
	builder.Configuration,
	"DataProcessing");
builder.Services.AddRecordHandler<OrderRecordHandler, OrderRecord>();

// -------------------------------------------------------------------
// 2. Enable the background service (the new feature!)
// -------------------------------------------------------------------

// This replaces the need for Quartz job scheduling.
// The hosted service polls IDataOrchestrationManager.ProcessDataTasksAsync()
// on the configured interval, bound from appsettings.json.
builder.Services.EnableDataProcessingBackgroundService(
	builder.Configuration,
	"DataProcessingService");

// Unhandled exceptions become RFC 9457 Problem Details responses, with the status code taken from
// the exception (404 for ResourceNotFoundException, 409 for ConcurrencyException). Details of a
// 5xx response are hidden outside Development.
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();

app.UseExceptionHandler();

// -------------------------------------------------------------------
// 3. Minimal API endpoints
// -------------------------------------------------------------------

// Create a data task for a given record type.
// The background service will pick it up on the next polling cycle.
//
// Example: POST /api/tasks/OrderRecord
app.MapPost("/api/tasks/{recordType}", async (
	string recordType,
	IDataOrchestrationManager manager,
	CancellationToken ct) =>
{
	var taskId = await manager.AddDataTaskForRecordTypeAsync(recordType, ct);
	return Results.Created($"/api/tasks/{taskId}", new { taskId, recordType });
});

// Health check endpoint showing background service state.
app.MapGet("/health", () => Results.Ok(new { status = "running" }));

app.Run();
