// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Cloud Storage Snapshots (Cold Event Store) Sample
// ============================================================================
//
// Excalibur supports a "tiered storage" model where a hot event store (SQL
// Server) sits in front of a cold store backed by cloud object storage. Events
// older than the configured archive policy are moved to the cold store; reads
// that span the boundary are transparently stitched back together.
//
// This sample wires all three cloud providers so you can compare the
// configuration surface side-by-side:
//
//   * AWS S3          (Excalibur.EventSourcing.AwsS3)
//   * Azure Blob      (Excalibur.EventSourcing.AzureBlob)
//   * Google Cloud    (Excalibur.EventSourcing.Gcs)
//
// Pick one at runtime via the PROVIDER environment variable:
//   PROVIDER=aws     dotnet run
//   PROVIDER=azure   dotnet run
//   PROVIDER=gcs     dotnet run
//
// ============================================================================

using CloudStorageSnapshots.Archive;
using CloudStorageSnapshots.Audit;
using CloudStorageSnapshots.Commands;
using CloudStorageSnapshots.Domain;

using Excalibur.A3.Audit;
using Excalibur.Application;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Domain;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;

var provider = Environment.GetEnvironmentVariable("PROVIDER") ?? "aws";

var builder = WebApplication.CreateBuilder(args);

var eventStoreCs = builder.Configuration.GetConnectionString("EventStore")
	?? "Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

// Dispatch pipeline + handlers from this assembly (CreateOrderHandler, AppendOrderNotesHandler).
builder.Services.AddDispatch(typeof(Program).Assembly);

// c6wd6f: register event types for secure-by-default resolution
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

// On-demand archive runner so the hot→cold boundary is exercisable via HTTP.
builder.Services.AddSingleton<ManualArchiveRunner>();

// Audit pipeline — IAmAuditable commands emit ActivityAudited records via
// AuditMiddleware into the in-memory store (swap for production publisher).
builder.Services.AddExcalibur(excalibur => excalibur.AddAudit());
builder.Services.AddSingleton<InMemoryAuditStore>();
builder.Services.AddSingleton<IAuditMessagePublisher, InMemoryAuditMessagePublisher>();

builder.Services.AddExcalibur(excalibur =>
{
	excalibur.AddEventSourcing(es =>
	{
		// Hot store = SQL Server
		es.UseSqlServer(sql => sql.ConnectionString(eventStoreCs));

		// Register the order aggregate so IEventSourcedRepository<OrderAggregate, Guid>
		// is resolvable by the command handlers.
		es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));

		// Tiered storage: archive aggressively for the sample so a single demo
		// run exercises the boundary. Production defaults are typically in the
		// 30-90 day range.
		es.UseTieredStorage(policy =>
		{
			policy.MaxAge            = TimeSpan.FromMinutes(1);
			policy.MaxPosition       = 10_000_000;
			policy.RetainRecentCount = 5;
		});

		// Cold store -- pick one cloud provider
		switch (provider.ToUpperInvariant())
		{
			case "AWS":
				es.UseAwsS3ColdEventStore(s3 =>
				{
					s3.BucketName(builder.Configuration["AwsS3:BucketName"] ?? "excalibur-cold-events")
					  .KeyPrefix(builder.Configuration["AwsS3:KeyPrefix"] ?? "events/")
					  .Region(builder.Configuration["AwsS3:Region"] ?? "us-east-1");
				});
				break;

			case "AZURE":
				es.UseAzureBlobColdEventStore(blob =>
				{
					blob.ConnectionString(builder.Configuration.GetConnectionString("AzureBlob")
						?? "UseDevelopmentStorage=true")
					   .ContainerName(builder.Configuration["AzureBlob:ContainerName"] ?? "cold-events")
					   .CreateContainerIfNotExists();
				});
				break;

			case "GCS":
				es.UseGcsColdEventStore(gcs =>
				{
					gcs.ProjectId(builder.Configuration["Gcs:ProjectId"] ?? "my-gcp-project")
					   .BucketName(builder.Configuration["Gcs:BucketName"] ?? "excalibur-cold-events")
					   .ObjectPrefix(builder.Configuration["Gcs:ObjectPrefix"] ?? "events/");

					var credsPath = builder.Configuration["Gcs:CredentialsPath"];
					if (!string.IsNullOrEmpty(credsPath))
					{
						gcs.CredentialsPath(credsPath);
					}
				});
				break;

			default:
				throw new InvalidOperationException(
					$"Unknown PROVIDER='{provider}'. Use aws, azure, or gcs.");
		}
	});
});

