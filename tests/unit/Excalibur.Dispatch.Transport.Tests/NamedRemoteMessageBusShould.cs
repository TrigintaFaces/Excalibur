// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SQS;
using Amazon.SQS.Model;
using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.AzureServiceBus;

namespace Excalibur.Dispatch.Transport.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Registration")]
public sealed class NamedRemoteMessageBusShould
{
	[Theory]
	[InlineData("sqs")]
	[InlineData("servicebus")]
	[InlineData("pubsub")]
	[InlineData("rabbitmq")]
	public void ExposeStandardNamedTransportsToTheMessageBusProvider(string transport)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		if (transport == "sqs")
		{
			_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion("us-east-1"));
			_ = services.AddAwsSqsTransport("audit", sqs => sqs.UseRegion("us-west-2"));
		}
		else if (transport == "servicebus")
		{
			_ = services.AddAzureServiceBusTransport("orders", bus => bus
				.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v"));
			_ = services.AddAzureServiceBusTransport("audit", bus => bus
				.ConnectionString("Endpoint=sb://audit.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v"));
		}
		else if (transport == "pubsub")
		{
			_ = services.AddGooglePubSubTransport("orders", pubsub => pubsub.ProjectId("orders-project").TopicId("orders-topic"));
			_ = services.AddGooglePubSubTransport("audit", pubsub => pubsub.ProjectId("audit-project").TopicId("audit-topic"));
		}
		else
		{
			_ = services.AddRabbitMQTransport("orders", rabbit => rabbit.ConnectionString("amqps://orders-broker/"));
			_ = services.AddRabbitMQTransport("audit", rabbit => rabbit.ConnectionString("amqps://audit-broker/"));
		}

		using var provider = services.BuildServiceProvider();
		var buses = provider.GetRequiredService<IMessageBusProvider>();
		buses.GetAllMessageBusNames().ShouldContain("orders");
		buses.GetAllMessageBusNames().ShouldContain("audit");
	}

	[Fact]
	public async Task PublishSqsEventsThroughTheMatchingNamedNativeClient()
	{
		var services = CreateServices();
		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion("us-east-1").MapQueue<RemoteEvent>("https://sqs.us-east-1.amazonaws.com/123/orders"));
		_ = services.AddAwsSqsTransport("audit", sqs => sqs.UseRegion("us-west-2").MapQueue<RemoteEvent>("https://sqs.us-west-2.amazonaws.com/123/audit"));
		services.Any(d => d.ServiceType == typeof(IAmazonSQS) && Equals(d.ServiceKey, "orders")).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IAmazonSQS) && Equals(d.ServiceKey, "audit")).ShouldBeTrue();
		var orders = A.Fake<IAmazonSQS>();
		var audit = A.Fake<IAmazonSQS>();
		SendMessageRequest? ordersRequest = null;
		SendMessageRequest? auditRequest = null;
		A.CallTo(() => orders.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageRequest request, CancellationToken _) => ordersRequest = request)
			.Returns(Task.FromResult(new SendMessageResponse()));
		A.CallTo(() => audit.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageRequest request, CancellationToken _) => auditRequest = request)
			.Returns(Task.FromResult(new SendMessageResponse()));
		_ = services.AddKeyedSingleton<IAmazonSQS>("orders", (_, _) => orders);
		_ = services.AddKeyedSingleton<IAmazonSQS>("audit", (_, _) => audit);
		await using (var provider = services.BuildServiceProvider())
		{
			var buses = provider.GetRequiredService<IMessageBusProvider>();
			await buses.GetRemoteMessageBus("orders").ShouldNotBeNull().PublishAsync(new RemoteEvent("orders"), CreateContext(), CancellationToken.None);
			await buses.GetRemoteMessageBus("audit").ShouldNotBeNull().PublishAsync(new RemoteEvent("audit"), CreateContext(), CancellationToken.None);
			var serializer = provider.GetRequiredService<IPayloadSerializer>();
			serializer.Deserialize<RemoteEvent>(Convert.FromBase64String(ordersRequest.ShouldNotBeNull().MessageBody)).Value.ShouldBe("orders");
			serializer.Deserialize<RemoteEvent>(Convert.FromBase64String(auditRequest.ShouldNotBeNull().MessageBody)).Value.ShouldBe("audit");
		}
		ordersRequest.ShouldNotBeNull().QueueUrl.ShouldBe("https://sqs.us-east-1.amazonaws.com/123/orders");
		auditRequest.ShouldNotBeNull().QueueUrl.ShouldBe("https://sqs.us-west-2.amazonaws.com/123/audit");
		A.CallTo(() => orders.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => orders.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishServiceBusEventsThroughTheMatchingNamedNativeSender()
	{
		var services = CreateServices();
		_ = services.AddAzureServiceBusTransport("orders", bus => bus.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureOptions(options => options.Sender.DefaultEntityName = "orders-queue"));
		_ = services.AddAzureServiceBusTransport("audit", bus => bus.ConnectionString("Endpoint=sb://audit.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureOptions(options => options.Sender.DefaultEntityName = "audit-queue"));
		services.Any(d => d.ServiceType == typeof(ServiceBusClient) && Equals(d.ServiceKey, "orders")).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(ServiceBusClient) && Equals(d.ServiceKey, "audit")).ShouldBeTrue();
		var orders = A.Fake<global::Azure.Messaging.ServiceBus.ServiceBusClient>();
		var audit = A.Fake<global::Azure.Messaging.ServiceBus.ServiceBusClient>();
		var ordersSender = A.Fake<global::Azure.Messaging.ServiceBus.ServiceBusSender>();
		var auditSender = A.Fake<global::Azure.Messaging.ServiceBus.ServiceBusSender>();
		A.CallTo(() => orders.CreateSender("orders-queue")).Returns(ordersSender);
		A.CallTo(() => audit.CreateSender("audit-queue")).Returns(auditSender);
		ServiceBusMessage? ordersMessage = null;
		ServiceBusMessage? auditMessage = null;
		A.CallTo(() => ordersSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
			.Invokes((ServiceBusMessage message, CancellationToken _) => ordersMessage = message).Returns(Task.CompletedTask);
		A.CallTo(() => auditSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
			.Invokes((ServiceBusMessage message, CancellationToken _) => auditMessage = message).Returns(Task.CompletedTask);
		_ = services.AddKeyedSingleton<ServiceBusClient>("orders", (_, _) => orders);
		_ = services.AddKeyedSingleton<ServiceBusClient>("audit", (_, _) => audit);
		await using (var provider = services.BuildServiceProvider())
		{
			var buses = provider.GetRequiredService<IMessageBusProvider>();
			await buses.GetRemoteMessageBus("orders").ShouldNotBeNull().PublishAsync(new RemoteEvent("orders"), CreateContext(), CancellationToken.None);
			await buses.GetRemoteMessageBus("audit").ShouldNotBeNull().PublishAsync(new RemoteEvent("audit"), CreateContext(), CancellationToken.None);
			var serializer = provider.GetRequiredService<IPayloadSerializer>();
			serializer.Deserialize<RemoteEvent>(ordersMessage.ShouldNotBeNull().Body.ToArray()).Value.ShouldBe("orders");
			serializer.Deserialize<RemoteEvent>(auditMessage.ShouldNotBeNull().Body.ToArray()).Value.ShouldBe("audit");
		}
		A.CallTo(() => orders.CreateSender("orders-queue")).MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.CreateSender("audit-queue")).MustHaveHappenedOnceExactly();
		A.CallTo(() => ordersSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => auditSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => ordersSender.DisposeAsync()).MustHaveHappenedOnceExactly();
		A.CallTo(() => auditSender.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	private static ServiceCollection CreateServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson()));
		return services;
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		return context;
	}

	private sealed record RemoteEvent(string Value) : IDispatchEvent;
}
