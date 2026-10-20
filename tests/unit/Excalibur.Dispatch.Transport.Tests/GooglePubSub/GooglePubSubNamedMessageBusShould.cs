// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class GooglePubSubNamedMessageBusShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PublishThroughIndependentNamedSdkClientsInEitherRegistrationOrder(bool reverse)
	{
		var services = CreateServices();
		foreach (var name in reverse ? new[] { "audit", "orders" } : ["orders", "audit"])
		{
			_ = services.AddGooglePubSubTransport(name, pubsub => pubsub.ProjectId(name + "-project").TopicId(name + "-topic"));
		}
		services.Any(d => d.ServiceType == typeof(TopicPublisherClientAdapter) && Equals(d.ServiceKey, "orders")).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(TopicPublisherClientAdapter) && Equals(d.ServiceKey, "audit")).ShouldBeTrue();
		var orders = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		var audit = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		PubsubMessage? ordersMessage = null;
		PubsubMessage? auditMessage = null;
		A.CallTo(() => orders.PublishAsync(A<PubsubMessage>._))
			.Invokes((PubsubMessage message) => ordersMessage = message).Returns(Task.FromResult("orders-id"));
		A.CallTo(() => audit.PublishAsync(A<PubsubMessage>._))
			.Invokes((PubsubMessage message) => auditMessage = message).Returns(Task.FromResult("audit-id"));
		_ = services.AddKeyedSingleton<PublisherClient>("orders", orders);
		_ = services.AddKeyedSingleton<PublisherClient>("audit", audit);
		_ = services.AddSingleton<PublisherClient>(_ => throw new InvalidOperationException("Unkeyed override must not defeat a keyed override"));
		await using (var provider = services.BuildServiceProvider())
		{
			var buses = provider.GetRequiredService<IMessageBusProvider>();
			var ordersBus = buses.GetRemoteMessageBus("orders").ShouldNotBeNull();
			var auditBus = buses.GetRemoteMessageBus("audit").ShouldNotBeNull();
			ordersBus.ShouldNotBeSameAs(auditBus);
			await ordersBus.PublishAsync(new PubSubEvent("orders"), CreateContext(), CancellationToken.None);
			await auditBus.PublishAsync(new PubSubEvent("audit"), CreateContext(), CancellationToken.None);
			var serializer = provider.GetRequiredService<IPayloadSerializer>();
			serializer.Deserialize<PubSubEvent>(ordersMessage.ShouldNotBeNull().Data.ToByteArray()).Value.ShouldBe("orders");
			serializer.Deserialize<PubSubEvent>(auditMessage.ShouldNotBeNull().Data.ToByteArray()).Value.ShouldBe("audit");
			using var canceled = new CancellationTokenSource();
			canceled.Cancel();
			_ = await Should.ThrowAsync<OperationCanceledException>(() => ordersBus.PublishAsync(new PubSubEvent("cancelled"), CreateContext(), canceled.Token));
		}
		A.CallTo(() => orders.PublishAsync(A<PubsubMessage>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => audit.PublishAsync(A<PubsubMessage>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => orders.DisposeAsync()).MustNotHaveHappened();
		A.CallTo(() => audit.DisposeAsync()).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PreserveAnEffectiveExplicitGlobalClientBeforeOrAfterRegistration(bool before)
	{
		var services = CreateServices();
		var client = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		A.CallTo(() => client.PublishAsync(A<PubsubMessage>._)).Returns(Task.FromResult("id"));
		if (before)
		{
			_ = services.AddSingleton<PublisherClient>(_ => client);
		}
		_ = services.AddGooglePubSubTransport("orders", pubsub => pubsub.ProjectId("orders-project").TopicId("orders-topic"));
		_ = services.AddGooglePubSubTransport("audit", pubsub => pubsub.ProjectId("audit-project").TopicId("audit-topic"));
		if (!before)
		{
			_ = services.AddSingleton<PublisherClient>(_ => client);
		}
		await using (var provider = services.BuildServiceProvider())
		{
			var buses = provider.GetRequiredService<IMessageBusProvider>();
			await buses.GetRemoteMessageBus("orders").ShouldNotBeNull().PublishAsync(new PubSubEvent("orders"), CreateContext(), CancellationToken.None);
			await buses.GetRemoteMessageBus("audit").ShouldNotBeNull().PublishAsync(new PubSubEvent("audit"), CreateContext(), CancellationToken.None);
		}
		A.CallTo(() => client.PublishAsync(A<PubsubMessage>._)).MustHaveHappenedTwiceExactly();
		A.CallTo(() => client.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ConstructTheSdkBuilderWithTheMatchingNamedTopicAndEmulatorPolicy()
	{
		var services = CreateServices();
		_ = services.AddGooglePubSubTransport("orders", pubsub => pubsub.ProjectId("orders-project").TopicId("orders-topic"));
		_ = services.AddGooglePubSubTransport("audit", pubsub => pubsub.ProjectId("audit-project").TopicId("audit-topic"));
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>();
		var orders = NamedTopicPublisherRegistration.CreateBuilder(options.Get("orders"));
		var audit = NamedTopicPublisherRegistration.CreateBuilder(options.Get("audit"));
		orders.TopicName.ToString().ShouldBe("projects/orders-project/topics/orders-topic");
		audit.TopicName.ToString().ShouldBe("projects/audit-project/topics/audit-topic");
		orders.EmulatorDetection.ShouldBe(EmulatorDetection.EmulatorOrProduction);
		audit.EmulatorDetection.ShouldBe(EmulatorDetection.EmulatorOrProduction);
	}

	[Fact]
	public async Task StartAPublisherOnlyHostWithoutInventingReceivingCapabilities()
	{
		var builder = Host.CreateApplicationBuilder();
		_ = builder.Services.AddDispatch(dispatch =>
		{
			_ = dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson());
			_ = dispatch.UseGooglePubSub("publisher", pubsub => pubsub.ProjectId("orders-project").TopicId("orders-topic"));
		});
		var client = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		_ = builder.Services.AddKeyedSingleton<PublisherClient>("publisher", client);
		var host = builder.Build();
		await using var lifetime = (IAsyncDisposable)host;
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try
		{
			await host.StartAsync(deadline.Token);
			host.Services.GetRequiredService<IMessageBusProvider>().GetRemoteMessageBus("publisher").ShouldNotBeNull();
			host.Services.GetKeyedService<ITransportSubscriber>("publisher").ShouldBeNull();
			host.Services.GetKeyedService<ITransportReceiver>("publisher").ShouldBeNull();
			host.Services.GetService<SubscriberClient>().ShouldBeNull();
			_ = Should.Throw<InvalidOperationException>(() => host.Services.GetRequiredKeyedService<ITransportReceiver>("publisher"));
		}
		finally
		{
			using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await host.StopAsync(stop.Token);
		}
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

	private sealed record PubSubEvent(string Value) : IDispatchEvent;
}
