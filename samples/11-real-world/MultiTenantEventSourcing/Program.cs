// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Multi-Tenant Event Sourcing Sample
// ============================================================================
//
// This sample shows how to run Excalibur event sourcing in a multi-tenant
// configuration with:
//
//   1. A per-tenant shard map (ITenantShardMap)
//   2. Per-tenant database routing (UseSqlServerTenantEventStore)
//   3. Tenant-scoped aggregate operations (IEventSourcedRepository is scoped
//      per-request, the routing decorator picks the right shard)
//
// Isolation models available:
//   * Database per tenant   -- full isolation, simplest compliance story
//   * Schema per tenant     -- same database, different schema  (sample default)
//   * Row-level (tenant id) -- single schema, tenantId column discriminator
//
// Tradeoffs are in the README.
//
// ============================================================================

using Excalibur.A3.Audit;
using Excalibur.Application;
using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Domain;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;

using MultiTenantEventSourcing;
using MultiTenantEventSourcing.Audit;
using MultiTenantEventSourcing.Commands;
using MultiTenantEventSourcing.Domain;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// 1. Declare the shards this deployment serves
// ----------------------------------------------------------------------------
// Each shard has its own connection string; ADO.NET manages connection pooling
// per-string automatically. Schema-per-tenant can be expressed via SchemaName.

var shards = new Dictionary<string, ShardInfo>(StringComparer.OrdinalIgnoreCase)
{
	["shard-eu-1"] = new ShardInfo(
		ShardId:          "shard-eu-1",
		ConnectionString: "Server=localhost,1434;Database=EventStore_EU;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True",
		SchemaName:       "dbo",
		Region:           "eu-west-1"),
	["shard-us-1"] = new ShardInfo(
		ShardId:          "shard-us-1",
		ConnectionString: "Server=localhost,1434;Database=EventStore_US;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True",
		SchemaName:       "dbo",
		Region:           "us-east-1")
};

var tenantToShardId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
	["tenant-acme"]   = "shard-us-1",
	["tenant-contoso"] = "shard-eu-1",
	["tenant-globex"] = "shard-us-1"
};

builder.Services.AddSingleton<ITenantShardMap>(
	new SampleTenantShardMap(shards, tenantToShardId));

// ----------------------------------------------------------------------------
// 1a. Dispatch pipeline + per-request tenant resolution
// ----------------------------------------------------------------------------
// Register dispatch handlers from this assembly so
// CreateTenantOrderHandler is discovered.
builder.Services.AddDispatch(typeof(Program).Assembly);

// c6wd6f: register event types for secure-by-default resolution
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

// HTTP context is required so the scoped ITenantId resolver can read the
// X-Tenant-Id header from the inbound request.
builder.Services.AddHttpContextAccessor();

// Per-request ITenantId is resolved from the X-Tenant-Id header. The routing
// decorator (TenantRoutingEventStore) reads this value on every IEventStore
// operation and picks the matching shard via ITenantStoreResolver<IEventStore>.
builder.Services.TryAddTenantId(sp =>
{
	var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
	return http?.Request.Headers["X-Tenant-Id"].FirstOrDefault()
		?? string.Empty;
});

// Audit pipeline — commands mark IAmAuditable, AuditMiddleware builds
// ActivityAudited records with TenantId flowing end-to-end, publisher writes
// to InMemoryAuditStore so GET /audit/recent is observable. The earlier
// TryAddTenantId(sp => header) registration wins via TryAdd precedence, so
// the per-request tenant still flows into every audit record.
builder.Services.AddExcalibur(excalibur => excalibur.AddAudit());
builder.Services.AddSingleton<InMemoryAuditStore>();
builder.Services.AddSingleton<IAuditMessagePublisher, InMemoryAuditMessagePublisher>();

// ----------------------------------------------------------------------------
// 2. Wire Excalibur event sourcing + enable tenant sharding
// ----------------------------------------------------------------------------

