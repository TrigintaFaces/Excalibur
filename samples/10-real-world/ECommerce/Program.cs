// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Trace;

namespace Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample;

/// <summary>
/// E-Commerce Order Processing Sample Application demonstrating enhanced stores in a realistic business scenario with message
/// deduplication, batch processing, scheduling, and comprehensive observability.
/// </summary>
/// <remarks>
/// This sample application demonstrates:
/// <list type="bullet">
/// <item> Order processing with enhanced inbox deduplication </item>
/// <item> Batch email notifications with enhanced outbox </item>
/// <item> Scheduled inventory checks with enhanced schedule store </item>
/// <item> Real-time performance monitoring and alerting </item>
/// <item> Health checks and graceful degradation </item>
/// </list>
/// </remarks>
public static class Program
{
	public static async Task Main(string[] args)
	{
		Console.WriteLine("🛒 E-Commerce Enhanced Stores Sample");
		Console.WriteLine("=====================================");
		Console.WriteLine();

		var host = CreateHostBuilder(args).Build();

		// Start the application services
		Console.WriteLine("🚀 Starting e-commerce order processing system...");
		await host.StartAsync().ConfigureAwait(false);

		Console.WriteLine("✅ System started successfully!");
		Console.WriteLine();
		Console.WriteLine("📊 Monitoring Dashboard:");
		Console.WriteLine("   - Order Processing: Enhanced Inbox Store with deduplication");
		Console.WriteLine("   - Email Notifications: Enhanced Outbox Store with batching");
		Console.WriteLine("   - Inventory Checks: Enhanced Schedule Store with execution tracking");
		Console.WriteLine();
		Console.WriteLine("🔄 Processing sample orders...");

		// Generate sample workload
		var workloadGenerator = host.Services.GetRequiredService<WorkloadGenerator>();
		await workloadGenerator.GenerateSampleWorkloadAsync().ConfigureAwait(false);

		Console.WriteLine();
		Console.WriteLine("📈 Performance metrics and alerts are being collected...");
		Console.WriteLine("Press 'q' to quit, 'm' for metrics, 'h' for health status");

		// Interactive console monitoring
		var keyTask = Task.Run(async () =>
		{
			while (true)
			{
				var key = Console.ReadKey(true);
				switch (key.KeyChar)
				{
					case 'q':
					case 'Q':
						return;

					case 'm':
					case 'M':
						await ShowMetrics(host.Services).ConfigureAwait(false);
						break;

					case 'h':
					case 'H':
						await ShowHealthStatus(host.Services).ConfigureAwait(false);
						break;
				}
			}
		});

		await keyTask.ConfigureAwait(false);

		Console.WriteLine();
		Console.WriteLine("🛑 Shutting down gracefully...");
		await host.StopAsync().ConfigureAwait(false);

		Console.WriteLine("✅ Application shutdown complete.");
	}

	private static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureServices(static (context, services) =>
			{
				// Configure enhanced stores with production settings
				ConfigureEnhancedStores(services);

				// Configure observability
				ConfigureObservability(services);

				// Register business services
				_ = services.AddScoped<OrderProcessingService>();
				_ = services.AddScoped<NotificationService>();
				_ = services.AddScoped<InventoryService>();

				// Register sample infrastructure
				_ = services.AddSingleton<InMemoryOrderRepository>();
				_ = services.AddSingleton<InMemoryEmailService>();
				_ = services.AddSingleton<InMemoryInventoryRepository>();

				// Register hosted services
				_ = services.AddSingleton<WorkloadGenerator>();
				_ = services.AddHostedService<OrderProcessorHostedService>();
				_ = services.AddHostedService<NotificationProcessorHostedService>();
				_ = services.AddHostedService<InventoryCheckProcessor>();
				_ = services.AddHostedService<MetricsReportingService>();
			})
			.ConfigureLogging(static logging =>
			{
				_ = logging.ClearProviders();
				_ = logging.AddConsole();
				_ = logging.SetMinimumLevel(LogLevel.Information);
			});

