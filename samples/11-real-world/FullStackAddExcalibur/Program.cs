// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Full-Stack AddExcalibur Composition Sample  (bd-15cu8x)
// ============================================================================
//
// This sample is the canonical reference for wiring Excalibur end-to-end via
// the unified AddExcalibur() root builder. Every major subsystem is configured
// together so you can see how the builders compose:
//
//   services.AddExcalibur(excalibur =>
//   {
//       excalibur
//           .AddEventSourcing(...)   // Aggregates + event store + snapshots
//           .AddOutbox(...)          // Transactional outbox + dispatcher
//           .AddCdc(...)             // Change Data Capture from legacy DB
//           .AddIdentityMap(...);    // Cross-system ID mapping (ACL)
//   });
//
// Subsystems that still register directly on IServiceCollection
// (ElasticSearch projections, DataProcessing) are composed alongside
// AddExcalibur() to demonstrate the full pattern.
//
// Every subsystem is configured via .BindConfiguration(...) so all options
// are driven from appsettings.json (+ appsettings.Development.json +
// environment-variable overrides).
//
// ============================================================================

using System.Data;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.A3.Audit;
using Excalibur.Application;
using Excalibur.Cdc.SqlServer;
using Excalibur.Data.DataProcessing;
using Excalibur.Data.ElasticSearch;
using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Domain;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Outbox.SqlServer;

using FullStackAddExcalibur.Audit;
using FullStackAddExcalibur.Commands;
using FullStackAddExcalibur.Domain;
using FullStackAddExcalibur.Processors;
using FullStackAddExcalibur.Projections;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. Connection strings (bound via IConfiguration)
// ============================================================================

var cdcSourceConnectionString = builder.Configuration.GetConnectionString("CdcSource")
	?? "Server=localhost,1433;Database=LegacyDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

var eventStoreConnectionString = builder.Configuration.GetConnectionString("EventStore")
	?? "Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

var identityMapConnectionString = builder.Configuration.GetConnectionString("IdentityMap")
	?? eventStoreConnectionString;

var dataProcessingConnectionString = builder.Configuration.GetConnectionString("DataProcessing")
	?? eventStoreConnectionString;

var elasticsearchNodeUris = builder.Configuration
	.GetSection("Elasticsearch:NodeUris").Get<string[]>()
	?? ["http://localhost:9200"];

// ============================================================================
// 2. Dispatch primitives  (registered automatically by AddExcalibur below,
//    but an explicit AddDispatch(...) is also fine if you need custom pipelines)
// ============================================================================

builder.Services.AddDispatch(typeof(Program).Assembly);

// c6wd6f: register event types for secure-by-default resolution
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

// ============================================================================
// 3. AddExcalibur() - single fluent root for ES + Outbox + CDC + IdentityMap
// ============================================================================

builder.Services.AddExcalibur(excalibur =>
{
	// ---- Event Sourcing with SQL Server provider --------------------------
	excalibur.AddEventSourcing(es =>
	{
		// SQL Server event store; ConnectionString is bound from appsettings
		// via GetConnectionString("EventStore") above.
		es.UseSqlServer(sql =>
		{
			sql.ConnectionString(eventStoreConnectionString);
		});

		// Register the OrderAggregate so IEventSourcedRepository<OrderAggregate, Guid>
		// is available for injection.
		es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));

		// Snapshot every 100 events for faster aggregate rehydration.
		es.UseIntervalSnapshots(100);
	});

	// ---- Transactional Outbox ---------------------------------------------
	excalibur.AddOutbox(outbox =>
	{
		// All options (PollingInterval, BatchSize, RetryPolicy) can be supplied
		// via BindConfiguration("Outbox") if preferred.
		outbox.UseSqlServer(sql =>
			sql.ConnectionString(eventStoreConnectionString));
	});

	// ---- CDC (Change Data Capture) from the legacy source DB --------------
	excalibur.AddCdc(cdc =>
	{
		cdc.UseSqlServer(sql =>
		{
			sql.ConnectionString(cdcSourceConnectionString)
			   .DatabaseName("LegacyDb")
			   .CaptureInstances("dbo_LegacyOrders")
			   .PollingInterval(TimeSpan.FromSeconds(5))
			   .BatchSize(100);
		});

		// Run CDC polling as a hosted background service.
		cdc.EnableBackgroundProcessing();
	});

	// ---- IdentityMap (legacy ID <-> aggregate ID translation) -------------
	excalibur.AddIdentityMap(identity =>
	{
		identity.UseSqlServer(sql =>
		{
			sql.ConnectionString(identityMapConnectionString);
		});
	});
});

