using System.Text;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport.Aws;

using FakeItEasy;

using Microsoft.Extensions.Logging;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Unit tests for <see cref="AwsEventBridgeMessageBus" />.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsEventBridgeMessageBusShould : UnitTestBase
{
	[Fact]
	public async Task PublishAsync_UsesDefaultsAndCreatesArchive()
	{
		// Arrange
		var eventBridgeClient = A.Fake<IAmazonEventBridge>();
		var serializer = A.Fake<IPayloadSerializer>();
		PutEventsRequest? capturedPutRequest = null;
		CreateArchiveRequest? capturedArchiveRequest = null;

		_ = A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._))
				.Returns(Encoding.UTF8.GetBytes("payload"));

		_ = A.CallTo(() => eventBridgeClient.DescribeArchiveAsync(A<DescribeArchiveRequest>._, A<CancellationToken>._))
				.ThrowsAsync(new ResourceNotFoundException("missing"));

		_ = A.CallTo(() => eventBridgeClient.DescribeEventBusAsync(A<DescribeEventBusRequest>._, A<CancellationToken>._))
				.Returns(new DescribeEventBusResponse { Arn = "arn:aws:events:us-east-1:123456789:event-bus/dispatch" });

		_ = A.CallTo(() => eventBridgeClient.CreateArchiveAsync(A<CreateArchiveRequest>._, A<CancellationToken>._))
				.Invokes((CreateArchiveRequest request, CancellationToken _) => capturedArchiveRequest = request)
				.Returns(new CreateArchiveResponse());

		_ = A.CallTo(() => eventBridgeClient.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._))
				.Invokes((PutEventsRequest request, CancellationToken _) => capturedPutRequest = request)
				.Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [new PutEventsResultEntry { EventId = "accepted" }] });

		var options = new AwsEventBridgeOptions
		{
			EventBusName = "dispatch",
			DefaultSource = "dispatch-default",
			DefaultDetailType = "dispatch.detail",
			EnableArchiving = true,
			ArchiveName = "dispatch-archive",
			ArchiveRetentionDays = 10,
		};

		var bus = new AwsEventBridgeMessageBus(
				eventBridgeClient,
				serializer,
				options,
				A.Fake<ILogger<AwsEventBridgeMessageBus>>());

		var context = new MessageContext();

		// Act
		await bus.PublishAsync(new TestAction(), context, CancellationToken.None);

		// Assert
		_ = capturedPutRequest.ShouldNotBeNull();
		_ = capturedPutRequest.Entries.ShouldNotBeNull();
		capturedPutRequest.Entries.Count.ShouldBe(1);
		capturedPutRequest.Entries[0].Source.ShouldBe("dispatch-default");
		capturedPutRequest.Entries[0].DetailType.ShouldBe("dispatch.detail");

		_ = capturedArchiveRequest.ShouldNotBeNull();
		capturedArchiveRequest.ArchiveName.ShouldBe("dispatch-archive");
		capturedArchiveRequest.EventPattern.ShouldBe("{}");
		capturedArchiveRequest.RetentionDays.ShouldBe(10);
		capturedArchiveRequest.EventSourceArn.ShouldBe("arn:aws:events:us-east-1:123456789:event-bus/dispatch");
	}

	[Fact]
	public async Task PublishAsync_TargetsTheConfiguredBus_WhenPublishingAsACloudEvent()
	{
		// The CloudEvents path builds its entry from a DIFFERENT options object than the native paths, and
		// that object's EventBusName defaults to the literal "default". Nothing copies the configured bus
		// across, so before the fix a consumer who set a bus for the native path had every CloudEvent land
		// on "default" -- silently, because an API acknowledgement does not prove target rule matching.
		// This asserts the bus the CONSUMER configured, which is the requirement; asserting
		// merely that some bus name is present would pass on the defect.
		var eventBridgeClient = A.Fake<IAmazonEventBridge>();
		var serializer = A.Fake<IPayloadSerializer>();
		PutEventsRequest? capturedPutRequest = null;

		_ = A.CallTo(() => eventBridgeClient.DescribeEventBusAsync(A<DescribeEventBusRequest>._, A<CancellationToken>._))
				.Returns(new DescribeEventBusResponse { Arn = "arn:aws:events:us-east-1:123456789:event-bus/orders-bus" });

		_ = A.CallTo(() => eventBridgeClient.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._))
				.Invokes((PutEventsRequest request, CancellationToken _) => capturedPutRequest = request)
				.Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [new PutEventsResultEntry { EventId = "accepted" }] });

		// The bridge returns an entry carrying the CloudEvents options' own default -- exactly what the
		// real adapter produces, and the value the fix must override.
		var bridge = A.Fake<IEnvelopeCloudEventBridge>();
		_ = A.CallTo(() => bridge.ToTransportAsync<PutEventsRequestEntry>(
					A<MessageEnvelope>._, A<CloudEventMode>._, A<CancellationToken>._))
				.Returns(new PutEventsRequestEntry { EventBusName = "default", Source = "ce", DetailType = "ce.detail" });

		var encoder = A.Fake<ICloudEventEncoder<PutEventsRequestEntry>>();
		_ = A.CallTo(() => encoder.Options).Returns(new CloudEventOptions());

		var options = new AwsEventBridgeOptions
		{
			EventBusName = "orders-bus",
			DefaultSource = "dispatch-default",
			DefaultDetailType = "dispatch.detail",
			EnableArchiving = false,
		};

		var bus = new AwsEventBridgeMessageBus(
				eventBridgeClient,
				serializer,
				options,
				A.Fake<ILogger<AwsEventBridgeMessageBus>>(),
				bridge,
				encoder);

		await bus.PublishAsync(new TestAction(), new MessageContext(), CancellationToken.None);

		_ = capturedPutRequest.ShouldNotBeNull();
		capturedPutRequest.Entries.Count.ShouldBe(1);
		capturedPutRequest.Entries[0].EventBusName.ShouldBe(
			"orders-bus",
			"a CloudEvents publish must target the bus the consumer configured, not the CloudEvents options' "
			+ "own \"default\" -- landing on the wrong bus is silent, because the response is never inspected");
	}

	/// <summary>
	/// Proves the CloudEvents mapper is INVOKED on the EventBridge publish path, not merely accepted by the
	/// constructor.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The sibling arm above fakes <c>IEnvelopeCloudEventBridge</c> and asserts the bus OVERRIDES the entry
	/// the fake returns. That is a statement about option precedence: a bus that never called the bridge at
	/// all would build the same entry from its options and pass it unchanged. Only the AWS SDK boundary is
	/// faked here, so the mapper, the bridge and the envelope converter are the real production types and a
	/// bypassed mapper leaves the CloudEvent attributes off the wire.
	/// </para>
	/// <para>
	/// EventBridge is payload-only: it writes no transport headers, serialising the whole CloudEvent into the
	/// entry's <c>Detail</c> JSON. The assertion therefore lands on the body rather than on attribute keys,
	/// which is what distinguishes this transport from the SNS and Event Hubs arms.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventAttributesInTheEntryDetail()
	{
		var eventBridgeClient = A.Fake<IAmazonEventBridge>();
		PutEventsRequest? captured = null;

		_ = A.CallTo(() => eventBridgeClient.DescribeEventBusAsync(A<DescribeEventBusRequest>._, A<CancellationToken>._))
				.Returns(new DescribeEventBusResponse { Arn = "arn:aws:events:us-east-1:123456789:event-bus/orders-bus" });

		_ = A.CallTo(() => eventBridgeClient.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._))
				.Invokes((PutEventsRequest request, CancellationToken _) => captured = request)
				.Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [new PutEventsResultEntry { EventId = "accepted" }] });

		var cloudEventOptions = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());
		var mapper = new AwsEventBridgeCloudEventAdapter(
			cloudEventOptions,
			Microsoft.Extensions.Options.Options.Create(new AwsEventBridgeCloudEventOptions()),
			A.Fake<ILogger<AwsEventBridgeCloudEventAdapter>>());

		var bridge = new EnvelopeCloudEventBridge(
			new CloudEventEnvelopeConverter(cloudEventOptions.Value),
			[new CloudEventEncoderAdapter<PutEventsRequestEntry>(mapper)]);

		var bus = new AwsEventBridgeMessageBus(
				eventBridgeClient,
				A.Fake<IPayloadSerializer>(),
				new AwsEventBridgeOptions
				{
					EventBusName = "orders-bus",
					DefaultSource = "dispatch-default",
					DefaultDetailType = "dispatch.detail",
					EnableArchiving = false,
				},
				A.Fake<ILogger<AwsEventBridgeMessageBus>>(),
				bridge,
				mapper);

		await bus.PublishAsync(new TestCloudEventBusEvent(), new MessageContext(), CancellationToken.None);

		_ = captured.ShouldNotBeNull();
		captured.Entries.Count.ShouldBe(1);

		var detail = captured.Entries[0].Detail;
		_ = detail.ShouldNotBeNull();

		// The spec-version field is written by the mapper alone. A bus that skipped it still produces a
		// well-formed entry, so its absence is the signal that the mapper was never reached.
		detail.ShouldContain("specversion", Case.Insensitive);
		detail.ShouldContain(nameof(TestCloudEventBusEvent), Case.Insensitive);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task PropagateRejectedIngestionFromEveryCloudEventsPublishPath(int path)
	{
		var client = A.Fake<IAmazonEventBridge>();
		A.CallTo(() => client.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._))
			.Returns(new PutEventsResponse
			{
				FailedEntryCount = 1,
				Entries = [new PutEventsResultEntry { ErrorCode = "InternalFailure", ErrorMessage = "controlled rejection" }],
			});
		var bridge = A.Fake<IEnvelopeCloudEventBridge>();
		A.CallTo(() => bridge.ToTransportAsync<PutEventsRequestEntry>(A<MessageEnvelope>._, A<CloudEventMode>._, A<CancellationToken>._))
			.Returns(new PutEventsRequestEntry { Detail = "{}" });
		var encoder = A.Fake<ICloudEventEncoder<PutEventsRequestEntry>>();
		A.CallTo(() => encoder.Options).Returns(new CloudEventOptions());
		await using var bus = new AwsEventBridgeMessageBus(client, A.Fake<IPayloadSerializer>(),
			new AwsEventBridgeOptions { EventBusName = "orders", EnableArchiving = false },
			A.Fake<ILogger<AwsEventBridgeMessageBus>>(), bridge, encoder);
		var context = new MessageContext();
		var error = await Should.ThrowAsync<AmazonEventBridgeException>(() => path switch
		{
			0 => bus.PublishAsync(new TestAction(), context, TestContext.Current.CancellationToken),
			1 => bus.PublishAsync(new TestCloudEventBusEvent(), context, TestContext.Current.CancellationToken),
			_ => bus.PublishAsync(new TestDocument(), context, TestContext.Current.CancellationToken),
		});
		error.ErrorCode.ShouldBe("InternalFailure");
		A.CallTo(() => client.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveAwsErrorDetailsWithoutRetryingAnAcknowledgementWithAMissingCount()
	{
		var client = A.Fake<IAmazonEventBridge>();
		A.CallTo(() => client.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._))
			.Returns(new PutEventsResponse
			{
				Entries = [new PutEventsResultEntry { ErrorCode = "InternalFailure", ErrorMessage = "controlled response details" }],
			});
		var serializer = A.Fake<IPayloadSerializer>();
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns(new byte[] { 1, 2 });
		await using var bus = new AwsEventBridgeMessageBus(client, serializer,
			new AwsEventBridgeOptions { EventBusName = "orders", EnableArchiving = false }, A.Fake<ILogger<AwsEventBridgeMessageBus>>());
		var error = await Should.ThrowAsync<global::Excalibur.Dispatch.Exceptions.DispatchDeliveryUncertainException>(() =>
			bus.PublishAsync(new TestAction(), new MessageContext(), TestContext.Current.CancellationToken));
		var awsError = error.InnerException.ShouldBeOfType<AmazonEventBridgeException>();
		awsError.ErrorCode.ShouldBe("InternalFailure");
		awsError.Message.ShouldBe("controlled response details");
		A.CallTo(() => client.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[MessageName("tests.AwsEventBridgeMessageBusShould.TestDocument")]
	private sealed class TestDocument : IDispatchDocument;

	[MessageName("tests.AwsEventBridgeMessageBusShould.TestCloudEventBusEvent")]
	private sealed class TestCloudEventBusEvent : IDispatchEvent
	{
	}

	[MessageName("tests.AwsEventBridgeMessageBusShould.TestAction")]
	private sealed class TestAction : IDispatchAction
	{
	}
}
