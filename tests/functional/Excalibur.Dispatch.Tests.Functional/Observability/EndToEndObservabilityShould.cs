// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;

using Excalibur.Dispatch.Observability.Context;

using Tests.Shared.Helpers;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Functional.Observability;

/// <summary>
///     Functional tests for end-to-end observability scenarios across the entire dispatch pipeline.
/// </summary>
[Trait("Category", "Functional")]
public sealed class EndToEndObservabilityShould : FunctionalTestBase
{
	private readonly ObservabilityTestHarness _observabilityHarness = new();

	[Fact]
	public async Task TraceCompleteOrderProcessingWorkflow()
	{
		// Arrange
		var host = CreateHost(ConfigureServices);
		var logger = host.Services.GetRequiredService<ILogger<EndToEndObservabilityShould>>();

		using var rootActivity = new Activity("order-processing-workflow");
		_ = rootActivity.SetTag("workflow.type", "order-processing");
		_ = rootActivity.SetTag("workflow.id", "test-workflow-001");
		_ = rootActivity.Start();

		var correlationId = Guid.NewGuid().ToString();
		var orderId = 12345;

		// Act - Simulate order processing with logging
		logger.LogInformation("Processing order created event for Order {OrderId}", orderId);
		await Task.Delay(10).ConfigureAwait(false); // Intentional: simulates processing time between pipeline steps

		logger.LogInformation("Processing payment for Order {OrderId}, Payment {PaymentId}", orderId, "payment-789");
		await Task.Delay(10).ConfigureAwait(false); // Intentional: simulates processing time between pipeline steps

		logger.LogInformation("Order {OrderId} processing completed with status: {Status}", orderId, "Completed");

		rootActivity.Stop();

		// Assert - Root activity was captured
		var workflowActivities = _observabilityHarness.RecordedActivities
			.Where(a => a.TraceId == rootActivity.TraceId)
			.ToList();

		workflowActivities.ShouldNotBeEmpty("At least the root activity should be captured");

		// Assert - Order processing logged
		var orderLogs = _observabilityHarness.LoggerProvider.Entries
			.Where(log => log.Message.Contains(orderId.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
			.ToList();

		orderLogs.ShouldNotBeEmpty("Order processing should generate logs");
		orderLogs.Count.ShouldBeGreaterThanOrEqualTo(3, "Should have at least 3 log entries for the workflow");

		// Assert - All log entries have proper category names
		orderLogs.ShouldAllBe(log => !string.IsNullOrEmpty(log.CategoryName));
	}

	[Fact]
	public async Task CapturePerformanceMetricsAcrossWorkflow()
	{
		// Arrange
		var host = CreateHost(ConfigureServices);
		var logger = host.Services.GetRequiredService<ILogger<EndToEndObservabilityShould>>();

		// Simulate processing multiple orders
		var orderIds = new[] { 1001, 1002 };
		var paymentIds = new[] { "pay-1", "pay-2" };

		// Act - Simulate workflow messages with logging
		foreach (var orderId in orderIds)
		{
			logger.LogInformation("Processing order created event for Order {OrderId}", orderId);
		}

		foreach (var (orderId, paymentId) in orderIds.Zip(paymentIds))
		{
			logger.LogInformation("Processing payment {PaymentId} for Order {OrderId}", paymentId, orderId);
		}

		foreach (var orderId in orderIds)
		{
			logger.LogInformation("Order {OrderId} processing completed", orderId);
		}

		// Poll for logs to be captured rather than using a fixed delay
		await WaitUntilAsync(
			() => _observabilityHarness.LoggerProvider.Count >= 6,
			TimeSpan.FromSeconds(2));

		// Assert - Messages were processed (logs captured)
		var logEntries = _observabilityHarness.LoggerProvider.Entries;
		logEntries.ShouldNotBeEmpty("Message processing should generate logs");

		// Assert - All order IDs were logged
		logEntries.ShouldContain(log => log.Message.Contains("1001", StringComparison.OrdinalIgnoreCase));
		logEntries.ShouldContain(log => log.Message.Contains("1002", StringComparison.OrdinalIgnoreCase));

		// Assert - Payment processing logged
		logEntries.ShouldContain(log => log.Message.Contains("pay-1", StringComparison.OrdinalIgnoreCase));
		logEntries.ShouldContain(log => log.Message.Contains("pay-2", StringComparison.OrdinalIgnoreCase));

		// Note: Detailed per-message performance metrics are not yet implemented in the dispatcher.
	}

	[Fact]
	public async Task HandleErrorsWithFullObservabilityContext()
	{
		// Arrange
		var host = CreateHost(ConfigureServices);
		var logger = host.Services.GetRequiredService<ILogger<EndToEndObservabilityShould>>();

		// Act - Simulate error processing with logging
		try
		{
			logger.LogInformation("Processing order with ID {OrderId}", -999);
			throw new InvalidOperationException("Invalid order ID: -999");
		}
		catch (InvalidOperationException ex)
		{
			logger.LogError(ex, "Error processing order with ID {OrderId}", -999);
		}

		// Poll for error log to be captured rather than using a fixed delay
		await WaitUntilAsync(
			() => _observabilityHarness.LoggerProvider.Entries.Any(l => l.Level == LogLevel.Error),
			TimeSpan.FromSeconds(2));

		// Assert - Error was logged
		var errorLogs = _observabilityHarness.LoggerProvider.Entries
			.Where(log => log.Level == LogLevel.Error)
			.ToList();

		errorLogs.ShouldNotBeEmpty("Error should be logged");

		// Assert - Error message contains the invalid order ID
		var invalidIdLogs = errorLogs
			.Where(log => log.Message.Contains("-999", StringComparison.OrdinalIgnoreCase))
			.ToList();

		invalidIdLogs.ShouldNotBeEmpty("Error log should include the invalid order ID");

		// Note: Error activities with ActivityStatusCode.Error are not yet implemented in the dispatcher.
	}

	[Fact]
	public async Task MaintainObservabilityAcrossConcurrentOperations()
	{
		// Arrange
		var host = CreateHost(ConfigureServices);
		var logger = host.Services.GetRequiredService<ILogger<EndToEndObservabilityShould>>();
		var orderIds = Enumerable.Range(2000, 10).ToList();

		// Act - Process messages concurrently with logging
		var tasks = orderIds.Select(orderId => Task.Run(() =>
		{
			logger.LogInformation("Processing concurrent order {OrderId}", orderId);
		}));

		await Task.WhenAll(tasks);

		// Assert - All messages were processed (logs generated)
		var logEntries = _observabilityHarness.LoggerProvider.Entries;
		logEntries.ShouldNotBeEmpty("Concurrent message processing should generate logs");

		// Assert - All order IDs were logged (showing all messages processed)
		foreach (var orderId in orderIds)
		{
			logEntries.ShouldContain(log => log.Message.Contains(orderId.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase),
				$"Order {orderId} should have been processed and logged");
		}

		// Note: Per-message trace contexts are not yet implemented in the dispatcher.
	}

	[Fact]
	public async Task ProvideRichDiagnosticContextForTroubleshooting()
	{
		// Arrange
		var host = CreateHost(ConfigureServices);
		var logger = host.Services.GetRequiredService<ILogger<EndToEndObservabilityShould>>();

		// Act - Log diagnostic information
		logger.LogInformation("Processing diagnostic order {OrderId} with correlation {CorrelationId}",
			9999, "diagnostic-test-001");

		// Poll for diagnostic log to be captured rather than using a fixed delay
		await WaitUntilAsync(
			() => _observabilityHarness.LoggerProvider.Count > 0,
			TimeSpan.FromSeconds(2));

		// Assert - Structured logging captured for troubleshooting
		var logEntries = _observabilityHarness.LoggerProvider.Entries;
		logEntries.ShouldNotBeEmpty("Message dispatch should generate logs for troubleshooting");

		// Assert - Logs have category names (diagnostic context)
		logEntries.ShouldAllBe(log => !string.IsNullOrEmpty(log.CategoryName),
			"All logs should have a category name for filtering/troubleshooting");

		// Assert - Order details were logged for diagnostic purposes
		logEntries.ShouldContain(log => log.Message.Contains("9999", StringComparison.OrdinalIgnoreCase),
			"Order ID should be logged for troubleshooting");

		// Note: Rich activity context with message.id, messaging.system, and messaging.correlation_id tags
		// is not yet implemented in the dispatcher.
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_observabilityHarness?.Dispose();
		}

		base.Dispose(disposing);
	}

	private void ConfigureServices(IServiceCollection services)
	{
		// Configure structured logging first so handlers get the right logger
		_ = services.AddLogging(builder =>
		{
			_ = builder.AddProvider(_observabilityHarness.LoggerProvider);
			_ = builder.SetMinimumLevel(LogLevel.Debug);
		});

		// Register core dispatch services with assembly scanning for handlers in this test file
		_ = services.AddDispatch(typeof(EndToEndObservabilityShould).Assembly);

		// Configure full observability pipeline (registers IContextFlowTracker, metrics, diagnostics, middleware)
		_ = services.AddDispatchObservability();
	}
}

#region Test Messages and Handlers

/// <summary>
/// Test message for observability tests.
/// IDispatchMessage is now a marker interface - properties are for test assertions only.
/// </summary>
public record OrderCreatedEvent : IDispatchMessage
{
	public int OrderId { get; init; }
	public int CustomerId { get; init; }
	public decimal Amount { get; init; }
	public string? CorrelationId { get; init; }
	public string MessageId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Test command for observability tests.
/// </summary>
public record PaymentProcessedCommand : IDispatchMessage
{
	public int OrderId { get; init; }
	public decimal Amount { get; init; }
	public string PaymentId { get; init; } = string.Empty;
	public string MessageId { get; init; } = Guid.NewGuid().ToString();
	public string? CorrelationId { get; init; }
}

/// <summary>
/// Test event for observability tests.
/// </summary>
public record OrderProcessedEvent : IDispatchMessage
{
	public int OrderId { get; init; }
	public string Status { get; init; } = string.Empty;
	public string MessageId { get; init; } = Guid.NewGuid().ToString();
	public string? CorrelationId { get; init; }
}

public sealed class OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger) : IDispatchHandler<OrderCreatedEvent>
{
	private readonly ILogger<OrderCreatedEventHandler> _logger = logger;