// ============================================================================
// 4. ElasticSearch projections  (IServiceCollection registration)
// ============================================================================
// ElasticSearch projection registration is still a direct IServiceCollection
// extension; it composes naturally alongside AddExcalibur(). Options are
// read from the "Elasticsearch" section of appsettings.json.

#pragma warning disable CA1308 // Normalize strings to uppercase -- ES index names require lowercase
var envPrefix = builder.Environment.EnvironmentName.ToLowerInvariant();
#pragma warning restore CA1308

builder.Services.AddElasticSearchProjections(
	shared =>
	{
		shared.NodeUris = [.. elasticsearchNodeUris.Select(u => new Uri(u))];
		shared.ConnectionPoolType = ConnectionPoolType.Sniffing;
		shared.RequestTimeoutSeconds = 30;
	},
	projections =>
	{
		projections.Add<OrderReadModel>(options =>
		{
			options.IndexPrefix = $"{envPrefix}-orders";
			options.CreateIndexOnInitialize = true;
			options.NumberOfShards = 2;
			options.NumberOfReplicas = 1;
		});
	});

// A direct Elasticsearch client is also useful when you want to run native
// queries alongside framework-managed projections.
builder.Services.AddSingleton<ElasticsearchClient>(_ =>
{
	var pool = new StaticNodePool(elasticsearchNodeUris.Select(u => new Uri(u)));
	var settings = new ElasticsearchClientSettings(pool)
		.RequestTimeout(TimeSpan.FromSeconds(30));
	return new ElasticsearchClient(settings);
});

// ============================================================================
// 5. DataProcessing (paged batch pipeline + background host)
// ============================================================================
// DataProcessing currently registers directly on IServiceCollection.
// All options bind from the "DataProcessing" and "DataProcessingService"
// configuration sections for full appsettings-driven tuning.

builder.Services.AddKeyedSingleton(
	DataProcessingKeys.OrchestrationConnection,
	(_, _) => (Func<IDbConnection>)(() => new SqlConnection(dataProcessingConnectionString)));

builder.Services.AddDataProcessor<OrderBatchProcessor>(
	builder.Configuration,
	"DataProcessing");

builder.Services.AddRecordHandler<OrderBatchHandler, OrderBatchRecord>();

builder.Services.EnableDataProcessingBackgroundService(
	builder.Configuration,
	"DataProcessingService");

// ============================================================================
// 6. Operational read model (canonical IEventHandler-driven projection)
// ============================================================================
// The in-memory projection store keeps the sample runnable without an
// ElasticSearch cluster. Its shape mirrors what an ElasticSearch projection
// writer would do: react to domain events and upsert a read model.

builder.Services.AddSingleton<IOrderProjectionStore, InMemoryOrderProjectionStore>();

// ============================================================================
// 7. Audit pipeline (Excalibur.A3.Audit.AuditMiddleware + dependencies)
// ============================================================================
// The commands (CreateOrderCommand etc.) mark themselves IAmAuditable. The
// AuditMiddleware observes the pipeline end-to-end, builds an
// ActivityAudit<TMessage, TResponse>, and publishes an ActivityAudited
// record through IAuditMessagePublisher.
//
// excalibur.AddAudit() (on IExcaliburBuilder, the canonical public path per
// ADR-321/325) wires the full audit stack in one call:
//   TryAddTenantId         - scoped ITenantId (defaults to "Default"; multi-
//                            tenant hosts override via their own resolver,
//                            see the MultiTenantEventSourcing sample)
//   TryAddCorrelationId    - per-request correlation id
//   TryAddETag             - ETag concurrency token
//   TryAddClientAddress    - originating client address
//   IActivityContext       - ActivityContext aggregates the above
//   AuditMiddleware        - registered via TryAddEnumerable<IDispatchMiddleware>
//
// The IOutboxDispatcher required by AuditMiddleware is registered by
// excalibur.AddOutbox(...) earlier in this file, so if the audit publisher
// throws the audit record falls through to the transactional outbox.
builder.Services.AddExcalibur(excalibur => excalibur.AddAudit());
builder.Services.TryAddCorrelationId();
builder.Services.TryAddETag();
builder.Services.TryAddClientAddress();
builder.Services.AddScoped<IActivityContext, ActivityContext>();

