// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Excalibur.ECommerceSample.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Sample.OrderSubmission")]
[Trait("Pattern", "Regression")]
public sealed class OrderStagingRecoveryShould
{
	[Fact]
	public async Task RetryAPreAcceptanceStageFailureThenSuppressOnlyACompletedDuplicate()
	{
		await using var services = Stores();
		var inbox = services.GetRequiredService<IInboxStore>();
		var outbox = services.GetRequiredService<IOutboxStore>();
		var stage = A.Fake<IOutboxStore>();
		var attempts = 0;
		A.CallTo(() => stage.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.ReturnsLazily((OutboundMessage row, CancellationToken token) =>
			{
				if (Interlocked.Increment(ref attempts) == 1)
				{
					throw new IOException("one-pre-acceptance-stage-failure");
				}
				return outbox.StageMessageAsync(row, token);
			});
		var repository = new InMemoryOrderRepository(NullLogger<InMemoryOrderRepository>.Instance);
		var monitor = new PerformanceMonitor();
		var notifications = new NotificationService(stage, monitor, NullLogger<NotificationService>.Instance);
		var processor = new OrderProcessingService(inbox, repository, notifications, monitor,
			NullLogger<OrderProcessingService>.Instance);
		var order = Order();

		(await processor.ProcessOrderAsync(order, CancellationToken.None)).ShouldBe(OrderProcessingOutcome.Failed);
		attempts.ShouldBe(1);
		(await outbox.GetUnsentMessagesAsync(10, CancellationToken.None)).ShouldBeEmpty();

		// Semantic RED on current source: the Failed inbox entry is incorrectly called Duplicate.
		(await processor.ProcessOrderAsync(order, CancellationToken.None)).ShouldBe(OrderProcessingOutcome.Processed);
		(await processor.ProcessOrderAsync(order, CancellationToken.None)).ShouldBe(OrderProcessingOutcome.Duplicate);

		attempts.ShouldBe(2);
		repository.GetTotalOrderCount().ShouldBe(1);
		var rows = (await outbox.GetUnsentMessagesAsync(10, CancellationToken.None)).ToArray();
		rows.Length.ShouldBe(1);
		rows[0].Id.ShouldBe("order-confirmation-d06-order");
		(await inbox.GetEntryAsync(order.OrderId, OrderProcessingService.HandlerType, CancellationToken.None))
			.ShouldNotBeNull().Status.ShouldBe(InboxStatus.Processed);
		monitor.OrdersProcessed.ShouldBe(1);
		monitor.DuplicatesSuppressed.ShouldBe(1);
	}

	[Fact]
	public async Task KeepHealthyCompletedDuplicateSuppressionAndOneNotification()
	{
		await using var services = Stores();
		var inbox = services.GetRequiredService<IInboxStore>();
		var outbox = services.GetRequiredService<IOutboxStore>();
		var repository = new InMemoryOrderRepository(NullLogger<InMemoryOrderRepository>.Instance);
		var monitor = new PerformanceMonitor();
		var processor = new OrderProcessingService(inbox, repository,
			new NotificationService(outbox, monitor, NullLogger<NotificationService>.Instance), monitor,
			NullLogger<OrderProcessingService>.Instance);

		(await processor.ProcessOrderAsync(Order(), CancellationToken.None)).ShouldBe(OrderProcessingOutcome.Processed);
		(await processor.ProcessOrderAsync(Order(), CancellationToken.None)).ShouldBe(OrderProcessingOutcome.Duplicate);
		repository.GetTotalOrderCount().ShouldBe(1);
		(await outbox.GetUnsentMessagesAsync(10, CancellationToken.None)).Count().ShouldBe(1);
	}

	[Fact]
	public async Task NeverStageAnInvalidOrder()
	{
		await using var services = Stores();
		var inbox = services.GetRequiredService<IInboxStore>();
		var outbox = services.GetRequiredService<IOutboxStore>();
		var repository = new InMemoryOrderRepository(NullLogger<InMemoryOrderRepository>.Instance);
		var monitor = new PerformanceMonitor();
		var processor = new OrderProcessingService(inbox, repository,
			new NotificationService(outbox, monitor, NullLogger<NotificationService>.Instance), monitor,
			NullLogger<OrderProcessingService>.Instance);

		(await processor.ProcessOrderAsync(Order() with { Price = 0 }, CancellationToken.None))
			.ShouldBe(OrderProcessingOutcome.Failed);
		repository.GetTotalOrderCount().ShouldBe(0);
		(await outbox.GetUnsentMessagesAsync(10, CancellationToken.None)).ShouldBeEmpty();
	}

	private static ServiceProvider Stores()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburInbox(inbox => inbox.UseInMemory());
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseInMemory()));
		return services.BuildServiceProvider();
	}

	private static OrderCreated Order() => new()
	{
		OrderId = "d06-order", CustomerId = "customer@example.test", ProductId = "product",
		ProductName = "Product", Price = 10m, Quantity = 1,
		OrderDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
	};
}