var app = builder.Build();

app.MapGet("/", () => Results.Text(
	$$"""
	Cloud Storage Snapshots Sample

	Current provider: {{provider.ToUpperInvariant()}}

	Switch provider:
	  PROVIDER=aws   dotnet run
	  PROVIDER=azure dotnet run
	  PROVIDER=gcs   dotnet run

	Hot/cold flow (tiered storage):
	  POST /orders                            create an order (returns id)
	  POST /orders/{id}/events?count=N        append N note events to the order
	  POST /archive-cycle?batchSize=10        force one archive cycle (hot -> cold)
	  GET  /orders/{id}                       rehydrate the order (stitches hot+cold)

	Try:
	  curl -X POST http://localhost:5000/orders
	  curl -X POST http://localhost:5000/orders/<id>/events?count=20
	  curl -X POST http://localhost:5000/archive-cycle?batchSize=10
	  curl http://localhost:5000/orders/<id>

	  GET /health
	"""));

app.MapGet("/health", () => Results.Ok(new { status = "running", provider }));

// ----------------------------------------------------------------------------
// Canonical write path: POST /orders -> dispatch CreateOrderCommand
// ----------------------------------------------------------------------------
app.MapPost("/orders", async (IDispatcher dispatcher, CancellationToken ct) =>
{
	var command = new CreateOrderCommand(Guid.NewGuid());
	var result = await dispatcher
		.DispatchAsync<CreateOrderCommand, Guid>(command, ct)
		.ConfigureAwait(false);
	return result.Succeeded
		? Results.Created($"/orders/{result.ReturnValue}", new { OrderId = result.ReturnValue })
		: Results.Problem(detail: result.ErrorMessage, statusCode: 500);
});

// ----------------------------------------------------------------------------
// Append many events to exercise the hot-store -> archive candidate transition
// ----------------------------------------------------------------------------
app.MapPost("/orders/{id:guid}/events", async (
	Guid id,
	int count,
	IDispatcher dispatcher,
	CancellationToken ct) =>
{
	var effectiveCount = count <= 0 ? 10 : count;
	var command = new AppendOrderNotesCommand(Guid.NewGuid())
	{
		OrderId = id,
		Count = effectiveCount,
	};
	var result = await dispatcher
		.DispatchAsync<AppendOrderNotesCommand, int>(command, ct)
		.ConfigureAwait(false);
	return result.Succeeded
		? Results.Ok(new { OrderId = id, AppendedCount = effectiveCount, TotalNotes = result.ReturnValue })
		: Results.Problem(detail: result.ErrorMessage, statusCode: 500);
});

// ----------------------------------------------------------------------------
// Force an archive cycle so old events move from hot -> cold store
// ----------------------------------------------------------------------------
app.MapPost("/archive-cycle", async (
	int? batchSize,
	ManualArchiveRunner runner,
	CancellationToken ct) =>
{
	var summary = await runner.RunAsync(batchSize ?? 10, ct).ConfigureAwait(false);
	return Results.Ok(summary);
});

// ----------------------------------------------------------------------------
// Rehydrate across the hot/cold boundary. The TieredEventStoreDecorator
// transparently stitches cold-store events in front of hot-store events.
// ----------------------------------------------------------------------------
app.MapGet("/orders/{id:guid}", async (
	Guid id,
	Excalibur.EventSourcing.IEventSourcedRepository<OrderAggregate, Guid> repository,
	CancellationToken ct) =>
{
	var order = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
	return order is null
		? Results.NotFound()
		: Results.Ok(new { order.Id, NoteCount = order.Notes.Count, Notes = order.Notes });
});

// Audit trail — one record per IAmAuditable dispatch through IDispatcher.
app.MapGet("/audit/recent", (InMemoryAuditStore store, int? take) =>
{
	var records = store.TakeRecent(take ?? 20);
	return Results.Ok(records.Select(r => new
	{
		r.ActivityName,
		r.StatusCode,
		r.CorrelationId,
		r.TenantId,
		r.UserName,
		r.ApplicationName,
		r.ActivityTimestamp,
		HasException = r.Exception is not null,
	}));
});

await app.RunAsync().ConfigureAwait(false);
