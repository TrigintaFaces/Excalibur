// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Verifies the shared producer-side OpenTelemetry instrumentation used by every transport publish path.
/// The producer span and the OTel <c>messaging.*</c> semantic-convention attributes are emitted from a
/// single helper so the attribute vocabulary is identical across all transports.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CrossTransport")]
[Trait("Category", "ProducerTelemetry")]
public sealed class MessagingProducerInstrumentationShould
{
	private static ActivityListener CreateListener(ICollection<Activity> captured) =>
		new()
		{
			ShouldListenTo = source => source.Name == MessagingProducerInstrumentation.ActivitySourceName,
			Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = captured.Add,
		};

	[Fact]
	public void StartPublishActivity_EmitsProducerSpan_WithMessagingSemanticConventions()
	{
		// Arrange
		var captured = new List<Activity>();
		using var listener = CreateListener(captured);
		ActivitySource.AddActivityListener(listener);

		// Act
		using (var activity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.RabbitMq,
			destination: "orders.exchange",
			messageId: "msg-123"))
		{
			// Assert (inside scope so the activity is live)
			activity.ShouldNotBeNull();
			activity.Kind.ShouldBe(ActivityKind.Producer);
			activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.System).ShouldBe("rabbitmq");
			activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.Operation).ShouldBe("publish");
			activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.DestinationName).ShouldBe("orders.exchange");
			activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.MessageId).ShouldBe("msg-123");
		}

		captured.ShouldContain(a => a.OperationName == "publish");
	}

	[Theory]
	[InlineData("rabbitmq")]
	[InlineData("kafka")]
	[InlineData("gcp_pubsub")]
	[InlineData("servicebus")]
	[InlineData("aws_sqs")]
	[InlineData("aws_sns")]
	public void StartPublishActivity_SetsMessagingSystem_PerTransport(string messagingSystem)
	{
		// Arrange
		var captured = new List<Activity>();
		using var listener = CreateListener(captured);
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = MessagingProducerInstrumentation.StartPublishActivity(
			messagingSystem, destination: "dest", messageId: null);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.System).ShouldBe(messagingSystem);
		activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.Operation).ShouldBe("publish");
		// message.id is only set when available — null must NOT add an empty tag.
		activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.MessageId).ShouldBeNull();
	}

	[Fact]
	public void StartPublishActivity_ReturnsNull_WhenNoListenerRegistered()
	{
		// No listener for the producer source → no span, no throw (fail-open hot path).
		using var activity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.Kafka, "topic", "msg-1");

		activity.ShouldBeNull();
	}

	[Fact]
	public void StartPublishActivity_OmitsDestinationTag_WhenDestinationIsNullOrEmpty()
	{
		// Arrange
		var captured = new List<Activity>();
		using var listener = CreateListener(captured);
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSqs, destination: null, messageId: "m");

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.DestinationName).ShouldBeNull();
		activity.GetTagItem(TransportTelemetryConstants.MessagingConventions.System).ShouldBe("aws_sqs");
	}
}
