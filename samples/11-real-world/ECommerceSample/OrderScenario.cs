// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

/// <summary>
/// Submits a fixed workload and then asserts what the stores and repositories actually contain.
/// </summary>
/// <remarks>
/// The workload is deliberately fixed rather than randomised: every expected number below is a constant, so a
/// path that stops executing changes an assertion from pass to fail instead of merely changing a log line.
/// </remarks>
public sealed partial class OrderScenario(
	OrderProcessingService orderService,
	InventoryService inventoryService,
	InMemoryOrderRepository orderRepository,
	InMemoryInventoryRepository inventoryRepository,
	InMemoryEmailService emailService,
	IInboxStore inboxStore,
	IOutboxStore outboxStore,
	IScheduleStore scheduleStore,
	PerformanceMonitor monitor,
	ILogger<OrderScenario> logger)
{
	/// <summary>The orders the scenario submits successfully. Each one is submitted once.</summary>
	private static readonly OrderCreated[] ValidOrders =
	[
		NewOrder("ORD-2026-001", "alice@example.com", "laptop-pro-15", "Laptop Pro 15\"", 1299.99m, 1),
		NewOrder("ORD-2026-002", "bob@example.com", "wireless-mouse", "Wireless Gaming Mouse", 79.99m, 3),
		NewOrder("ORD-2026-003", FlakyRecipient, "mechanical-keyboard", "Mechanical Keyboard", 149.99m, 2),
		NewOrder("ORD-2026-004", "diana@example.com", "usb-c-hub", "USB-C Hub 8-in-1", 59.99m, 4),
		NewOrder("ORD-2026-005", "eve@example.com", "monitor-4k-27", "27\" 4K Monitor", 449.99m, 1)
	];

	/// <summary>Order identifiers submitted a second time, to be suppressed by the inbox.</summary>
	private static readonly string[] ResubmittedOrderIds = ["ORD-2026-001", "ORD-2026-002"];

	/// <summary>The order that fails validation, so the inbox failure path is exercised by the run.</summary>
	private static readonly OrderCreated InvalidOrder =
		NewOrder("ORD-2026-006", "frank@example.com", "usb-c-hub", "USB-C Hub 8-in-1", 59.99m, 0);

	/// <summary>Products whose inventory check the scenario schedules for immediate execution.</summary>
	private static readonly string[] ScheduledProducts = ["laptop-pro-15", "usb-c-hub", "monitor-4k-27"];

	/// <summary>
	/// The first delivery attempt to this address fails, so the outbox retry path is exercised by the run
	/// rather than merely described by it.
	/// </summary>
	internal const string FlakyRecipient = "charlie@example.com";

	private const int ExpectedOrdersProcessed = 5;
	private const int ExpectedDuplicatesSuppressed = 2;
	private const int ExpectedOrdersFailed = 1;
	private const int ExpectedNotifications = 5;
	private const int ExpectedInventoryChecks = 3;

	private static readonly TimeSpan QuiesceTimeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	private readonly OrderProcessingService _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
	private readonly InventoryService _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));

	private readonly InMemoryOrderRepository _orderRepository =
		orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));

	private readonly InMemoryInventoryRepository _inventoryRepository =
		inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));

	private readonly InMemoryEmailService _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
	private readonly IInboxStore _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
	private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
	private readonly IScheduleStore _scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));
	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<OrderScenario> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task<OrderScenarioReport> RunAsync(CancellationToken cancellationToken)
	{
		_emailService.FailFirstAttemptFor(FlakyRecipient);

		LogSubmittingOrders(ValidOrders.Length);
		foreach (var order in ValidOrders)
		{
			_ = await _orderService.ProcessOrderAsync(order, cancellationToken).ConfigureAwait(false);
		}

		LogResubmittingOrders(ResubmittedOrderIds.Length);
		foreach (var orderId in ResubmittedOrderIds)
		{
			var original = Array.Find(ValidOrders, o => string.Equals(o.OrderId, orderId, StringComparison.Ordinal))!;
			_ = await _orderService.ProcessOrderAsync(original, cancellationToken).ConfigureAwait(false);
		}

		LogSubmittingInvalidOrder();
		_ = await _orderService.ProcessOrderAsync(InvalidOrder, cancellationToken).ConfigureAwait(false);

		LogSchedulingInventoryChecks(ScheduledProducts.Length);
		foreach (var productId in ScheduledProducts)
		{
			await _inventoryService.ScheduleInventoryCheckAsync(productId, DateTimeOffset.UtcNow, cancellationToken)
				.ConfigureAwait(false);
		}

		var quiesced = await WaitForQuiescenceAsync(cancellationToken).ConfigureAwait(false);

		return await AssertAsync(quiesced, cancellationToken).ConfigureAwait(false);
	}

	private static OrderCreated NewOrder(
		string orderId,
		string customerId,
		string productId,
		string productName,
		decimal price,
		int quantity) =>
		new()
		{
			OrderId = orderId,
			CustomerId = customerId,
			ProductId = productId,
			ProductName = productName,
			Price = price,
			Quantity = quantity,
			OrderDate = DateTimeOffset.UtcNow
		};

	[LoggerMessage(2001, LogLevel.Information, "Submitting {OrderCount} orders")]
	private partial void LogSubmittingOrders(int orderCount);

	[LoggerMessage(2002, LogLevel.Information, "Resubmitting {OrderCount} orders to exercise inbox deduplication")]
	private partial void LogResubmittingOrders(int orderCount);

	[LoggerMessage(2003, LogLevel.Information, "Submitting one invalid order to exercise the inbox failure path")]
	private partial void LogSubmittingInvalidOrder();

	[LoggerMessage(2004, LogLevel.Information, "Scheduling {CheckCount} inventory checks")]
	private partial void LogSchedulingInventoryChecks(int checkCount);

	[LoggerMessage(2005, LogLevel.Warning, "Background work did not quiesce within {TimeoutSeconds}s")]
	private partial void LogQuiesceTimedOut(double timeoutSeconds);

	/// <summary>
	/// Waits until the background workers have nothing left to do, or the deadline elapses. Polling an
	/// observable condition keeps the run deterministic on a slow machine without pinning it to a fixed sleep.
	/// </summary>
	private async Task<bool> WaitForQuiescenceAsync(CancellationToken cancellationToken)
	{
		var deadline = DateTimeOffset.UtcNow + QuiesceTimeout;

		while (DateTimeOffset.UtcNow < deadline)
		{
			if (_monitor.NotificationsSent >= ExpectedNotifications &&
				_monitor.InventoryChecksExecuted >= ExpectedInventoryChecks)
			{
				return true;
			}

			await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
		}

		LogQuiesceTimedOut(QuiesceTimeout.TotalSeconds);
		return false;
	}

	private async Task<OrderScenarioReport> AssertAsync(bool quiesced, CancellationToken cancellationToken)
	{
		var checks = new List<ScenarioCheck>
		{
			new("background work quiesced", quiesced, quiesced ? "within deadline" : "timed out")
		};

		// -- Persisted order state ---------------------------------------------------------------------
		var persisted = (await _orderRepository.GetAllOrdersAsync().ConfigureAwait(false)).ToList();
		checks.Add(Equal("orders persisted", ExpectedOrdersProcessed, persisted.Count));

		foreach (var order in ValidOrders)
		{
			var record = await _orderRepository.GetOrderAsync(order.OrderId).ConfigureAwait(false);
			checks.Add(new ScenarioCheck(
				$"order {order.OrderId} confirmed",
				record is not null && string.Equals(record.Status, OrderProcessingService.ConfirmedStatus, StringComparison.Ordinal)
									&& record.Quantity == order.Quantity,
				record is null ? "not persisted" : $"status={record.Status}, quantity={record.Quantity}"));
		}

		var invalidRecord = await _orderRepository.GetOrderAsync(InvalidOrder.OrderId).ConfigureAwait(false);
		checks.Add(new ScenarioCheck(
			$"order {InvalidOrder.OrderId} not persisted",
			invalidRecord is null,
			invalidRecord is null ? "absent, as expected" : "present"));

		// -- Inbox: processed, duplicate and failed entries ---------------------------------------------
		checks.Add(Equal("orders processed", ExpectedOrdersProcessed, _monitor.OrdersProcessed));
		checks.Add(Equal("duplicates suppressed", ExpectedDuplicatesSuppressed, _monitor.DuplicatesSuppressed));
		checks.Add(Equal("orders failed", ExpectedOrdersFailed, _monitor.OrdersFailed));

		var firstEntry = await _inboxStore
			.GetEntryAsync(ValidOrders[0].OrderId, OrderProcessingService.HandlerType, cancellationToken)
			.ConfigureAwait(false);
		checks.Add(new ScenarioCheck(
			$"inbox entry {ValidOrders[0].OrderId} is processed",
			firstEntry?.Status == InboxStatus.Processed,
			firstEntry is null ? "no entry" : $"status={firstEntry.Status}"));

		var failedEntry = await _inboxStore
			.GetEntryAsync(InvalidOrder.OrderId, OrderProcessingService.HandlerType, cancellationToken)
			.ConfigureAwait(false);
		checks.Add(new ScenarioCheck(
			$"inbox entry {InvalidOrder.OrderId} is failed with an error",
			failedEntry?.Status == InboxStatus.Failed && !string.IsNullOrEmpty(failedEntry.LastError),
			failedEntry is null ? "no entry" : $"status={failedEntry.Status}, error={failedEntry.LastError}"));

		// -- Outbox: staged, retried, sent, drained -----------------------------------------------------
		checks.Add(Equal("notifications staged", ExpectedNotifications, _monitor.NotificationsStaged));
		checks.Add(Equal("notifications sent", ExpectedNotifications, _monitor.NotificationsSent));
		checks.Add(Equal("notification records", ExpectedNotifications, _emailService.GetSentEmailCount()));

		var retries = _monitor.NotificationSendRetries;
		checks.Add(new ScenarioCheck(
			"transient send failure was retried",
			retries >= 1,
			$"retries={retries}"));

		var flakyDelivered = (await _emailService.GetSentEmailsAsync().ConfigureAwait(false))
			.Any(e => string.Equals(e.ToEmail, FlakyRecipient, StringComparison.Ordinal));
		checks.Add(new ScenarioCheck(
			"retried notification was ultimately delivered",
			flakyDelivered,
			flakyDelivered ? $"delivered to {FlakyRecipient}" : $"never delivered to {FlakyRecipient}"));

		var remaining = (await _outboxStore.GetUnsentMessagesAsync(100, cancellationToken).ConfigureAwait(false)).Count();
		checks.Add(Equal("outbox messages left unsent", 0, remaining));

		// -- Schedule store: stored, executed from the stored payload, completed ------------------------
		checks.Add(Equal("inventory checks scheduled", ExpectedInventoryChecks, _monitor.InventoryChecksScheduled));
		checks.Add(Equal("inventory checks executed", ExpectedInventoryChecks, _monitor.InventoryChecksExecuted));

		foreach (var productId in ScheduledProducts)
		{
			var history = (await _inventoryRepository.GetCheckHistoryAsync(productId).ConfigureAwait(false)).ToList();
			checks.Add(new ScenarioCheck(
				$"inventory check recorded for {productId}",
				history.Count > 0,
				$"{history.Count} result(s)"));
		}

		var due = (await _scheduleStore.GetAllAsync(cancellationToken).ConfigureAwait(false))
			.Count(InventoryCheckProcessor.IsDue);
		checks.Add(Equal("schedules still due", 0, due));

		return new OrderScenarioReport(checks);
	}

	private static ScenarioCheck Equal(string name, long expected, long actual) =>
		new(name, expected == actual, string.Create(CultureInfo.InvariantCulture, $"expected {expected}, actual {actual}"));
}

/// <summary>A single named assertion and the value that satisfied or refuted it.</summary>
public sealed record ScenarioCheck(string Name, bool Passed, string Detail);

/// <summary>The outcome of a scenario run. <see cref="Passed"/> decides the process exit code.</summary>
public sealed class OrderScenarioReport(IReadOnlyList<ScenarioCheck> checks)
{
	public IReadOnlyList<ScenarioCheck> Checks { get; } = checks ?? throw new ArgumentNullException(nameof(checks));

	public bool Passed => Checks.All(static c => c.Passed);

	public void Write(TextWriter writer)
	{
		ArgumentNullException.ThrowIfNull(writer);

		writer.WriteLine("Scenario results");
		writer.WriteLine("----------------");

		foreach (var check in Checks)
		{
			writer.WriteLine($"  [{(check.Passed ? "PASS" : "FAIL")}] {check.Name} ({check.Detail})");
		}

		var failed = Checks.Count(static c => !c.Passed);
		writer.WriteLine();
		writer.WriteLine(failed == 0
			? $"All {Checks.Count} checks passed."
			: $"{failed} of {Checks.Count} checks FAILED.");
	}
}
