// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

/// <summary>
/// Liveness lock for the GooglePubSub transport: a value a consumer sets through the public fluent API
/// must be observable at the runtime seam — the registered <see cref="GooglePubSubOptions"/> that every
/// resolved component reads.
///
/// This began as a checklist over a hand-written field-carry: a parallel flat options type mapped only
/// some of its fields into the canonical model, and the test enumerated the rest. That carry no longer
/// exists — the builder is a view over the instance the options system owns — so the test now asserts
/// the property (the consumer's value arrives) rather than the completeness of a copy list. It is kept
/// because the property is worth holding, not because the list needs guarding.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GooglePubSubOptionsCollapseRoundTripShould
{
	[Fact]
	public void CarryEveryConfiguredFieldThroughTheBuilderToTheRegisteredOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — configure via the fluent builder + ConfigureOptions on the canonical model.
		_ = services.AddGooglePubSubTransport("orders", pubsub => pubsub
			.ProjectId("proj-1")
			.TopicId("topic-1")
			.SubscriptionId("sub-1")
			.MapTopic<string>("string-topic")
			.EnableDeadLetter("dlq-topic")
			.ConfigureOptions(o =>
			{
				o.Subscriber.MaxPullMessages = 250;
				o.Subscriber.MaxPayloadBytes = 4096;
				o.Subscriber.EnableMessageOrdering = true;
				o.Subscriber.EnableExactlyOnceDelivery = true;
				o.Subscriber.FlowControl.MaxOutstandingElementCount = 555;
				o.Subscriber.FlowControl.MaxOutstandingByteCount = 999_999;
				o.Subscriber.DeadLetter.AutoApplyPolicy = true;
				o.Subscriber.DeadLetter.MaxDeliveryAttempts = 9;
				o.Telemetry.EnableOpenTelemetry = false;
			}));

		using var provider = services.BuildServiceProvider();
		// NAMED registration: the unnamed instance is no longer the one the transport configures.
		var options = provider.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>().Get("orders");

		// Assert — each configured value is visible at the seam the transport resolves.
		options.Name.ShouldBe("orders");
		options.Connection.ProjectId.ShouldBe("proj-1");
		options.Connection.TopicId.ShouldBe("topic-1");
		options.Connection.SubscriptionId.ShouldBe("sub-1");
		options.TopicMappings.ShouldContainKeyAndValue(typeof(string), "string-topic");

		options.Subscriber.MaxPullMessages.ShouldBe(250);
		options.Subscriber.MaxPayloadBytes.ShouldBe(4096);
		options.Subscriber.EnableMessageOrdering.ShouldBeTrue();
		options.Subscriber.EnableExactlyOnceDelivery.ShouldBeTrue();
		options.Subscriber.FlowControl.MaxOutstandingElementCount.ShouldBe(555);
		options.Subscriber.FlowControl.MaxOutstandingByteCount.ShouldBe(999_999);

		options.Subscriber.DeadLetter.Enable.ShouldBeTrue();
		options.Subscriber.DeadLetter.TopicId.ShouldBe("dlq-topic");
		options.Subscriber.DeadLetter.AutoApplyPolicy.ShouldBeTrue();
		options.Subscriber.DeadLetter.MaxDeliveryAttempts.ShouldBe(9);

		options.Telemetry.EnableOpenTelemetry.ShouldBeFalse();
	}
}
