// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSnsMessageBusShould : IAsyncDisposable
{
	private readonly IAmazonSimpleNotificationService _snsClient;
	private readonly IPayloadSerializer _serializer;
	private readonly AwsSnsOptions _options;
	private readonly AwsSnsMessageBus _bus;

	public AwsSnsMessageBusShould()
	{
		_snsClient = A.Fake<IAmazonSimpleNotificationService>();
		_serializer = A.Fake<IPayloadSerializer>();
		_options = new AwsSnsOptions { TopicArn = "arn:aws:sns:us-east-1:123456789:test-topic" };

		A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._))
			.Returns([1, 2, 3]);

		A.CallTo(() => _snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new PublishResponse()));

		_bus = new AwsSnsMessageBus(
			_snsClient,
			_serializer,
			_options,
			NullLogger<AwsSnsMessageBus>.Instance);
	}

	[Fact]
	public async Task PublishActionSuccessfully()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();

		// Act
		await _bus.PublishAsync(action, context, CancellationToken.None);

		// Assert
		A.CallTo(() => _snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishEventSuccessfully()
	{
		// Arrange
		var evt = A.Fake<IDispatchEvent>();
		var context = A.Fake<IMessageContext>();

		// Act
		await _bus.PublishAsync(evt, context, CancellationToken.None);

		// Assert
		A.CallTo(() => _snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishDocumentSuccessfully()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();

		// Act
		await _bus.PublishAsync(doc, context, CancellationToken.None);

		// Assert
		A.CallTo(() => _snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenActionIsNull()
	{
		var context = A.Fake<IMessageContext>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => _bus.PublishAsync((IDispatchAction)null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEventIsNull()
	{
		var context = A.Fake<IMessageContext>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => _bus.PublishAsync((IDispatchEvent)null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDocumentIsNull()
	{
		var context = A.Fake<IMessageContext>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => _bus.PublishAsync((IDispatchDocument)null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenContextIsNullForAction()
	{
		var action = A.Fake<IDispatchAction>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => _bus.PublishAsync(action, null!, CancellationToken.None));
	}

	[Fact]
	public async Task DisposeClient()
	{
		// Act
		await _bus.DisposeAsync();

		// Assert
		A.CallTo(() => _snsClient.Dispose()).MustHaveHappenedOnceExactly();
	}

	public async ValueTask DisposeAsync()
	{
		await _bus.DisposeAsync();
		_snsClient.Dispose();
	}
	/// <summary>
	/// SAFETY-CRITICAL, and the reason this arm exists: it fails when the mapper is never CALLED.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The conformance suite already checks that a constructor accepts the bridge. A bus can satisfy that,
	/// store the bridge, log that it resolved it, and publish the native envelope anyway — which is exactly
	/// what the Kafka bus did. So the predicate that matters is not the constructor's shape, it is whether the
	/// CloudEvent reaches the wire.
	/// </para>
	/// <para>
	/// Only the AWS SDK boundary is faked. The mapper, the bridge and the envelope converter are the real
	/// production types, so this also fails if the mapper is invoked but drops the attributes.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventAttributesOnDefaultSend()
	{
		var cloudEventOptions = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());
		var mapper = new AwsSnsCloudEventAdapter(cloudEventOptions, NullLogger<AwsSnsCloudEventAdapter>.Instance);
		var bridge = new EnvelopeCloudEventBridge(
			new CloudEventEnvelopeConverter(cloudEventOptions.Value),
			[new CloudEventEncoderAdapter<PublishRequest>(mapper)]);

		await using var bus = new AwsSnsMessageBus(
			_snsClient,
			_serializer,
			_options,
			NullLogger<AwsSnsMessageBus>.Instance,
			bridge,
			mapper);

		PublishRequest? captured = null;
		_ = A.CallTo(() => _snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.Invokes((PublishRequest r, CancellationToken _) => captured = r)
			.Returns(Task.FromResult(new PublishResponse()));

		var evt = new TestCloudEventBusEvent();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());

		await bus.PublishAsync(evt, context, CancellationToken.None);

		_ = captured.ShouldNotBeNull();
		captured.MessageAttributes.ShouldContainKey("ce-specversion");
		captured.MessageAttributes.ShouldContainKey("ce-id");
		captured.MessageAttributes["ce-type"].StringValue.ShouldContain(nameof(TestCloudEventBusEvent));

		// The topic is set by the bus AFTER the mapper builds the request; a mapper that is bypassed
		// would still produce a request with the right topic, so this is not the load-bearing assertion —
		// it guards the one line of the CloudEvents path the bus owns.
		captured.TopicArn.ShouldBe(_options.TopicArn);
	}

	/// <summary>
	/// LIVENESS. Without a bridge the bus still publishes, so the arm above cannot be satisfied by a bus that
	/// simply stopped sending.
	/// </summary>
	[Fact]
	public async Task PublishEvent_WhenCloudEventsNotConfigured_StillPublishesTheNativeEnvelope()
	{
		PublishRequest? captured = null;
		_ = A.CallTo(() => _snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.Invokes((PublishRequest r, CancellationToken _) => captured = r)
			.Returns(Task.FromResult(new PublishResponse()));

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());

		await _bus.PublishAsync(new TestCloudEventBusEvent(), context, CancellationToken.None);

		_ = captured.ShouldNotBeNull();
		// The native path leaves MessageAttributes null rather than empty, so the absence has to be
		// asserted on the nullable dictionary itself.
		(captured.MessageAttributes?.ContainsKey("ce-specversion") ?? false).ShouldBeFalse(
			"a bus with no bridge registered must not emit CloudEvent attributes");
	}

	private sealed class TestCloudEventBusEvent : IDispatchEvent
	{
	}
}
