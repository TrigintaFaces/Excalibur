// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Examples.TwoProcessProjections;

/// <summary>
/// Everything both roles register, and the one registration that differs between them.
/// </summary>
/// <remarks>
/// A separate class rather than inline composition in <c>Program</c>, because a minimal-API entry
/// point that both composes the container and declares the endpoints ends up coupled to more types
/// than the class-coupling analyzer permits — and splitting it is the fix the analyzer is asking for
/// rather than a suppression.
/// </remarks>
internal static class SampleRegistration
{
	/// <summary>Registers the sample's event sourcing, projections and the role-specific processor.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="role">Either <c>api</c> or <c>jobs</c>.</param>
	/// <returns>The service collection, for chaining.</returns>
	internal static IServiceCollection AddSample(
		this IServiceCollection services,
		string connectionString,
		string role)
	{
		_ = services.AddSingleton(new EffectLog(connectionString));
		_ = services.AddSingleton(new RoleName(role));

		// DURABLE PROJECTION CHECKPOINTS. A separate call from UseSqlServer, deliberately: the host
		// refuses to start until it has been told whether its checkpoints survive a restart, rather
		// than defaulting to in-memory and replaying the whole stream on every start. That replay is
		// not idempotent for an accumulating handler -- a running total double-counts, silently and
		// without bound -- so the choice has to be made out loud. The durability marker is emitted by
		// this same call and is not separately registerable, so a host cannot advertise durability it
		// does not have.
		_ = services.AddSqlServerSubscriptionCheckpointStore(() => new SqlConnection(connectionString));

		// The queryable read model's store. Registered for BOTH roles: the jobs service writes it and
		// the api reads it, and it is the same store either way.
		_ = services.AddSqlServerProjectionStore<OrderSummary>(options =>
		{
			options.ConnectionString = connectionString;
			options.TableName = "SampleOrderSummary";
		});

		return services.AddExcalibur(x => x.AddEventSourcing(es => Configure(es, connectionString, role)));
	}

	private static void Configure(IEventSourcingBuilder es, string connectionString, string role)
	{
		_ = es.UseSqlServer(sql => sql.ConnectionString(connectionString));

		// The aggregate's repository. Registered per aggregate type rather than open-generic, so a
		// type that was never registered is a DI failure at the call site instead of a repository
		// that resolves and then cannot find its event types.
		// The factory creates an EMPTY aggregate for the replay to fold into; the identity arrives
		// with the first event, which is why it is not assigned here.
		_ = es.AddRepository<Order>(_ => new Order());

		// REQUIRED, and the host refuses to start without it. The default JSON serializer rejects
		// event types it was not told about, so an unregistered type would fail on every load and
		// replay; the startup validator turns that into a refusal naming this call instead of a
		// runtime failure on the first read. Both roles need it -- the api to load aggregates, the
		// jobs service to deserialize what it reads off the global stream.
		_ = es.RegisterEventTypesFromAssembly(typeof(SampleRegistration).Assembly);

		// THE QUERYABLE read model. Stored, so something has to apply it. Its handler is DI-resolved
		// (WhenHandledBy), which is the only shape that can reach outside the projection -- here, the
		// effect log that makes a skipped or repeated notification countable.
		_ = es.AddProjection<OrderSummary>(p => p
			.Async()
			.WhenHandledBy<OrderPlaced, OrderPlacedNotificationHandler>()
			.When<OrderLineAdded>((projection, @event) =>
			{
				projection.Total += @event.Amount;
				projection.EventCount++;
			}));

		// THE READ-BY-ID read model. Ephemeral, so there is no stored row, no checkpoint and nothing
		// to apply: it is the fold of one order's events, computed when it is read. It cannot be
		// stale, and it is unavailable to a list screen -- which is why the sample needs both shapes.
		_ = es.AddProjection<OrderDetail>(p => p
			.Ephemeral()
			.When<OrderPlaced>((detail, @event, context) =>
			{
				detail.Id = context.AggregateId;
				detail.Total = @event.Amount;
			})
			.When<OrderLineAdded>((detail, @event) =>
			{
				detail.Total += @event.Amount;
				detail.Lines++;
			}));

		// The projection health check, which is how a stopped or halted projection host becomes
		// visible. It reports Unhealthy when a subscription has LATCHED a halt -- a poison event left
		// its position unchanged, so it will re-read the same batch forever -- and that latch is
		// cleared only by a clean apply, never by an elapsed window. Lag alone cannot see a halt: a
		// stopped host on a quiet stream sits about one event from the head, which is below every
		// sane threshold.
		_ = es.WithProjectionHealthChecks();

		// ───── THE ONE DIFFERENCE BETWEEN THE TWO PROCESSES ─────
		if (role == "jobs")
		{
			// The long-running service owns the background poller. One host, one work set, one
			// checkpoint. Microsoft's guidance for a background-only workload is a Windows Service or
			// a worker, not an IIS-hosted application: an application pool idles out and recycles,
			// and a BackgroundService dies with it, silently, while the site still answers requests.
			_ = es.EnableProjectionProcessing(opts =>
			{
				opts.ProjectionName = "OrderSummaries";
				opts.SubscriptionScope = "two-process-orders/sql-events/sql-order-summary";
				opts.SubscriptionRevision = "1";
				opts.IdlePollingInterval = TimeSpan.FromSeconds(1);

				// Every finite page is checkpointed. External effects still need idempotency.

			});
		}
		else
		{
			// The API registers the processor and NO background worker, then drives it itself after a
			// write. By the time the response returns the queryable read model is current -- no
			// session token on the read, no poller inside IIS.
			_ = es.AddProjectionProcessor(opts =>
			{
				opts.ProjectionName = "OrderSummaries";
				opts.SubscriptionScope = "two-process-orders/sql-events/sql-order-summary";
				opts.SubscriptionRevision = "1";
			});
		}
	}
}
