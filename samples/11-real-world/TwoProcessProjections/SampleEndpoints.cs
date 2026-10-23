// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.Examples.TwoProcessProjections;

/// <summary>The sample's HTTP surface.</summary>
/// <remarks>
/// Separate from <c>Program</c> for the same reason the registration is: a minimal-API entry point
/// that composes the container AND declares every endpoint exceeds the class-coupling limit, and
/// splitting it is the fix the analyzer asks for rather than a suppression.
/// </remarks>
internal static class SampleEndpoints
{
	/// <summary>Maps every endpoint the sample serves.</summary>
	/// <param name="app">The route builder.</param>
	/// <param name="role">Either <c>api</c> or <c>jobs</c>.</param>
	internal static void MapSample(this IEndpointRouteBuilder app, string role)
	{
		app.MapWrites();
		app.MapReads(role);
	}

	// Split from the reads only to stay under the class-coupling limit: a single method declaring
	// every endpoint touches more types than the analyzer permits.
	private static void MapWrites(this IEndpointRouteBuilder app)
	{
		_ = app.MapPost("/orders", async (
			PlaceOrderRequest request,
			IEventSourcedRepository<Order> repository,
			IProjectionProcessor processor,
			CancellationToken cancellationToken) =>
		{
			// NOT a process-local counter. Two processes each start their own at zero and generate
			// colliding identifiers, and so does one process restarted -- the event store then
			// correctly refuses the second write with a concurrency conflict, which looks like a
			// framework fault and is not one. The identity has to be unique across processes.
			var orderId = request.OrderId ?? $"order-{Guid.NewGuid():N}";
			var order = Order.Place(orderId, request.CustomerId, request.Amount);

			var save = await repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

			// READ-YOUR-WRITES, the cheap way. One pass over the stream from the checkpoint, applying
			// everything outstanding, and it RETURNS rather than polling. After this the queryable
			// read model reflects the write, so the caller's next GET needs no token and no waiting.
			//
			// Not atomic with the append, and it does not need to be: the checkpoint is durable, so a
			// crash between the two leaves the read model BEHIND, never missing a fold. Whichever
			// process next drives the processor catches it up.
			var processed = await processor.ProcessPendingAsync(cancellationToken).ConfigureAwait(false);

			return Results.Ok(new PlaceOrderResponse(
				orderId, save.CommittedVersion, save.SessionToken,
				processed.EventsApplied, processed.Outcome.ToString()));
		});

		_ = app.MapPost("/orders/{id}/lines", async (
			string id,
			AddLineRequest request,
			IEventSourcedRepository<Order> repository,
			IProjectionProcessor processor,
			CancellationToken cancellationToken) =>
		{
			var order = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
			if (order is null)
			{
				return Results.NotFound();
			}

			order.AddLine(request.Sku, request.Amount);
			var save = await repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
			var processed = await processor.ProcessPendingAsync(cancellationToken).ConfigureAwait(false);

			return Results.Ok(new PlaceOrderResponse(
				id, save.CommittedVersion, save.SessionToken,
				processed.EventsApplied, processed.Outcome.ToString()));
		});

	}

	private static void MapReads(this IEndpointRouteBuilder app, string role)
	{
		// READ-BY-ID, from the aggregate's own stream. No store, no checkpoint, no staleness possible.
		_ = app.MapGet("/orders/{id}", async (
			string id,
			IEphemeralProjectionEngine engine,
			CancellationToken cancellationToken) =>
			Results.Ok(await engine.BuildAsync<OrderDetail>(id, nameof(Order), cancellationToken)
				.ConfigureAwait(false)));

		// THE QUERYABLE read model -- the one that needs a writer, and the reason this topology exists.
		_ = app.MapGet("/orders", async (
			IProjectionStore<OrderSummary> store,
			CancellationToken cancellationToken) =>
			Results.Ok(await store.QueryAsync(filters: null, options: null, cancellationToken)
				.ConfigureAwait(false)));

		// What the handlers actually DID. One row per attempted effect, so a notification that was
		// skipped when it should have fired, or fired twice when it should have fired once, is a
		// number here rather than something nobody can see.
		_ = app.MapGet("/effects", async (EffectLog log, CancellationToken cancellationToken) =>
		{
			var rows = await log.SummariseAsync(cancellationToken).ConfigureAwait(false);
			return Results.Ok(rows
				.Select(row => new EffectSummary(
					row.EventId, row.Deliveries, row.ReportedReplay, row.Suppressed, row.Performed))
				.ToList());
		});

		// Drives projections on demand, so the README's scenarios can be run without posting an order.
		_ = app.MapPost("/projections/drive", async (
			IProjectionProcessor processor,
			CancellationToken cancellationToken) =>
		{
			var result = await processor.ProcessPendingAsync(cancellationToken).ConfigureAwait(false);
			return Results.Ok(new DriveResponse(result.EventsApplied, result.Outcome.ToString()));
		});

		_ = app.MapGet("/role", () => Results.Ok(new RoleResponse(role)));
	}
}

/// <summary>The body of a place-order request.</summary>
/// <param name="CustomerId">The customer.</param>
/// <param name="Amount">The opening amount.</param>
/// <param name="OrderId">An explicit order id, or null to generate one.</param>
internal sealed record PlaceOrderRequest(string CustomerId, decimal Amount, string? OrderId);

/// <summary>The body of an add-line request.</summary>
/// <param name="Sku">The product.</param>
/// <param name="Amount">The line amount.</param>
internal sealed record AddLineRequest(string Sku, decimal Amount);

/// <summary>What a write returned, including how far projections were driven.</summary>
/// <param name="OrderId">The order.</param>
/// <param name="CommittedVersion">The aggregate version after commit.</param>
/// <param name="SessionToken">The last committed global position, for a session-consistent read.</param>
/// <param name="ProjectionsApplied">How many events the drive applied.</param>
/// <param name="ProjectionOutcome">How the drive finished.</param>
internal sealed record PlaceOrderResponse(
	string OrderId,
	long CommittedVersion,
	long? SessionToken,
	long ProjectionsApplied,
	string ProjectionOutcome);

/// <summary>What happened to one event across every delivery of it.</summary>
/// <param name="EventId">The event.</param>
/// <param name="Deliveries">How many times a handler received it.</param>
/// <param name="ReportedAsReplay">How many of those the framework labelled a re-fold of history.</param>
/// <param name="Suppressed">How many had their effect skipped.</param>
/// <param name="Performed">How many actually performed the effect.</param>
/// <remarks>
/// <b>Deliveries, not attempts</b>, and suppression recorded beside the classification. The earlier
/// shape counted only deliveries whose effect ran, so a wrongly suppressed one disappeared and the
/// replay count could not be anything but zero.
/// </remarks>
internal sealed record EffectSummary(
	string EventId, int Deliveries, int ReportedAsReplay, int Suppressed, int Performed);

/// <summary>The result of driving the processor by hand.</summary>
/// <param name="Applied">How many events were applied.</param>
/// <param name="Outcome">How the drive finished.</param>
internal sealed record DriveResponse(long Applied, string Outcome);

/// <summary>Which role this process is running as.</summary>
/// <param name="Role">Either <c>api</c> or <c>jobs</c>.</param>
internal sealed record RoleResponse(string Role);
