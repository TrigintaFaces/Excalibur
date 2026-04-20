// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Transport Bindings API Demo  (bd-xk0s07)
// ============================================================================
//
// Demonstrates the two pillars of the Excalibur event-ingress pipeline:
//
//   1. Named message transports (queue-style, e.g. InMemory/RabbitMQ/Kafka/SB)
//   2. Typed cron timers registered through the same transport abstraction
//      via AddCronTimerTransport<TTimer>(cronExpr, configure) + a marker
//      struct implementing ICronTimerMarker.
//
// Both are composed into the dispatcher via the declarative
//   services.AddEventBindings(b => b.FromQueue/FromTimer/FromTransport
//                                    .RouteType<T>()
//                                    .ToDispatcher(profile));
//
// The cron-timer path is the framework's flagship pattern: zero-allocation
// typed markers eliminate string-name filtering, enable health checks,
// OpenTelemetry metrics, overlap prevention, and tz-aware scheduling out of
// the box. See docs-site/docs/transports/cron-timer.md and
// docs-site/docs/migration/from-aspnet-eventing-proposal.md for the full
// design.
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TransportBindings;

Console.WriteLine("Transport Bindings API Demo");
Console.WriteLine("===========================");

var builder = Host.CreateApplicationBuilder();

// ----------------------------------------------------------------------------
// 1. Named message transports — production swaps these for real brokers.
// ----------------------------------------------------------------------------
builder.Services.AddInMemoryTransport("orders");

// ----------------------------------------------------------------------------
// 2. Typed cron timer transport — the framework's flagship scheduled-work
//    primitive. Each unique TTimer marker = one independent timer with its
//    own cron expression, overlap policy, time-zone, and health check.
// ----------------------------------------------------------------------------
builder.Services.AddCronTimerTransport<OrderArrivalTimer>(
	cronExpression: "*/10 * * * * *", // every 10 seconds (6-field cron)
	configure: static o =>
	{
		o.PreventOverlap = true;
		o.RunOnStartup = true;
		o.TimeZone = TimeZoneInfo.Utc;
	});

// ----------------------------------------------------------------------------
// 3. Declarative bindings — route inbound messages from each transport into
//    the local dispatcher pipeline via a named pipeline profile.
// ----------------------------------------------------------------------------
builder.Services.AddEventBindings(static b =>
{
	_ = b.FromQueue("orders")
		.RouteType<OrderReceived>()
		.ToDispatcher("default");

	_ = b.FromTimer(nameof(OrderArrivalTimer))
		.RouteType<CronTimerTriggerMessage<OrderArrivalTimer>>()
		.ToDispatcher("default");
});

// ----------------------------------------------------------------------------
// 4. Discover handlers in this assembly (OrderReceivedHandler,
//    OrderArrivalHandler). AddDispatch(assembly) auto-wires both action and
//    event handlers.
// ----------------------------------------------------------------------------
builder.Services.AddDispatch(typeof(Program).Assembly);

var host = builder.Build();

// ----------------------------------------------------------------------------
// 5. Start the host — TransportAdapterHostedService materializes the
//    InMemory + CronTimer adapters into the registry; the CronTimer adapter
//    begins ticking against its cron expression.
// ----------------------------------------------------------------------------
await host.StartAsync().ConfigureAwait(false);

var transportRegistry = host.Services.GetRequiredService<ITransportRegistry>();
Console.WriteLine("\nRegistered Transports:");
foreach (var name in transportRegistry.GetTransportNames())
{
	var adapter = transportRegistry.GetTransportAdapter(name);
	Console.WriteLine($"  - {name} (type: {adapter?.TransportType ?? "pending"})");
}

Console.WriteLine();
Console.WriteLine("Cron timer will fire every 10 seconds — watch the logs.");
Console.WriteLine("Press Ctrl+C to stop.");

await host.WaitForShutdownAsync().ConfigureAwait(false);

namespace TransportBindings
{
	/// <summary>
	/// Typed cron-timer marker. Zero-allocation struct; uniqueness of the type
	/// is what distinguishes this timer from any other cron registered in the
	/// host, enabling type-safe handler routing.
	/// </summary>
	public struct OrderArrivalTimer : ICronTimerMarker;

	/// <summary>Message dispatched when an order arrives from the <c>orders</c> queue.</summary>
	public sealed record OrderReceived(string OrderId) : IDispatchAction;

	/// <summary>
	/// Handles <see cref="OrderReceived"/> messages flowing in through the
	/// <c>FromQueue("orders")</c> binding.
	/// </summary>
	public sealed class OrderReceivedHandler : IActionHandler<OrderReceived>
	{
		private readonly ILogger<OrderReceivedHandler> _logger;

		public OrderReceivedHandler(ILogger<OrderReceivedHandler> logger)
		{
			_logger = logger;
		}

		public Task HandleAsync(OrderReceived action, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(action);
			_logger.LogInformation("Order received from queue: {OrderId}", action.OrderId);
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Handles <see cref="CronTimerTriggerMessage{TTimer}"/> for
	/// <see cref="OrderArrivalTimer"/>. Typed-marker routing means this handler
	/// fires only for <c>OrderArrivalTimer</c> — no string-name filtering.
	/// </summary>
	public sealed class OrderArrivalHandler : IEventHandler<CronTimerTriggerMessage<OrderArrivalTimer>>
	{
		private readonly ILogger<OrderArrivalHandler> _logger;

		public OrderArrivalHandler(ILogger<OrderArrivalHandler> logger)
		{
			_logger = logger;
		}

		public Task HandleAsync(
			CronTimerTriggerMessage<OrderArrivalTimer> eventMessage,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(eventMessage);
			_logger.LogInformation(
				"Timer {Timer} fired at {TriggerTime} (cron: {Cron}, tz: {TimeZone})",
				eventMessage.TimerType.Name,
				eventMessage.TriggerTimeUtc,
				eventMessage.CronExpression,
				eventMessage.TimeZone);
			return Task.CompletedTask;
		}
	}
}
