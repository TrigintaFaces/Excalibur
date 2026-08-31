// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>
/// Liveness locks for the Pulsar options collapse. The registration used to build a private options
/// instance from the builder calls and then assign six named properties onto the instance the container
/// serves; a property the builder could set but that list omitted was collected and discarded. The
/// builder is now a view over the served instance, so these assert the property that matters — a value
/// set through the public fluent API is observable where it is consumed.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class PulsarOptionsBuilderViewShould
{
	private static PulsarOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<PulsarOptions>>().Get(name);

	[Fact]
	public void CarryEveryFluentlyConfiguredValueToTheResolvedNamedOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddPulsarTransport("orders", pulsar => pulsar
			.ServiceUrl("pulsar://broker:6650")
			.Topic("orders-topic")
			.SubscriptionName("orders-subscription")
			.SubscriptionType(PulsarSubscriptionType.KeyShared)
			.SubscriptionInitialPosition(PulsarSubscriptionInitialPosition.Earliest));

		using var provider = services.BuildServiceProvider();
		var options = Resolve(provider, "orders");

		options.ServiceUrl.ShouldBe("pulsar://broker:6650");
		options.Topic.ShouldBe("orders-topic");
		options.SubscriptionName.ShouldBe("orders-subscription");
		options.SubscriptionType.ShouldBe(PulsarSubscriptionType.KeyShared);
		options.SubscriptionInitialPosition.ShouldBe(PulsarSubscriptionInitialPosition.Earliest);
	}

	[Fact]
	public void CarryNestedReceiveTuningSetThroughTheGroupedConfigureCall()
	{
		var services = new ServiceCollection();

		_ = services.AddPulsarTransport("tuned", pulsar => pulsar
			.ServiceUrl("pulsar://broker:6650")
			.Topic("tuned-topic")
			.SubscriptionName("tuned-subscription")
			.ConfigureReceive(receive => receive.MaxBatchSize = 42));

		using var provider = services.BuildServiceProvider();

		// The nested group was carried by reference in the old copy list, which is why it survived
		// where the flat properties were at risk. Asserted so the grouped path stays covered too.
		Resolve(provider, "tuned").Receive.MaxBatchSize.ShouldBe(42);
	}

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();

		_ = services.AddPulsarTransport("a", p => p
			.ServiceUrl("pulsar://a:6650").Topic("topic-a").SubscriptionName("sub-a"));
		_ = services.AddPulsarTransport("b", p => p
			.ServiceUrl("pulsar://b:6650").Topic("topic-b").SubscriptionName("sub-b"));

		using var provider = services.BuildServiceProvider();

		Resolve(provider, "a").Topic.ShouldBe("topic-a");
		Resolve(provider, "b").Topic.ShouldBe("topic-b");
	}

	[Fact]
	public void RejectAConfigurationWithAnUnusableReceiveBatchSize()
	{
		var services = new ServiceCollection();

		// The flat string setters guard at call time, so they cannot reach the validator through the
		// fluent API at all. The grouped receive options have no such guard, which makes this the
		// validator's reachable arm — and the group the collapse carries by reference.
		_ = services.AddPulsarTransport("invalid", pulsar => pulsar
			.ServiceUrl("pulsar://broker:6650")
			.Topic("some-topic")
			.SubscriptionName("some-subscription")
			.ConfigureReceive(receive => receive.MaxBatchSize = 0));

		using var provider = services.BuildServiceProvider();

		// Safety's counterpart to the liveness assertions above: a collapse that quietly stopped
		// validating would satisfy every "the value arrives" test and still ship a broken transport.
		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider, "invalid"));
	}
}
