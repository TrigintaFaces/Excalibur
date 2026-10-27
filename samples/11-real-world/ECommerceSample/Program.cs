// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;
using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

/// <summary>
/// E-commerce order processing sample. Every store this sample talks to is a shipping Excalibur store, and
/// every path it advertises is executed by the run: orders are deduplicated through the inbox, confirmation
/// e-mails are staged in the outbox and drained by a background worker that retries a transient send failure,
/// and inventory checks are scheduled and then executed from the schedule store.
/// </summary>
/// <remarks>
/// <para>
/// The run is a self-checking scenario rather than a demo loop. It submits a fixed workload, waits for the
/// background workers to quiesce, then asserts the observable outcome: which orders were persisted, how many
/// duplicates the inbox suppressed, how many notifications were sent, how many send retries were needed, and
/// how many scheduled inventory checks executed against the product recorded in the schedule payload. The
/// process exits non-zero if any assertion fails, so the sample cannot quietly decay into a program that
/// starts, prints encouraging messages, and exercises nothing.
/// </para>
/// <para>Pass <c>--trace</c> to also emit OpenTelemetry spans and metrics to the console.</para>
/// </remarks>
public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		ArgumentNullException.ThrowIfNull(args);

		var emitTelemetryToConsole = args.Contains("--trace", StringComparer.Ordinal);

		Console.WriteLine("E-Commerce Order Processing Sample");
		Console.WriteLine("==================================");
		Console.WriteLine();

		var host = CreateHostBuilder(args, emitTelemetryToConsole).Build();
		await host.StartAsync().ConfigureAwait(false);

		OrderScenarioReport report;
		try
		{
			var scenario = host.Services.GetRequiredService<OrderScenario>();
			report = await scenario.RunAsync(CancellationToken.None).ConfigureAwait(false);
		}
		finally
		{
			await host.StopAsync().ConfigureAwait(false);
		}

		Console.WriteLine();
		report.Write(Console.Out);

		return report.Passed ? 0 : 1;
	}

	private static IHostBuilder CreateHostBuilder(string[] args, bool emitTelemetryToConsole) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureServices((context, services) =>
			{
				ConfigureStores(services);
				ConfigureObservability(services, emitTelemetryToConsole);

				_ = services.AddSingleton<OrderProcessingService>();
				_ = services.AddSingleton<NotificationService>();
				_ = services.AddSingleton<InventoryService>();

				_ = services.AddSingleton<InMemoryOrderRepository>();
				_ = services.AddSingleton<InMemoryEmailService>();
				_ = services.AddSingleton<InMemoryInventoryRepository>();

				_ = services.AddSingleton<OrderScenario>();
				_ = services.AddHostedService<NotificationDrainService>();
				_ = services.AddHostedService<InventoryCheckProcessor>();
			})
			.ConfigureLogging(static logging =>
			{
				_ = logging.ClearProviders();
				_ = logging.AddConsole();
				_ = logging.SetMinimumLevel(LogLevel.Information);
			});

	private static void ConfigureStores(IServiceCollection services)
	{
		_ = services.AddDispatchTelemetry(static options =>
		{
			options.ServiceName = "ECommerce.OrderProcessing";
			options.ServiceVersion = "1.0.0";
			options.EnableMetrics = true;
			options.EnableTracing = true;
		});

		// The shipping stores, composed through the canonical builder entry points. Only the provider line
		// changes for a persistent deployment -- swap UseInMemory() for UseSqlServer(...) / UsePostgres(...)
		// and nothing else here or below moves, because this sample only ever talks to IInboxStore,
		// IOutboxStore and IScheduleStore.
		_ = services.AddExcaliburInbox(static inbox => inbox.UseInMemory());
		_ = services.AddExcalibur(static excalibur => excalibur.AddOutbox(static outbox => outbox.UseInMemory()));

		// The drain below polls faster than the framework's default outbox poll interval, and the failure
		// backoff floor must stay above whichever poll interval is in force or a failed message would be
		// re-claimed on the very next pass -- the retry hot-loop the floor exists to prevent. Both are stated
		// here so the retry in this sample settles in seconds rather than the production-shaped default.
		_ = services.Configure<OutboxProcessingOptions>(static options => options.PollingInterval = TimeSpan.FromSeconds(1));
		_ = services.Configure<InMemoryOutboxOptions>(static options => options.FailureBackoffFloorSeconds = 2);

		// Registers the in-memory IScheduleStore along with the rest of the scheduling infrastructure.
		_ = services.AddDispatchScheduling();
	}

	private static void ConfigureObservability(IServiceCollection services, bool emitTelemetryToConsole)
	{
		_ = services.AddOpenTelemetry()
			.WithTracing(builder =>
			{
				_ = builder
					.AddSource("Excalibur.Dispatch.Core")
					.AddSource("Excalibur.Dispatch.Pipeline")
					.AddSource("ECommerce.OrderProcessing")
					.AddSource("ECommerce.Inventory");

				if (emitTelemetryToConsole)
				{
					_ = builder.AddConsoleExporter();
				}
			})
			.WithMetrics(builder =>
			{
				_ = builder
					.AddMeter("Excalibur.Dispatch.Core")
					.AddMeter("Excalibur.Dispatch.Pipeline")
					.AddMeter("ECommerce.OrderProcessing");

				if (emitTelemetryToConsole)
				{
					_ = builder.AddConsoleExporter();
				}
			});

		_ = services.AddHealthChecks()
			.AddCheck<StoreHealthCheck>("dispatch-stores")
			.AddCheck<BusinessLogicHealthCheck>("business-logic");

		_ = services.AddSingleton<PerformanceMonitor>();
	}
}

