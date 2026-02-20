// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox.MultiTransport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.MultiTransport;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class MultiTransportOutboxStoreShould
{
	private readonly IOutboxStore _innerStore;
	private readonly MultiTransportOutboxOptions _options;

	public MultiTransportOutboxStoreShould()
	{
		_innerStore = A.Fake<IOutboxStore>();
		_options = new MultiTransportOutboxOptions
		{
			DefaultTransport = "kafka",
		};
	}

	[Fact]
	public void ThrowWhenInnerStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiTransportOutboxStore(
				null!,
				Options.Create(_options),
				NullLogger<MultiTransportOutboxStore>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiTransportOutboxStore(
				_innerStore,
				null!,
				NullLogger<MultiTransportOutboxStore>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiTransportOutboxStore(
				_innerStore,
				Options.Create(_options),
				null!));
	}

	[Fact]
	public async Task PublishToSingleTransport()
	{
		// Arrange
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1", MessageType = "OrderCreated" };

		// Act
		await sut.PublishToTransportAsync("rabbitmq", message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("rabbitmq");
		message.IsMultiTransport.ShouldBeFalse();
		A.CallTo(() => _innerStore.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenTransportNameIsEmpty()
	{
		// Arrange
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			sut.PublishToTransportAsync("", message, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowWhenMessageIsNullForPublishToTransport()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.PublishToTransportAsync("kafka", null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task PublishToMultipleTransports()
	{
		// Arrange
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1", MessageType = "OrderCreated" };
		string[] transports = ["kafka", "rabbitmq"];

		// Act
		await sut.PublishToTransportsAsync(transports, message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("kafka,rabbitmq");
		message.IsMultiTransport.ShouldBeTrue();
		A.CallTo(() => _innerStore.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenTransportNamesIsEmpty()
	{
		// Arrange
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			sut.PublishToTransportsAsync([], message, CancellationToken.None).AsTask());
	}

	[Fact]
	public void GetRegisteredTransports()
	{
		// Arrange
		_options.TransportBindings["OrderCreated"] = "rabbitmq";
		_options.TransportBindings["Payment*"] = "azure";
		var sut = CreateSut();

		// Act
		var transports = sut.GetRegisteredTransports();

		// Assert
		transports.ShouldContain("kafka"); // default
		transports.ShouldContain("rabbitmq");
		transports.ShouldContain("azure");
	}

	[Fact]
	public async Task ResolveTransportByExactMatch()
	{
		// Arrange
		_options.TransportBindings["OrderCreated"] = "rabbitmq";
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1", MessageType = "OrderCreated" };

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task ResolveTransportByWildcardMatch()
	{
		// Arrange
		_options.TransportBindings["Order*"] = "azure";
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1", MessageType = "OrderShipped" };

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("azure");
	}

	[Fact]
	public async Task FallBackToDefaultTransport()
	{
		// Arrange
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1", MessageType = "UnknownEvent" };

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("kafka"); // default transport
	}

	[Fact]
	public async Task ThrowWhenRequireExplicitBindingsAndNoMatch()
	{
		// Arrange
		_options.RequireExplicitBindings = true;
		var sut = CreateSut();
		var message = new OutboundMessage { Id = "msg-1", MessageType = "UnknownEvent" };

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.StageMessageAsync(message, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DelegateGetUnsentMessagesToInnerStore()
	{
		// Arrange
		var expected = new List<OutboundMessage> { new() { Id = "msg-1" } };
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetUnsentMessagesAsync(10, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(expected));
#pragma warning restore CA2012
		var sut = CreateSut();

		// Act
		var result = await sut.GetUnsentMessagesAsync(10, CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task DelegateMarkSentToInnerStore()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.MarkSentAsync("msg-1", CancellationToken.None);

		// Assert
		A.CallTo(() => _innerStore.MarkSentAsync("msg-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateMarkFailedToInnerStore()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.MarkFailedAsync("msg-1", "error", 3, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerStore.MarkFailedAsync("msg-1", "error", 3, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private MultiTransportOutboxStore CreateSut() =>
		new(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);
}
