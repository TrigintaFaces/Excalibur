// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsMessageBusShould : IAsyncDisposable
{
	private readonly IAmazonSQS _sqsClient;
	private readonly IPayloadSerializer _serializer;
	private readonly AwsSqsOptions _options;
	private readonly AwsSqsMessageBus _bus;

	public AwsSqsMessageBusShould()
	{
		_sqsClient = A.Fake<IAmazonSQS>();
		_serializer = A.Fake<IPayloadSerializer>();
		_options = new AwsSqsOptions { QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue") };

		A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._))
			.Returns([1, 2, 3]);

		A.CallTo(() => _sqsClient.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new SendMessageResponse()));

		_bus = new AwsSqsMessageBus(
			_sqsClient,
			_serializer,
			_options,
			NullLogger<AwsSqsMessageBus>.Instance);
	}

	[Fact]
	public async Task PublishAction_InitializesMessageAttributes_AndSendsMessage()
	{
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();

		await _bus.PublishAsync(action, context, CancellationToken.None);

		A.CallTo(() => _sqsClient.SendMessageAsync(
				A<SendMessageRequest>.That.Matches(r => r.MessageAttributes != null && r.MessageAttributes.Count > 0),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishEvent_InitializesMessageAttributes_AndSendsMessage()
	{
		var evt = A.Fake<IDispatchEvent>();
		var context = A.Fake<IMessageContext>();

		await _bus.PublishAsync(evt, context, CancellationToken.None);

		A.CallTo(() => _sqsClient.SendMessageAsync(
				A<SendMessageRequest>.That.Matches(r => r.MessageAttributes != null && r.MessageAttributes.Count > 0),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishDocument_InitializesMessageAttributes_AndSendsMessage()
	{
		var doc = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();

		await _bus.PublishAsync(doc, context, CancellationToken.None);

		A.CallTo(() => _sqsClient.SendMessageAsync(
				A<SendMessageRequest>.That.Matches(r => r.MessageAttributes != null && r.MessageAttributes.Count > 0),
				A<CancellationToken>._))
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
	public async Task ThrowWhenContextIsNull()
	{
		var action = A.Fake<IDispatchAction>();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _bus.PublishAsync(action, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenQueueUrlNotConfigured()
	{
		// Arrange
		var opts = new AwsSqsOptions(); // No QueueUrl
		var bus = new AwsSqsMessageBus(
			_sqsClient, _serializer, opts, NullLogger<AwsSqsMessageBus>.Instance);
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => bus.PublishAsync(action, context, CancellationToken.None));
	}

	[Fact]
	public async Task DisposeClient()
	{
		// Act
		await _bus.DisposeAsync();

		// Assert
		A.CallTo(() => _sqsClient.Dispose()).MustHaveHappenedOnceExactly();
	}

	public async ValueTask DisposeAsync()
	{
		await _bus.DisposeAsync();
	}
}

#pragma warning restore CA2012
