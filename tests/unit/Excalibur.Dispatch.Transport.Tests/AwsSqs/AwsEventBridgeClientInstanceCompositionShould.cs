// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

// NEW helper API is deliberately a separate POST-API file. Do not adopt it into
// the initial current-API HTTP semantic RED build before that API exists.
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class AwsEventBridgeClientInstanceCompositionShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task SelectOnlyTheDefaultSdkInstanceWhilePreservingEachNamedBusAndCallerOwnership(bool helperFirst, bool unkeyedFirst)
	{
		var a = Client();
		var b = Client();
		var factoryCalls = 0;
		var services = Services();
		services.AddKeyedSingleton<IAmazonEventBridge>("A", a);
		if (helperFirst)
		{
			services.AddDefaultAwsEventBridgeSdkClientInstance("B", b);
		}
		else
		{
			// The helper must REMOVE all matching old descriptors, not leave owning
			// factories reachable through GetKeyedServices/GetServices enumeration.
			services.AddKeyedSingleton<IAmazonEventBridge>("B", (_, _) => { factoryCalls++; return A.Fake<IAmazonEventBridge>(); });
			services.AddKeyedSingleton<IAmazonEventBridge>("B", (_, _) => { factoryCalls++; return A.Fake<IAmazonEventBridge>(); });
		}
		services.AddAwsEventBridgeTransport("A", builder => builder.Region("us-east-1"));
		services.AddAwsEventBridgeTransport("B", builder => builder.Region("us-east-1"));
		if (!helperFirst) { services.AddDefaultAwsEventBridgeSdkClientInstance("B", b); }
		factoryCalls.ShouldBe(0);
		var provider = services.BuildServiceProvider();
		try
		{
			AwsEventBridgeMessageBus defaultBus;
			if (unkeyedFirst)
			{
				provider.GetRequiredService<IAmazonEventBridge>().ShouldBeSameAs(b);
				defaultBus = provider.GetRequiredService<AwsEventBridgeMessageBus>();
			}
			else
			{
				provider.GetRequiredKeyedService<IAmazonEventBridge>("B").ShouldBeSameAs(b);
				_ = provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("B");
				defaultBus = provider.GetRequiredService<AwsEventBridgeMessageBus>();
			}
			provider.GetRequiredKeyedService<IAmazonEventBridge>("A").ShouldBeSameAs(a);
			provider.GetRequiredKeyedService<IAmazonEventBridge>("B").ShouldBeSameAs(b);
			provider.GetServices<IAmazonEventBridge>().ShouldHaveSingleItem().ShouldBeSameAs(b);
			provider.GetKeyedServices<IAmazonEventBridge>("B").ShouldHaveSingleItem().ShouldBeSameAs(b);
			defaultBus.ShouldBeSameAs(provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("A"));
			var busB = provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("B");
			busB.ShouldNotBeSameAs(defaultBus);
			await defaultBus.PublishAsync(new InstanceRouteEvent("A"), new MessageContext(), TestContext.Current.CancellationToken);
			A.CallTo(() => a.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => b.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
			await busB.PublishAsync(new InstanceRouteEvent("B"), new MessageContext(), TestContext.Current.CancellationToken);
			A.CallTo(() => a.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => b.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			factoryCalls.ShouldBe(0);
		}
		finally { await provider.DisposeAsync(); }
		A.CallTo(() => a.Dispose()).MustNotHaveHappened();
		A.CallTo(() => b.Dispose()).MustNotHaveHappened();
		a.Dispose(); b.Dispose();
		A.CallTo(() => a.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => b.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LetTheLastExplicitHelperChooseSdkDefaultWithoutRemovingOtherNamedInstances()
	{
		var a = Client();
		var b = Client();
		var services = Services();
		services.AddDefaultAwsEventBridgeSdkClientInstance("A", a);
		services.AddAwsEventBridgeTransport("A", builder => builder.Region("us-east-1"));
		services.AddAwsEventBridgeTransport("B", builder => builder.Region("us-east-1"));
		services.AddDefaultAwsEventBridgeSdkClientInstance("B", b);
		using (var provider = services.BuildServiceProvider())
		{
			provider.GetRequiredService<IAmazonEventBridge>().ShouldBeSameAs(b);
			provider.GetRequiredKeyedService<IAmazonEventBridge>("A").ShouldBeSameAs(a);
			provider.GetRequiredKeyedService<IAmazonEventBridge>("B").ShouldBeSameAs(b);
		}
		A.CallTo(() => a.Dispose()).MustNotHaveHappened();
		A.CallTo(() => b.Dispose()).MustNotHaveHappened();
		a.Dispose(); b.Dispose();
	}

	private static ServiceCollection Services()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		return services;
	}
	private static IAmazonEventBridge Client()
	{
		var client = A.Fake<IAmazonEventBridge>();
		A.CallTo(() => client.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).Returns(Task.FromResult(new PutEventsResponse
		{
			FailedEntryCount = 0, Entries = [new PutEventsResultEntry { EventId = "accepted-controlled" }],
		}));
		return client;
	}
	private sealed record InstanceRouteEvent(string Destination) : IDispatchEvent;
}