// Demo in-memory audit destination. Swap InMemoryAuditMessagePublisher for a
// Kafka / EventHubs / ElasticSearch / Splunk publisher in production.
builder.Services.AddSingleton<InMemoryAuditStore>();
builder.Services.AddSingleton<IAuditMessagePublisher, InMemoryAuditMessagePublisher>();

// ============================================================================
// 8. Build application
// ============================================================================

var app = builder.Build();

app.MapGet("/", () => Results.Text(
	"""
	Full-Stack AddExcalibur composition sample.

	Subsystems wired:
	  * Event Sourcing          (Excalibur.EventSourcing.SqlServer)
	  * Transactional Outbox    (Excalibur.Outbox.SqlServer)
	  * CDC polling             (Excalibur.Cdc.SqlServer)
	  * IdentityMap             (Excalibur.Data.IdentityMap.SqlServer)
	  * ElasticSearch projection(Excalibur.Data.ElasticSearch)
	  * DataProcessing batches  (Excalibur.Data.DataProcessing)

	Operational flow (canonical L3 pipeline):
	  POST /orders              dispatch CreateOrderCommand -> handler ->
	                            event-sourced SaveAsync (outbox) ->
	                            IEventHandler projection write
	  GET  /orders/{id}         read the projected OrderReadModel
	  GET  /orders              list all projected orders

	Try:
	  GET  /health
	"""));

app.MapGet("/health", () => Results.Ok(new { status = "running" }));

// ----------------------------------------------------------------------------
// POST /orders  — canonical dispatcher entry point for a command
// ----------------------------------------------------------------------------
// Sequence:
//   1. Build a CreateOrderCommand (IDispatchAction<Guid>).
//   2. Dispatch through IDispatcher; the pipeline resolves the IActionHandler.
//   3. Handler saves the aggregate via IEventSourcedRepository (events are
//      persisted to the event store and staged in the outbox).
//   4. Handler dispatches the uncommitted domain events so registered
//      IEventHandler<T> projection handlers update the read model.
//   5. The generated OrderId is returned in the response.
app.MapPost("/orders", async (
	CreateOrderRequest request,
	IDispatcher dispatcher,
	CancellationToken cancellationToken) =>
{
	ArgumentNullException.ThrowIfNull(request);

	var command = new CreateOrderCommand
	{
		ExternalOrderId = request.ExternalOrderId,
		CustomerId = request.CustomerId,
		CustomerExternalId = request.CustomerExternalId,
		OrderDate = request.OrderDate ?? DateTime.UtcNow,
		LineItems = [.. request.LineItems.Select(li => new CreateOrderLineItem(li.ProductName, li.Quantity, li.UnitPrice))],
	};

	var result = await dispatcher
		.DispatchAsync<CreateOrderCommand, Guid>(command, cancellationToken)
		.ConfigureAwait(false);

	if (!result.Succeeded)
	{
		return Results.Problem(
			detail: result.ErrorMessage,
			statusCode: result.ProblemDetails?.Status ?? 500,
			title: result.ProblemDetails?.Title ?? "Dispatch failed");
	}

	var orderId = result.ReturnValue;
	return Results.Created($"/orders/{orderId}", new { OrderId = orderId });
});

// ----------------------------------------------------------------------------
// GET /orders/{id}  — read from the IEventHandler-maintained projection
// ----------------------------------------------------------------------------
app.MapGet("/orders/{id:guid}", async (
	Guid id,
	IOrderProjectionStore store,
	CancellationToken cancellationToken) =>
{
	var model = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
	return model is null ? Results.NotFound() : Results.Ok(model);
});

// ----------------------------------------------------------------------------
// GET /orders  — browse the projected read models
// ----------------------------------------------------------------------------
app.MapGet("/orders", async (
	IOrderProjectionStore store,
	CancellationToken cancellationToken) =>
{
	var models = await store.ListAsync(cancellationToken).ConfigureAwait(false);
	return Results.Ok(models);
});

// ----------------------------------------------------------------------------
// GET /audit/recent  — browse ActivityAudited records emitted by AuditMiddleware
// ----------------------------------------------------------------------------
// Every IAmAuditable command that flows through IDispatcher produces one
// ActivityAudited record (activity name, status code, tenant, correlation,
// user, timestamp, request & response payloads, exception if any).
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