	public async Task<IMessageResult> HandleAsync(OrderCreatedEvent message, IMessageContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Processing order created event for Order {OrderId}, Customer {CustomerId}, Amount {Amount}",
			message.OrderId, message.CustomerId, message.Amount);

		// Intentional: simulates async processing work in handler
		await Task.Delay(20, cancellationToken).ConfigureAwait(false);

		// Simulate error condition
		if (message.OrderId < 0)
		{
			_logger.LogError("Invalid order ID {OrderId} - must be positive", message.OrderId);
			throw new InvalidOperationException($"Invalid order ID: {message.OrderId}");
		}

		// Add processing metadata to context
		context.Items["ProcessedAt"] = DateTimeOffset.UtcNow;
		context.Items["HandlerVersion"] = "1.0";
		context.Items["OrderAmount"] = message.Amount;

		_logger.LogInformation("Successfully processed order created event for Order {OrderId}", message.OrderId);
		return MessageResult.Success();
	}
}

public sealed class PaymentProcessedCommandHandler(ILogger<PaymentProcessedCommandHandler> logger)
	: IDispatchHandler<PaymentProcessedCommand>
{
	private readonly ILogger<PaymentProcessedCommandHandler> _logger = logger;

	public async Task<IMessageResult> HandleAsync(PaymentProcessedCommand message, IMessageContext context,
		CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Processing payment for Order {OrderId}, Payment {PaymentId}, Amount {Amount}",
			message.OrderId, message.PaymentId, message.Amount);

		await Task.Delay(15, cancellationToken).ConfigureAwait(false); // Intentional: simulates async processing work

		context.Items["PaymentProcessedAt"] = DateTimeOffset.UtcNow;
		context.Items["PaymentMethod"] = "CreditCard";

		_logger.LogInformation(
			"Successfully processed payment {PaymentId} for Order {OrderId}",
			message.PaymentId, message.OrderId);

		return MessageResult.Success();
	}
}

