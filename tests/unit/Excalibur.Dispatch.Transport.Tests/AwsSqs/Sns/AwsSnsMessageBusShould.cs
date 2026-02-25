// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
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
}