	private static void ConfigureEnhancedStores(IServiceCollection services)
	{
		// Configure telemetry provider
		_ = services.AddDispatchTelemetry(static options =>
		{
			options.ServiceName = "ECommerce.OrderProcessing";
			options.ServiceVersion = "1.0.0";
			options.EnableEnhancedStoreObservability = true;
			options.EnableMetrics = true;
			options.EnableTracing = true;
		});

		// Add in-memory store implementations for the sample
		_ = services.AddSingleton<IInboxStore, InMemoryInboxStore>();
		_ = services.AddSingleton<IOutboxStore, InMemoryOutboxStore>();
		_ = services.AddSingleton<IScheduleStore, InMemoryScheduleStore>();

		// TODO: Enhanced store configurations commented out - extension methods don't exist yet When enhanced store extensions are
		// implemented, uncomment and configure:
		/*
		services.AddEnhancedInboxStore(static options =>
		{
			options.EnableAdvancedDeduplication = true;
			options.EnableContentBasedDeduplication = true;
			options.DeduplicationCacheSize = 50000;
			options.ContentDeduplicationWindow = TimeSpan.FromMinutes(30);
			options.MaxConcurrentOperations = 200;
		});

		services.AddEnhancedOutboxStore(static options =>
		{
			options.EnableBatchStaging = true;
			options.EnableExponentialBackoff = true;
			options.StagingBatchSize = 50;
			options.MaxRetryAttempts = 5;
			options.BaseRetryDelay = TimeSpan.FromSeconds(2);
			options.MaxRetryDelay = TimeSpan.FromMinutes(10);
		});

		services.AddEnhancedScheduleStore(static options =>
		{
			options.EnableDuplicateDetection = true;
			options.EnableExecutionTimeIndexing = true;
			options.EnableBatchOperations = true;
			options.ScheduleCacheSize = 25000;
			options.BatchSize = 100;
			options.DuplicateDetectionWindow = TimeSpan.FromMinutes(15);
		});
		*/
	}

	private static void ConfigureObservability(IServiceCollection services)
	{
		_ = services.AddOpenTelemetry()
			.WithTracing(static builder => builder
				.AddSource(DispatchTelemetryConstants.ActivitySources.Core)
				.AddSource(DispatchTelemetryConstants.ActivitySources.Pipeline)
				.AddSource(DispatchTelemetryConstants.ActivitySources.TimePolicy)
				.AddSource("ECommerce.OrderProcessing")
				.AddConsoleExporter())
			.WithMetrics(static builder => builder
				.AddMeter(DispatchTelemetryConstants.Meters.Core)
				.AddMeter(DispatchTelemetryConstants.Meters.Pipeline)
				.AddMeter(DispatchTelemetryConstants.Meters.TimePolicy)
				.AddMeter("ECommerce.OrderProcessing"));

		// Add health checks
		_ = services.AddHealthChecks()
			.AddCheck<EnhancedStoreHealthCheck>("enhanced-stores")
			.AddCheck<BusinessLogicHealthCheck>("business-logic");

		// Add custom monitoring
		_ = services.AddSingleton<PerformanceMonitor>();
	}

	private static async Task ShowMetrics(IServiceProvider services)
	{
		var monitor = services.GetRequiredService<PerformanceMonitor>();
		var metrics = await monitor.GetCurrentMetricsAsync().ConfigureAwait(false);

		Console.WriteLine();
		Console.WriteLine("📊 Current Performance Metrics:");
		Console.WriteLine($"   Orders Processed: {metrics.OrdersProcessed}");
		Console.WriteLine($"   Duplicates Detected: {metrics.DuplicatesDetected}");
		Console.WriteLine($"   Emails Queued: {metrics.EmailsQueued}");
		Console.WriteLine($"   Inventory Checks Scheduled: {metrics.InventoryChecksScheduled}");
		Console.WriteLine($"   Average Processing Time: {metrics.AverageProcessingTime:F2}ms");
		Console.WriteLine($"   Cache Hit Rate: {metrics.CacheHitRate:P}");
		Console.WriteLine();
	}

	private static async Task ShowHealthStatus(IServiceProvider services)
	{
		var healthCheck = services.GetRequiredService<EnhancedStoreHealthCheck>();
		var context = new HealthCheckContext();
		var healthResult = await healthCheck.CheckHealthAsync(context).ConfigureAwait(false);

		Console.WriteLine();
		Console.WriteLine("🏥 System Health Status:");
		Console.WriteLine($"   Overall: {healthResult.Status}");
		Console.WriteLine($"   Description: {healthResult.Description}");
		if (healthResult.Exception != null)
		{
			Console.WriteLine($"   Error: {healthResult.Exception.Message}");
		}

		Console.WriteLine();
	}
}