/// <summary>
/// Counters recorded by the services and workers, and read back by the scenario assertions.
/// </summary>
public sealed class PerformanceMonitor
{
	private long _ordersProcessed;
	private long _ordersFailed;
	private long _duplicatesSuppressed;
	private long _notificationsStaged;
	private long _notificationsSent;
	private long _notificationSendRetries;
	private long _inventoryChecksScheduled;
	private long _inventoryChecksExecuted;

	public long OrdersProcessed => Interlocked.Read(ref _ordersProcessed);

	public long OrdersFailed => Interlocked.Read(ref _ordersFailed);

	public long DuplicatesSuppressed => Interlocked.Read(ref _duplicatesSuppressed);

	public long NotificationsStaged => Interlocked.Read(ref _notificationsStaged);

	public long NotificationsSent => Interlocked.Read(ref _notificationsSent);

	public long NotificationSendRetries => Interlocked.Read(ref _notificationSendRetries);

	public long InventoryChecksScheduled => Interlocked.Read(ref _inventoryChecksScheduled);

	public long InventoryChecksExecuted => Interlocked.Read(ref _inventoryChecksExecuted);

	public void RecordOrderProcessed() => Interlocked.Increment(ref _ordersProcessed);

	public void RecordOrderFailed() => Interlocked.Increment(ref _ordersFailed);

	public void RecordDuplicateSuppressed() => Interlocked.Increment(ref _duplicatesSuppressed);

	public void RecordNotificationStaged() => Interlocked.Increment(ref _notificationsStaged);

	public void RecordNotificationSent() => Interlocked.Increment(ref _notificationsSent);

	public void RecordNotificationSendRetry() => Interlocked.Increment(ref _notificationSendRetries);

	public void RecordInventoryCheckScheduled() => Interlocked.Increment(ref _inventoryChecksScheduled);

	public void RecordInventoryCheckExecuted() => Interlocked.Increment(ref _inventoryChecksExecuted);
}

/// <summary>
/// The activity sources this sample emits under. Held statically: an <see cref="ActivitySource"/> lives for
/// the life of the process, so giving one a per-instance lifetime would make every holder disposable for no
/// gain.
/// </summary>
internal static class SampleTelemetry
{
	public static readonly ActivitySource Orders = new("ECommerce.OrderProcessing");

	public static readonly ActivitySource Inventory = new("ECommerce.Inventory");
}

// Message definitions for the e-commerce sample.

public sealed record OrderCreated
{
	public required string OrderId { get; init; }
	public required string CustomerId { get; init; }
	public required string ProductId { get; init; }
	public required string ProductName { get; init; }
	public required decimal Price { get; init; }
	public required int Quantity { get; init; }
	public DateTimeOffset OrderDate { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EmailNotification
{
	public required string ToEmail { get; init; }
	public required string Subject { get; init; }
	public required string Body { get; init; }
	public required string NotificationType { get; init; }
	public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// The payload persisted in the schedule store and read back by the worker that executes the check. The
/// worker deserializes this record from the stored message body; it does not reconstruct the product from
/// the schedule identifier, which carries no business meaning.
/// </summary>
public sealed record ScheduledInventoryCheck
{
	public required string ProductId { get; init; }
	public required string CheckType { get; init; }
	public DateTimeOffset ExecuteAt { get; init; }
}
