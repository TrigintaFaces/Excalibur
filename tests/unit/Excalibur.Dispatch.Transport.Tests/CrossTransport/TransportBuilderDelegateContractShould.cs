// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Locks the re-runnability contract the transport registrations place on a consumer's configuration
/// delegate. Several transports branch their registration graph on configuration, so they must read
/// those values before the container exists; they do that by running the consumer's own delegate
/// against a throwaway instance first, and again against the instance the options system owns.
///
/// That is a published contract — consumers are told their delegate must be re-runnable and that a
/// delegate with side effects may observe them more than once. These assert the observable count, so
/// the documented number and the implemented number cannot drift apart silently.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class TransportBuilderDelegateContractShould
{
	[Fact]
	public void RunTheAzureServiceBusDelegateTwice_OnceEagerlyAndOnceAgainstTheServedInstance()
	{
		var invocations = 0;
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusTransport("orders", sb =>
		{
			invocations++;
			_ = sb.ConnectionString("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
				.ConfigureSender(sender => sender.DefaultEntityName = "orders-queue");
		});

		// One eager application decides which registrations are made at all.
		invocations.ShouldBe(1);

		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get("orders");

		// The second runs against the instance every resolved component reads.
		invocations.ShouldBe(2);
	}

	[Fact]
	public void RunTheGooglePubSubDelegateTwice_OnceEagerlyAndOnceAgainstTheServedInstance()
	{
		var invocations = 0;
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("events", pubsub =>
		{
			invocations++;
			_ = pubsub.ProjectId("my-project").TopicId("my-topic").SubscriptionId("my-subscription");
		});

		invocations.ShouldBe(1);

		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>().Get("events");

		invocations.ShouldBe(2);
	}

	[Fact]
	public void RunTheAwsSnsDelegateTwice_OnceEagerlyAndOnceAgainstTheServedInstance()
	{
		var invocations = 0;
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("notifications", sns =>
		{
			invocations++;
			_ = sns.TopicArn("arn:aws:sns:us-east-1:123456789:my-topic").Region("us-east-1");
		});

		invocations.ShouldBe(1);

		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>().Get("notifications");

		invocations.ShouldBe(2);
	}

	[Fact]
	public void RunThePulsarDelegateOnce_BecauseNoRegistrationBranchesOnItsConfiguration()
	{
		var invocations = 0;
		var services = new ServiceCollection();

		_ = services.AddPulsarTransport("events", pulsar =>
		{
			invocations++;
			_ = pulsar.ServiceUrl("pulsar://localhost:6650").Topic("events").SubscriptionName("dispatch");
		});

		// Nothing is read before the container is built, so there is no eager application.
		invocations.ShouldBe(0);

		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOptionsMonitor<PulsarOptions>>().Get("events");

		// Exactly one, against the served instance. Pulsar is the counter-example that keeps the
		// assertions above honest: "twice" is a consequence of eager branching, not a blanket rule.
		invocations.ShouldBe(1);
	}
}
