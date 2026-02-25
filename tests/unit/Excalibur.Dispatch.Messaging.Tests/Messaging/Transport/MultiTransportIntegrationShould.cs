// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Sprint 38: bd-l7mz - Multi-Transport Integration Tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Bus;
using Excalibur.Dispatch.Transport;

// Alias to avoid ambiguity with Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult
using TransportHealthCheckResult = Excalibur.Dispatch.Abstractions.Transport.HealthCheckResult;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Integration tests for Multi-Transport Publishing (Epic 2 completion).
/// Tests publishing to multiple transports, per-transport delivery status,
/// routing configuration, default transport fallback, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MultiTransportIntegrationShould
{
	#region Multi-Transport Message Bus Adapter Tests

	[Fact]
	public void Create_MultiTransportAdapter_With_Multiple_Adapters()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq");
		var adapter2 = CreateMockAdapter("kafka");
		var adapters = new[] { adapter1, adapter2 };

		// Act
		var multiAdapter = new MultiTransportMessageBusAdapter(adapters);

		// Assert
		_ = multiAdapter.ShouldNotBeNull();
		multiAdapter.Name.ShouldBe("MultiTransport");
	}

	[Fact]
	public void Report_SupportsPublishing_When_Any_Adapter_Supports_Publishing()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq", supportsPublishing: true);
		var adapter2 = CreateMockAdapter("kafka", supportsPublishing: false);

		// Act
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Assert
		multiAdapter.SupportsPublishing.ShouldBeTrue();
	}

	[Fact]
	public void Report_SupportsSubscription_When_Any_Adapter_Supports_Subscription()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq", supportsSubscription: true);
		var adapter2 = CreateMockAdapter("kafka", supportsSubscription: false);

		// Act
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Assert
		multiAdapter.SupportsSubscription.ShouldBeTrue();
	}

	[Fact]
	public void Report_NotSupportsPublishing_When_No_Adapter_Supports_Publishing()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq", supportsPublishing: false);
		var adapter2 = CreateMockAdapter("kafka", supportsPublishing: false);

		// Act
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Assert
		multiAdapter.SupportsPublishing.ShouldBeFalse();
	}

	[Fact]
	public async Task Publish_Message_Via_Default_Adapter()
	{
		// Arrange
		var defaultAdapter = CreateMockAdapter("rabbitmq");
		var expectedResult = MessageResult.Success();
		_ = A.CallTo(() => defaultAdapter.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(expectedResult);

		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { defaultAdapter }, defaultAdapter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var result = await multiAdapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => defaultAdapter.PublishAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_Failed_Result_When_No_Default_Adapter()
	{
		// Arrange - empty adapter list
		var multiAdapter = new MultiTransportMessageBusAdapter(Array.Empty<IMessageBusAdapter>());
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("test-message-id");

		// Act
		var result = await multiAdapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task Subscribe_Via_Default_Adapter_For_Simple_Subscription()
	{
		// Arrange
		var defaultAdapter = CreateMockAdapter("rabbitmq");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { defaultAdapter }, defaultAdapter);
		var subscriptionName = "my-subscription";
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(msg, ctx, ct) => Task.FromResult(MessageResult.Success());

		// Act
		await multiAdapter.SubscribeAsync(subscriptionName, handler, null, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => defaultAdapter.SubscribeAsync(subscriptionName, handler, A<IMessageBusOptions>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Subscribe_Via_Specific_Adapter_Using_Protocol_Format()
	{
		// Arrange
		var rabbitAdapter = CreateMockAdapter("rabbitmq");
		var kafkaAdapter = CreateMockAdapter("kafka");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { rabbitAdapter, kafkaAdapter }, rabbitAdapter);
		var subscriptionName = "kafka://my-topic";
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(msg, ctx, ct) => Task.FromResult(MessageResult.Success());

		// Act
		await multiAdapter.SubscribeAsync(subscriptionName, handler, null, CancellationToken.None);

		// Assert - should route to kafka adapter with subscription name "my-topic"
		_ = A.CallTo(() => kafkaAdapter.SubscribeAsync("my-topic", handler, A<IMessageBusOptions>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => rabbitAdapter.SubscribeAsync(A<string>._, A<Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>>>._, A<IMessageBusOptions>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Throw_When_Subscribing_To_Unknown_Adapter()
	{
		// Arrange
		var rabbitAdapter = CreateMockAdapter("rabbitmq");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { rabbitAdapter }, rabbitAdapter);
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(msg, ctx, ct) => Task.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await multiAdapter.SubscribeAsync("unknown://my-subscription", handler, null, CancellationToken.None));
	}

	[Fact]
	public async Task Unsubscribe_Via_Default_Adapter()
	{
		// Arrange
		var defaultAdapter = CreateMockAdapter("rabbitmq");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { defaultAdapter }, defaultAdapter);
		var subscriptionName = "my-subscription";

		// Act
		await multiAdapter.UnsubscribeAsync(subscriptionName, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => defaultAdapter.UnsubscribeAsync(subscriptionName, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Unsubscribe_Via_Specific_Adapter()
	{
		// Arrange
		var rabbitAdapter = CreateMockAdapter("rabbitmq");
		var kafkaAdapter = CreateMockAdapter("kafka");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { rabbitAdapter, kafkaAdapter }, rabbitAdapter);

		// Act
		await multiAdapter.UnsubscribeAsync("kafka://my-topic", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => kafkaAdapter.UnsubscribeAsync("my-topic", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Silently_Ignore_Unsubscribe_From_Unknown_Adapter()
	{
		// Arrange
		var rabbitAdapter = CreateMockAdapter("rabbitmq");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { rabbitAdapter }, rabbitAdapter);

		// Act - should not throw
		await multiAdapter.UnsubscribeAsync("unknown://my-subscription", CancellationToken.None);

		// Assert - no exception, no calls to any adapter for unknown protocol
		A.CallTo(() => rabbitAdapter.UnsubscribeAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Health Check Aggregation Tests

	[Fact]
	public async Task Report_Healthy_When_All_Adapters_Healthy()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq", isConnected: true);
		var adapter2 = CreateMockAdapter("kafka", isConnected: true);
		_ = A.CallTo(() => adapter1.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new TransportHealthCheckResult(true, "RabbitMQ healthy"));
		_ = A.CallTo(() => adapter2.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new TransportHealthCheckResult(true, "Kafka healthy"));

		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Act
		var result = await multiAdapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldContain("healthy");
	}

	[Fact]
	public async Task Report_Unhealthy_When_Any_Adapter_Unhealthy()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq", isConnected: true);
		var adapter2 = CreateMockAdapter("kafka", isConnected: false);
		_ = A.CallTo(() => adapter1.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new TransportHealthCheckResult(true, "RabbitMQ healthy"));
		_ = A.CallTo(() => adapter2.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new TransportHealthCheckResult(false, "Kafka disconnected"));

		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Act
		var result = await multiAdapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task Include_Per_Adapter_Status_In_Health_Data()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq");
		var healthData = new Dictionary<string, object> { ["rabbitmq_healthy"] = true };
		_ = A.CallTo(() => adapter1.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new TransportHealthCheckResult(true, "RabbitMQ OK", healthData));

		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1 });

		// Act
		var result = await multiAdapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	#endregion

	#region Lifecycle Management Tests

	[Fact]
	public async Task Initialize_All_Adapters_Concurrently()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq");
		var adapter2 = CreateMockAdapter("kafka");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });
		var options = A.Fake<IMessageBusOptions>();

		// Act
		await multiAdapter.InitializeAsync(options, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => adapter1.InitializeAsync(options, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => adapter2.InitializeAsync(options, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Start_All_Adapters_Concurrently()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq");
		var adapter2 = CreateMockAdapter("kafka");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Act
		await multiAdapter.StartAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => adapter1.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => adapter2.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Stop_All_Adapters_Concurrently()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq");
		var adapter2 = CreateMockAdapter("kafka");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Act
		await multiAdapter.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => adapter1.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => adapter2.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Dispose_All_Adapters_And_Clear_Collection()
	{
		// Arrange
		var adapter1 = CreateMockAdapter("rabbitmq");
		var adapter2 = CreateMockAdapter("kafka");
		var multiAdapter = new MultiTransportMessageBusAdapter(new[] { adapter1, adapter2 });

		// Act
		multiAdapter.Dispose();

		// Assert
		_ = A.CallTo(() => adapter1.Dispose()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => adapter2.Dispose()).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Transport Registry Tests

	[Fact]
	public void Register_Transport_With_Registry()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateMockTransportAdapter("rabbitmq");

		// Act
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		// Assert
		registry.GetTransportAdapter("rabbitmq").ShouldBe(adapter);
	}

	[Fact]
	public void Set_Default_Transport()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateMockTransportAdapter("rabbitmq");
		var adapter2 = CreateMockTransportAdapter("kafka");
		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");

		// Act
		registry.SetDefaultTransport("kafka");

		// Assert
		registry.DefaultTransportName.ShouldBe("kafka");
		registry.HasDefaultTransport.ShouldBeTrue();
	}

	[Fact]
	public void Get_All_Registered_Transports()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateMockTransportAdapter("rabbitmq");
		var adapter2 = CreateMockTransportAdapter("kafka");
		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");

		// Act
		var transports = registry.GetTransportNames().ToList();

		// Assert
		transports.Count.ShouldBe(2);
		transports.ShouldContain("rabbitmq");
		transports.ShouldContain("kafka");
	}

	[Fact]
	public void Return_Null_For_Unregistered_Transport()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var adapter = registry.GetTransportAdapter("nonexistent");

		// Assert
		adapter.ShouldBeNull();
	}

	[Fact]
	public void Throw_When_Registering_Duplicate_Transport()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateMockTransportAdapter("rabbitmq");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ"));
	}

	[Fact]
	public void Get_Default_Transport_Adapter()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateMockTransportAdapter("rabbitmq");
		var adapter2 = CreateMockTransportAdapter("kafka");
		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");
		registry.SetDefaultTransport("kafka");

		// Act
		var defaultAdapter = registry.GetDefaultTransportAdapter();

		// Assert
		defaultAdapter.ShouldBe(adapter2);
	}

	#endregion

	#region Helper Methods

	private static IMessageBusAdapter CreateMockAdapter(
		string name,
		bool supportsPublishing = true,
		bool supportsSubscription = true,
		bool supportsTransactions = false,
		bool isConnected = true)
	{
		var adapter = A.Fake<IMessageBusAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		_ = A.CallTo(() => adapter.SupportsPublishing).Returns(supportsPublishing);
		_ = A.CallTo(() => adapter.SupportsSubscription).Returns(supportsSubscription);
		_ = A.CallTo(() => adapter.SupportsTransactions).Returns(supportsTransactions);
		_ = A.CallTo(() => adapter.IsConnected).Returns(isConnected);
		return adapter;
	}

	private static ITransportAdapter CreateMockTransportAdapter(string name)
	{
		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		return adapter;
	}

	#endregion
}