/// <summary>
/// Generates realistic e-commerce workload for demonstration purposes.
/// </summary>
public sealed partial class WorkloadGenerator(
	OrderProcessingService orderService,
	NotificationService notificationService,
	InventoryService inventoryService,
	ILogger<WorkloadGenerator> logger)
{
	private readonly OrderProcessingService _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));

	private readonly NotificationService _notificationService =
		notificationService ?? throw new ArgumentNullException(nameof(notificationService));

	private readonly InventoryService _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
	private readonly ILogger<WorkloadGenerator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task GenerateSampleWorkloadAsync()
	{
		LogGeneratingWorkload();

		// Generate sample orders with some duplicates to test deduplication
		var orderIds = new[]
		{
			"ORD-2025-001", "ORD-2025-002", "ORD-2025-003", "ORD-2025-004", "ORD-2025-005",
			"ORD-2025-001", // Duplicate to test deduplication
			"ORD-2025-006", "ORD-2025-007", "ORD-2025-008", "ORD-2025-009", "ORD-2025-010", "ORD-2025-002" // Another duplicate
		};

		var customers = new[]
		{
			"customer-alice@example.com", "customer-bob@example.com", "customer-charlie@example.com", "customer-diana@example.com",
			"customer-eve@example.com"
		};

		var products = new[]
		{
			("laptop-pro-15", """
			                  Laptop Pro 15"
			                  """, 1299.99m),
			("wireless-mouse", "Wireless Gaming Mouse", 79.99m), ("mechanical-keyboard", "Mechanical Keyboard", 149.99m),
			("usb-c-hub", "USB-C Hub 8-in-1", 59.99m), ("monitor-4k-27", "27\" 4K Monitor", 449.99m)
		};

		// Process orders
		var orderTasks = orderIds.Select(async (orderId, index) =>
		{
			var customer = customers[index % customers.Length];
			var (productId, productName, price) = products[index % products.Length];

			var order = new OrderCreated
			{
				OrderId = orderId,
				CustomerId = customer,
				ProductId = productId,
				ProductName = productName,
				Price = price,
				Quantity = RandomNumberGenerator.GetInt32(1, 4),
				OrderDate = DateTimeOffset.UtcNow.AddSeconds(-RandomNumberGenerator.GetInt32(0, 3600))
			};

			await _orderService.ProcessOrderAsync(order).ConfigureAwait(false);
		});

		await Task.WhenAll(orderTasks).ConfigureAwait(false);

		// Schedule inventory checks
		var inventoryTasks = products.Select(async product =>
		{
			await _inventoryService.ScheduleInventoryCheckAsync(
				product.Item1,
				DateTimeOffset.UtcNow.AddMinutes(RandomNumberGenerator.GetInt32(5, 30))).ConfigureAwait(false);
		});

		await Task.WhenAll(inventoryTasks).ConfigureAwait(false);

		// Queue notification emails
		var notificationTasks = customers.Select(async customer =>
		{
			await _notificationService.QueueWelcomeEmailAsync(customer).ConfigureAwait(false);
			await _notificationService.QueuePromotionalEmailAsync(customer, "Spring Sale - 20% Off!").ConfigureAwait(false);
		});

		await Task.WhenAll(notificationTasks).ConfigureAwait(false);

		LogWorkloadComplete();
	}

	[LoggerMessage(1001, LogLevel.Information, "🔄 Generating sample e-commerce workload...")]
	private partial void LogGeneratingWorkload();

	[LoggerMessage(1002, LogLevel.Information, "✅ Sample workload generation complete")]
	private partial void LogWorkloadComplete();
}

/// <summary>
/// Performance monitoring service for tracking metrics across enhanced stores.
/// </summary>
public sealed class PerformanceMonitor
{
	private readonly List<double> _processingTimes = [];
	private readonly double _cacheHitRate = 0.85;
	private long _ordersProcessed;
	private long _duplicatesDetected;
	private long _emailsQueued;
	private long _inventoryChecksScheduled;
	// Simulated cache hit rate

	public Task<PerformanceMetrics> GetCurrentMetricsAsync()
	{
		var metrics = new PerformanceMetrics
		{
			OrdersProcessed = _ordersProcessed,
			DuplicatesDetected = _duplicatesDetected,
			EmailsQueued = _emailsQueued,
			InventoryChecksScheduled = _inventoryChecksScheduled,
			AverageProcessingTime = _processingTimes.Count != 0 ? _processingTimes.Average() : 0,
			CacheHitRate = _cacheHitRate
		};

		return Task.FromResult(metrics);
	}

	public void RecordOrderProcessed(double processingTimeMs)
	{
		_ = Interlocked.Increment(ref _ordersProcessed);
		lock (_processingTimes)
		{
			_processingTimes.Add(processingTimeMs);
			if (_processingTimes.Count > 100) // Keep last 100 measurements
			{
				_processingTimes.RemoveAt(0);
			}
		}
	}

	public void RecordDuplicateDetected() => Interlocked.Increment(ref _duplicatesDetected);

	public void RecordEmailQueued() => Interlocked.Increment(ref _emailsQueued);

	public void RecordInventoryCheckScheduled() => Interlocked.Increment(ref _inventoryChecksScheduled);
}

public sealed record PerformanceMetrics
{
	public long OrdersProcessed { get; init; }
	public long DuplicatesDetected { get; init; }
	public long EmailsQueued { get; init; }
	public long InventoryChecksScheduled { get; init; }
	public double AverageProcessingTime { get; init; }
	public double CacheHitRate { get; init; }
}

// Message definitions for the e-commerce sample

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

public sealed record InventoryCheck
{
	public required string ProductId { get; init; }
	public DateTimeOffset ScheduledFor { get; init; }
	public required string CheckType { get; init; }
}

public sealed record ScheduledInventoryCheck
{
	public required string ScheduleId { get; init; }
	public required string ProductId { get; init; }
	public DateTimeOffset ExecuteAt { get; init; }
	public required string CheckType { get; init; }
}