builder.Services.AddExcalibur(excalibur =>
{
	excalibur.AddEventSourcing(es =>
	{
		// Default event store config — this is only used when no tenant is
		// in scope (e.g., host-level catch-up services that fan-out to shards).
		es.UseSqlServer(sql => sql.ConnectionString(shards["shard-us-1"].ConnectionString));

		// Register the aggregate type.
		es.AddRepository<TenantScopedOrder, Guid>(id => new TenantScopedOrder(id));
		es.UseIntervalSnapshots(100);

		// Enable tenant-aware routing. Replaces IEventStore with a scoped decorator
		// that looks up the current tenant's shard at each operation.
		es.EnableTenantSharding(opts =>
		{
			opts.DefaultShardId = "shard-us-1";
		});

		// SQL Server provider for tenant-aware resolution.
		es.UseSqlServerTenantEventStore();
	});
});

var app = builder.Build();

// ----------------------------------------------------------------------------
// 3. Example: inspect the tenant-to-shard mapping
// ----------------------------------------------------------------------------

app.MapGet("/shards", (ITenantShardMap map) => Results.Json(new
{
	RegisteredShards = map.GetRegisteredShardIds()
}));

app.MapGet("/shards/{tenantId}", (string tenantId, ITenantShardMap map) =>
{
	try
	{
		var info = map.GetShardInfo(tenantId);
		return Results.Ok(new
		{
			TenantId = tenantId,
			info.ShardId,
			info.SchemaName,
			info.Region,
			ConnectionStringLength = info.ConnectionString.Length  // don't leak credentials
		});
	}
	catch (TenantShardNotFoundException)
	{
		return Results.NotFound(new { TenantId = tenantId, Message = "No shard mapped." });
	}
});

// ----------------------------------------------------------------------------
// 4. POST /orders — exercises TenantRoutingEventStore per-operation shard pick
// ----------------------------------------------------------------------------
// Flow:
//   1. Client sends POST /orders with `X-Tenant-Id: tenant-acme` header.
//   2. Scoped ITenantId resolver reads the header.
//   3. IDispatcher dispatches CreateTenantOrderCommand.
//   4. CreateTenantOrderHandler asks IEventSourcedRepository<TenantScopedOrder, Guid>
//      to save the aggregate; internally the TenantRoutingEventStore decorator
//      resolves ITenantShardMap[tenantId] and writes events to that shard.
//   5. The handler logs `shard-per-operation` so the routing is observable.
app.MapPost("/orders", async (
	CreateOrderRequest body,
	HttpContext context,
	IDispatcher dispatcher,
	CancellationToken ct) =>
{
	ArgumentNullException.ThrowIfNull(body);
	ArgumentNullException.ThrowIfNull(context);

	var tenantHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
	var command = new CreateTenantOrderCommand(Guid.NewGuid(), tenantHeader)
	{
		Total = body.Total,
	};

	var result = await dispatcher
		.DispatchAsync<CreateTenantOrderCommand, Guid>(command, ct)
		.ConfigureAwait(false);

	if (!result.Succeeded)
	{
		return result.ProblemDetails is { } problem
			? Results.Problem(detail: problem.Detail, statusCode: problem.Status ?? 500, title: problem.Title)
			: Results.Problem(detail: result.ErrorMessage, statusCode: 500);
	}

	return Results.Created($"/orders/{result.ReturnValue}", new { OrderId = result.ReturnValue });
});

// Audit trail — one record per IAmAuditable dispatch, including tenant id.
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

app.MapGet("/", () => Results.Text(
	"""
	Multi-tenant event sourcing sample. Endpoints:

	  GET  /shards                              list registered shards
	  GET  /shards/{tenantId}                   shard mapping for a tenant
	  POST /orders  (header X-Tenant-Id: ...)   dispatch CreateTenantOrderCommand
	                                            -> TenantRoutingEventStore picks shard
	                                            -> shard-per-operation logged

	Try:
	  curl -X POST http://localhost:5000/orders \
	    -H 'Content-Type: application/json' \
	    -H 'X-Tenant-Id: tenant-acme' \
	    -d '{"total": 125.50}'

	The X-Tenant-Id header drives the scoped ITenantId; the same mechanism
	applies to any transport — JWT claim, subdomain, gRPC metadata, etc.
	"""));

await app.RunAsync().ConfigureAwait(false);
