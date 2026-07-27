// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

/// <summary>
/// Regression lock for the GooglePubSub options collapse: every configuration field must survive the
/// builder → registered <see cref="GooglePubSubOptions"/> round-trip. Before the collapse a parallel flat
/// options type mapped only a subset of its fields into the canonical model, silently dropping the rest;
/// this test asserts the mapping is now lossless.
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

		// Act — configure every field via the fluent builder + ConfigureOptions on the canonical model.
		_ = services.AddGooglePubSubTransport("orders", pubsub => pubsub
			.ProjectId("proj-1")
			.TopicId("topic-1")
			.SubscriptionId("sub-1")
			.MapTopic<string>("string-topic")
			.EnableDeadLetter("dlq-topic")
			.ConfigureOptions(o =>
			{
				o.MaxConcurrentMessages = 7;
				o.EnableEncryption = true;
				o.Subscriber.MaxPullMessages = 250;
				o.Subscriber.AckDeadlineSeconds = 123;
				o.Subscriber.EnableAutoAckExtension = false;
				o.Subscriber.MaxConcurrentAcks = 42;
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
		var options = provider.GetRequiredService<IOptions<GooglePubSubOptions>>().Value;

		// Assert — every former flat field is carried (lossless collapse).
		options.Name.ShouldBe("orders");
		options.Connection.ProjectId.ShouldBe("proj-1");
		options.Connection.TopicId.ShouldBe("topic-1");
		options.Connection.SubscriptionId.ShouldBe("sub-1");
		options.MaxConcurrentMessages.ShouldBe(7);
		options.EnableEncryption.ShouldBeTrue();
		options.TopicMappings.ShouldContainKeyAndValue(typeof(string), "string-topic");

		options.Subscriber.MaxPullMessages.ShouldBe(250);
		options.Subscriber.AckDeadlineSeconds.ShouldBe(123);
		options.Subscriber.EnableAutoAckExtension.ShouldBeFalse();
		options.Subscriber.MaxConcurrentAcks.ShouldBe(42);
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