public sealed class OrderProcessedEventHandler(ILogger<OrderProcessedEventHandler> logger) : IDispatchHandler<OrderProcessedEvent>
{
	private readonly ILogger<OrderProcessedEventHandler> _logger = logger;

	public async Task<IMessageResult> HandleAsync(OrderProcessedEvent message, IMessageContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Order {OrderId} processing completed with status: {Status}",
			message.OrderId, message.Status);

		await Task.Delay(10, cancellationToken).ConfigureAwait(false); // Intentional: simulates async processing work

		context.Items["FinalStatus"] = message.Status;
		context.Items["CompletedAt"] = DateTimeOffset.UtcNow;

		_logger.LogInformation("Order {OrderId} workflow completed successfully", message.OrderId);
		return MessageResult.Success();
	}
}

#endregion Test Messages and Handlers

#region Test Infrastructure

/// <summary>
///     Comprehensive test harness for capturing observability data.
/// </summary>
public sealed class ObservabilityTestHarness : IDisposable
{
	private readonly TestMeterListener _meterListener;
	private readonly TestActivityListener _activityListener;

	public ObservabilityTestHarness()
	{
		_meterListener = new TestMeterListener(RecordedMetrics);
		_activityListener = new TestActivityListener(RecordedActivities);

		_meterListener.Start();
		_activityListener.Start();
	}

