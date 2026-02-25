// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox.MultiTransport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Outbox.Tests.MultiTransport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiTransportOutboxStoreDepthShould
{
	private readonly IOutboxStore _innerStore = A.Fake<IOutboxStore>();
	private readonly MultiTransportOutboxOptions _options = new();
	private readonly MultiTransportOutboxStore _sut;

	public MultiTransportOutboxStoreDepthShould()
	{
		_sut = new MultiTransportOutboxStore(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);
	}

	[Fact]
	public void ThrowWhenInnerStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiTransportOutboxStore(
				null!,
				Options.Create(new MultiTransportOutboxOptions()),
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
				Options.Create(new MultiTransportOutboxOptions()),
				null!));
	}

	[Fact]
	public async Task PublishToSingleTransport()
	{
		// Arrange
		var message = new OutboundMessage("test-type", "data"u8.ToArray(), "dest", new Dictionary<string, object>())
		{
			Id = "msg-1"
		};

		A.CallTo(() => _innerStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.PublishToTransportAsync("kafka", message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("kafka");
		message.IsMultiTransport.ShouldBeFalse();
		A.CallTo(() => _innerStore.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishToMultipleTransports()
	{
		// Arrange
		var message = new OutboundMessage("test-type", "data"u8.ToArray(), "dest", new Dictionary<string, object>())
		{
			Id = "msg-1"
		};

		A.CallTo(() => _innerStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.PublishToTransportsAsync(["kafka", "rabbitmq"], message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("kafka,rabbitmq");
		message.IsMultiTransport.ShouldBeTrue();
		A.CallTo(() => _innerStore.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenPublishToTransportsWithEmptyList()
	{
		// Arrange
		var message = new OutboundMessage("test-type", "data"u8.ToArray(), "dest", new Dictionary<string, object>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			async () => await _sut.PublishToTransportsAsync([], message, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenTransportNameIsNull()
	{
		var message = new OutboundMessage("test-type", "data"u8.ToArray(), "dest", new Dictionary<string, object>());
		await Should.ThrowAsync<ArgumentException>(
			async () => await _sut.PublishToTransportAsync(null!, message, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenMessageIsNullForPublishToTransport()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.PublishToTransportAsync("kafka", null!, CancellationToken.None));
	}

	[Fact]
	public void GetRegisteredTransports()
	{
		// Arrange
		_options.DefaultTransport = "kafka";
		_options.TransportBindings["MyEvent"] = "rabbitmq";

		var sut = new MultiTransportOutboxStore(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);

		// Act
		var transports = sut.GetRegisteredTransports();

		// Assert
		transports.ShouldContain("kafka");
		transports.ShouldContain("rabbitmq");
	}

	[Fact]
	public async Task StageMessageWithTransportBinding()
	{
		// Arrange
		_options.TransportBindings["OrderCreated"] = "kafka";

		var sut = new MultiTransportOutboxStore(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);

		var message = new OutboundMessage("OrderCreated", "data"u8.ToArray(), "dest", new Dictionary<string, object>());

		A.CallTo(() => _innerStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("kafka");
	}

	[Fact]
	public async Task UseDefaultTransportWhenNoBindingFound()
	{
		// Arrange
		_options.DefaultTransport = "rabbitmq";

		var sut = new MultiTransportOutboxStore(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);

		var message = new OutboundMessage("UnknownEvent", "data"u8.ToArray(), "dest", new Dictionary<string, object>());

		A.CallTo(() => _innerStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task ThrowWhenRequireExplicitBindingsAndNoBinding()
	{
		// Arrange
		_options.RequireExplicitBindings = true;

		var sut = new MultiTransportOutboxStore(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);

		var message = new OutboundMessage("UnknownEvent", "data"u8.ToArray(), "dest", new Dictionary<string, object>());

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			async () => await sut.StageMessageAsync(message, CancellationToken.None));
	}

	[Fact]
	public async Task MatchWildcardTransportBinding()
	{
		// Arrange
		_options.TransportBindings["Orders.*"] = "kafka";

		var sut = new MultiTransportOutboxStore(
			_innerStore,
			Options.Create(_options),
			NullLogger<MultiTransportOutboxStore>.Instance);

		var message = new OutboundMessage("Orders.Created", "data"u8.ToArray(), "dest", new Dictionary<string, object>());

		A.CallTo(() => _innerStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None);

		// Assert
		message.TargetTransports.ShouldBe("kafka");
	}

	[Fact]
	public async Task DelegateGetUnsentMessagesToInnerStore()
	{
		// Arrange
		A.CallTo(() => _innerStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));

		// Act
		var result = await _sut.GetUnsentMessagesAsync(10, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerStore.GetUnsentMessagesAsync(10, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateMarkSentToInnerStore()
	{
		// Arrange
		A.CallTo(() => _innerStore.MarkSentAsync(A<string>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.MarkSentAsync("msg-1", CancellationToken.None);

		// Assert
		A.CallTo(() => _innerStore.MarkSentAsync("msg-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateMarkFailedToInnerStore()
	{
		// Arrange
		A.CallTo(() => _innerStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.MarkFailedAsync("msg-1", "error", 1, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerStore.MarkFailedAsync("msg-1", "error", 1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}

#pragma warning restore CA2012
