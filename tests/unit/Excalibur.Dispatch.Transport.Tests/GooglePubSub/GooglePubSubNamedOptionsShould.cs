// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// The Google Pub/Sub transport registered its options UNNAMED, so a host adding two named transports
/// wrote both configurations into one instance and the second silently won. The XML example on
/// <c>AddGooglePubSubTransport</c> demonstrates exactly that two-transport scenario, so the defect was
/// reachable from the documented usage.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class GooglePubSubNamedOptionsShould
{
	private static GooglePubSubOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>().Get(name);

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("orders", pubsub => pubsub
			.ProjectId("orders-project")
			.TopicId("orders-topic")
			.SubscriptionId("orders-subscription"));

		_ = services.AddGooglePubSubTransport("analytics", pubsub => pubsub
			.ProjectId("analytics-project")
			.TopicId("metrics-topic")
			.SubscriptionId("metrics-subscription"));

		using var provider = services.BuildServiceProvider();

		Resolve(provider, "orders").Name.ShouldBe("orders");
		Resolve(provider, "orders").Connection.ProjectId.ShouldBe("orders-project");
		Resolve(provider, "orders").Connection.TopicId.ShouldBe("orders-topic");
		Resolve(provider, "analytics").Name.ShouldBe("analytics");
		Resolve(provider, "analytics").Connection.ProjectId.ShouldBe("analytics-project");
		Resolve(provider, "analytics").Connection.TopicId.ShouldBe("metrics-topic");
	}

	[Fact]
	public void RejectAConfigurationWithNoSubscription()
	{
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("nameless", pubsub => pubsub
			.ProjectId("some-project")
			.TopicId("some-topic"));

		using var provider = services.BuildServiceProvider();

		// Liveness's counterpart: naming the options must not stop them being validated. A registration
		// that quietly bypassed validation would pass every "the value arrives" assertion above.
		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider, "nameless"));
	}
}