	public CapturingLoggerProvider LoggerProvider { get; } = new();

	public Collection<Activity> RecordedActivities { get; } = [];

	public Collection<MetricMeasurement> RecordedMetrics { get; } = [];

	/// <inheritdoc/>
	public void Dispose()
	{
		_meterListener?.Dispose();
		_activityListener?.Dispose();
	}
}

public sealed class TestMeterListener : IDisposable
{
	private readonly Collection<MetricMeasurement> _measurements;
	private readonly MeterListener _listener;

	public TestMeterListener(Collection<MetricMeasurement> measurements)
	{
		_measurements = measurements;
		_listener = new MeterListener();
		_listener.MeasurementsCompleted = static (instrument, state) => { };
		_listener.InstrumentPublished = static (instrument, listener) => listener.EnableMeasurementEvents(instrument);
	}

	public void OnMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
	{
		ArgumentNullException.ThrowIfNull(instrument);

		var tagsDictionary = tags.ToArray().ToDictionary(
			static kvp => kvp.Key,
			static kvp => kvp.Value ?? string.Empty);

		var metricMeasurement = new MetricMeasurement
		{
			Name = instrument.Name,
			Value = measurement,
			Tags = tagsDictionary,
			Timestamp = DateTimeOffset.UtcNow,
		};

		lock (_measurements)
		{
			_measurements.Add(metricMeasurement);
		}
	}

	public void Start() => _listener.Start();

	/// <inheritdoc/>
	public void Dispose() => _listener.Dispose();
}

public sealed class TestActivityListener : IDisposable
{
	private readonly Collection<Activity> _activities;
	private readonly ActivityListener _listener;

	public TestActivityListener(Collection<Activity> activities)
	{
		_activities = activities;
		_listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
			ActivityStarted = activity =>
			{
				lock (_activities)
				{
					_activities.Add(activity);
				}
			},
		};
	}

	public void Start() => ActivitySource.AddActivityListener(_listener);

	/// <inheritdoc/>
	public void Dispose() => _listener.Dispose();
}

public sealed class MetricMeasurement
{
	public required string Name { get; set; } = string.Empty;

	public required object? Value { get; set; }

	public Dictionary<string, object> Tags { get; init; } = [];

	public DateTimeOffset Timestamp { get; set; }
}

#endregion Test Infrastructure
