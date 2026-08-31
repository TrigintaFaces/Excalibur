// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Liveness locks for the Google Pub/Sub options collapse. The registration used to enumerate roughly
/// twenty properties from a private options instance onto the instance the container serves — an
/// identity function over two instances of the same type, guarded only by a hand-maintained list. The
/// builder is now a view over the served instance. These assert the property that matters: a value set
/// through the public fluent API is observable where it is consumed.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class GooglePubSubOptionsBuilderViewShould
{
	// The transport registers its options NAMED, so two named transports no longer collapse onto one
	// instance. Resolving the unnamed instance here would read a configuration nobody registered.
	private static GooglePubSubOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>().Get(name);

	[Fact]
	public void CarryEveryFluentlyConfiguredValueToTheResolvedOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("events", pubsub => pubsub
			.ProjectId("my-project")
			.TopicId("my-topic")
			.SubscriptionId("my-subscription")
			.MapTopic<string>("string-topic"));

		using var provider = services.BuildServiceProvider();
		var options = Resolve(provider, "events");

		options.Name.ShouldBe("events");
		options.Connection.ProjectId.ShouldBe("my-project");
		options.Connection.TopicId.ShouldBe("my-topic");
		options.Connection.SubscriptionId.ShouldBe("my-subscription");

		// MapTopic appends to a routing table rather than assigning, so it is the one call that was
		// never at risk from the copy list. Asserted alongside the setters it used to travel with.
		options.TopicMappings.ShouldContainKeyAndValue(typeof(string), "string-topic");
	}

	[Fact]
	public void CarryNestedSubscriberTuningThatTheOldCopyListEnumeratedByHand()
	{
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("tuned", pubsub => pubsub
			.ProjectId("my-project")
			.TopicId("my-topic")
			.SubscriptionId("my-subscription")
			.ConfigureOptions(o =>
			{
				o.Subscriber.MaxPullMessages = 33;
				o.Subscriber.EnableExactlyOnceDelivery = true;
			}));

		using var provider = services.BuildServiceProvider();
		var options = Resolve(provider, "tuned");

		// Each of these was a separate hand-written assignment in the deleted list; a property added
		// to the model but forgotten there was silently dropped. There is no list to forget now.
		options.Subscriber.MaxPullMessages.ShouldBe(33);
		options.Subscriber.EnableExactlyOnceDelivery.ShouldBeTrue();
	}

	[Fact]
	public void CarryTheDeadLetterPolicyThatBranchesTheRegistrationGraph()
	{
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("dlq", pubsub => pubsub
			.ProjectId("my-project")
			.TopicId("my-topic")
			.SubscriptionId("my-subscription")
			.EnableDeadLetter("my-dead-letter-topic"));

		using var provider = services.BuildServiceProvider();

		// This value is read eagerly, before the container is built, to decide whether the dead-letter
		// services are registered at all — and again against the served instance. Both readings come
		// from the consumer's own delegate, so the served instance must agree with the eager one.
		var deadLetter = Resolve(provider, "dlq").Subscriber.DeadLetter;

		// Both fields are load-bearing and are set by the one fluent call: the subscriber skips the
		// dead-letter path entirely unless Enable is set, so carrying the topic without the flag would
		// be a silent no-op rather than an error.
		deadLetter.Enable.ShouldBeTrue();
		deadLetter.TopicId.ShouldBe("my-dead-letter-topic");
	}
}
