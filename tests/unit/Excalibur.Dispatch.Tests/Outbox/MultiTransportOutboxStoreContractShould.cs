// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Outbox;

/// <summary>
/// Contract tests for IMultiTransportOutboxStore interface.
/// Verifies the interface shape, argument semantics, and call patterns
/// that any implementation must support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiTransportOutboxStoreContractShould
{
	[Fact]
	public void HaveFiveCoreMethodsInInterface()
	{
		// Assert -- ISP compliance: core interface should have exactly 5 own methods
		var methods = typeof(IMultiTransportOutboxStore)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

		methods.Length.ShouldBe(5,
			"IMultiTransportOutboxStore should have exactly 5 core methods per ISP");
	}

	[Fact]
	public void HaveFourAdminMethodsInAdminInterface()
	{
		// Assert -- ISP compliance: admin interface should have 4 methods
		var methods = typeof(IMultiTransportOutboxStoreAdmin)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

		methods.Length.ShouldBe(4,
			"IMultiTransportOutboxStoreAdmin should have exactly 4 admin methods");
	}

	[Fact]
	public void InheritFromIOutboxStore()
	{
		// Assert -- core interface should extend IOutboxStore
		typeof(IOutboxStore).IsAssignableFrom(typeof(IMultiTransportOutboxStore)).ShouldBeTrue(
			"IMultiTransportOutboxStore should inherit from IOutboxStore");
	}

	[Fact]
	public void NotInheritAdminFromCore()
	{
		// Assert -- admin interface is separate (ISP)
		typeof(IMultiTransportOutboxStore).IsAssignableFrom(typeof(IMultiTransportOutboxStoreAdmin)).ShouldBeFalse(
			"IMultiTransportOutboxStoreAdmin should NOT inherit from IMultiTransportOutboxStore");
	}

	[Fact]
	public async Task AcceptStageMessageWithTransportsCall()
	{
		// Arrange
		var store = A.Fake<IMultiTransportOutboxStore>();
		var message = new OutboundMessage { Id = "msg-1", Payload = System.Text.Encoding.UTF8.GetBytes("test") };
		var transports = new[] { new OutboundMessageTransport { TransportName = "rabbitmq" } };

		// Act
		await store.StageMessageWithTransportsAsync(message, transports, CancellationToken.None);

		// Assert
		A.CallTo(() => store.StageMessageWithTransportsAsync(message, transports, CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AcceptGetTransportDeliveriesCall()
	{
		// Arrange
		var store = A.Fake<IMultiTransportOutboxStore>();
		A.CallTo(() => store.GetTransportDeliveriesAsync("msg-1", CancellationToken.None))
			.Returns(Task.FromResult<IEnumerable<OutboundMessageTransport>>(new[]
			{
				new OutboundMessageTransport { TransportName = "rabbitmq" },
				new OutboundMessageTransport { TransportName = "kafka" },
			}));

		// Act
		var deliveries = (await store.GetTransportDeliveriesAsync("msg-1", CancellationToken.None)).ToList();

		// Assert
		deliveries.Count.ShouldBe(2);
	}

	[Fact]
	public async Task AcceptMarkTransportSentCall()
	{
		// Arrange
		var store = A.Fake<IMultiTransportOutboxStore>();

		// Act
		await store.MarkTransportSentAsync("msg-1", "rabbitmq", CancellationToken.None);

		// Assert
		A.CallTo(() => store.MarkTransportSentAsync("msg-1", "rabbitmq", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AcceptMarkTransportFailedCall()
	{
		// Arrange
		var store = A.Fake<IMultiTransportOutboxStore>();

		// Act
		await store.MarkTransportFailedAsync("msg-1", "kafka", "Connection refused", CancellationToken.None);

		// Assert
		A.CallTo(() => store.MarkTransportFailedAsync("msg-1", "kafka", "Connection refused", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AcceptMarkTransportSkippedCall()
	{
		// Arrange
		var store = A.Fake<IMultiTransportOutboxStore>();

		// Act
		await store.MarkTransportSkippedAsync("msg-1", "grpc", "Transport not configured", CancellationToken.None);

		// Assert
		A.CallTo(() => store.MarkTransportSkippedAsync("msg-1", "grpc", "Transport not configured", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void HaveTransportDeliveryStatisticsWithExpectedProperties()
	{
		// Assert -- verify the statistics DTO has expected properties
		var stats = new TransportDeliveryStatistics
		{
			PendingCount = 5,
			SendingCount = 2,
			SentCount = 100,
			FailedCount = 3,
			SkippedCount = 1,
			OldestPendingAge = TimeSpan.FromMinutes(10),
			TransportName = "rabbitmq",
		};

		stats.PendingCount.ShouldBe(5);
		stats.SendingCount.ShouldBe(2);
		stats.SentCount.ShouldBe(100);
		stats.FailedCount.ShouldBe(3);
		stats.SkippedCount.ShouldBe(1);
		stats.OldestPendingAge.ShouldBe(TimeSpan.FromMinutes(10));
		stats.TransportName.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task AcceptAdminGetPendingTransportDeliveriesCall()
	{
		// Arrange
		var admin = A.Fake<IMultiTransportOutboxStoreAdmin>();

		// Act
		await admin.GetPendingTransportDeliveriesAsync("rabbitmq", 100, CancellationToken.None);

		// Assert
		A.CallTo(() => admin.GetPendingTransportDeliveriesAsync("rabbitmq", 100, CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AcceptAdminGetTransportStatisticsCall()
	{
		// Arrange
		var admin = A.Fake<IMultiTransportOutboxStoreAdmin>();
		A.CallTo(() => admin.GetTransportStatisticsAsync("rabbitmq", CancellationToken.None))
			.Returns(Task.FromResult(new TransportDeliveryStatistics { SentCount = 42 }));

		// Act
		var stats = await admin.GetTransportStatisticsAsync("rabbitmq", CancellationToken.None);

		// Assert
		stats.SentCount.ShouldBe(42);
	}
}
