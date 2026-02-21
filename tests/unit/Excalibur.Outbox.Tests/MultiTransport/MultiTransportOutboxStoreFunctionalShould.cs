// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox.MultiTransport;

namespace Excalibur.Outbox.Tests.MultiTransport;

[Trait("Category", "Unit")]
public class MultiTransportOutboxStoreFunctionalShould
{
	private static readonly byte[] TestPayload = [0x01, 0x02];

	private static MultiTransportOutboxStore CreateStore(
		IOutboxStore innerStore,
		MultiTransportOutboxOptions? options = null)
	{
		options ??= new MultiTransportOutboxOptions();
		return new MultiTransportOutboxStore(
			innerStore,
			Options.Create(options),
			NullLogger<MultiTransportOutboxStore>.Instance);
	}

	[Fact]
	public void Constructor_WithNullInnerStore_ShouldThrow()
	{
		Should.Throw<ArgumentNullException>(() => new MultiTransportOutboxStore(
			null!,
			Options.Create(new MultiTransportOutboxOptions()),
			NullLogger<MultiTransportOutboxStore>.Instance));
	}

	[Fact]
	public void Constructor_WithNullOptions_ShouldThrow()
	{
		var inner = A.Fake<IOutboxStore>();
		Should.Throw<ArgumentNullException>(() => new MultiTransportOutboxStore(
			inner, null!, NullLogger<MultiTransportOutboxStore>.Instance));
	}

	[Fact]
	public void Constructor_WithNullLogger_ShouldThrow()
	{
		var inner = A.Fake<IOutboxStore>();
		Should.Throw<ArgumentNullException>(() => new MultiTransportOutboxStore(
			inner, Options.Create(new MultiTransportOutboxOptions()), null!));
	}

	[Fact]
	public async Task PublishToTransportAsync_ShouldSetTargetTransportAndDelegate()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);
		var message = new OutboundMessage("OrderCreated", TestPayload, "dest");

		await store.PublishToTransportAsync("kafka", message, CancellationToken.None).ConfigureAwait(true);

		message.TargetTransports.ShouldBe("kafka");
		message.IsMultiTransport.ShouldBeFalse();
		A.CallTo(() => inner.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishToTransportAsync_WithNullTransportName_ShouldThrow()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);
		var message = new OutboundMessage("OrderCreated", TestPayload, "dest");

		await Should.ThrowAsync<ArgumentException>(
			() => store.PublishToTransportAsync(null!, message, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task PublishToTransportAsync_WithNullMessage_ShouldThrow()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);

		await Should.ThrowAsync<ArgumentNullException>(
			() => store.PublishToTransportAsync("kafka", null!, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task PublishToTransportsAsync_ShouldSetMultiTransportAndDelegate()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);
		var message = new OutboundMessage("OrderCreated", TestPayload, "dest");
		var transports = new List<string> { "kafka", "rabbitmq", "azure-sb" };

		await store.PublishToTransportsAsync(transports, message, CancellationToken.None).ConfigureAwait(true);

		message.TargetTransports.ShouldContain("kafka");
		message.TargetTransports.ShouldContain("rabbitmq");
		message.TargetTransports.ShouldContain("azure-sb");
		message.IsMultiTransport.ShouldBeTrue();
		A.CallTo(() => inner.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishToTransportsAsync_WithSingleTransport_ShouldNotSetMultiTransport()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);
		var message = new OutboundMessage("OrderCreated", TestPayload, "dest");
		var transports = new List<string> { "kafka" };

		await store.PublishToTransportsAsync(transports, message, CancellationToken.None).ConfigureAwait(true);

		message.IsMultiTransport.ShouldBeFalse();
	}

	[Fact]
	public async Task PublishToTransportsAsync_WithEmptyList_ShouldThrow()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);
		var message = new OutboundMessage("OrderCreated", TestPayload, "dest");

		await Should.ThrowAsync<ArgumentException>(
			() => store.PublishToTransportsAsync(new List<string>(), message, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public void GetRegisteredTransports_ShouldIncludeDefaultTransport()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "rabbitmq" };
		var store = CreateStore(inner, options);

		var transports = store.GetRegisteredTransports();

		transports.ShouldContain("rabbitmq");
	}

	[Fact]
	public void GetRegisteredTransports_ShouldIncludeBindingTransports()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "default" };
		options.TransportBindings["OrderCreated"] = "kafka";
		options.TransportBindings["PaymentProcessed"] = "azure-sb";
		var store = CreateStore(inner, options);

		var transports = store.GetRegisteredTransports();

		transports.ShouldContain("default");
		transports.ShouldContain("kafka");
		transports.ShouldContain("azure-sb");
		transports.Count.ShouldBe(3);
	}

	[Fact]
	public void GetRegisteredTransports_ShouldDeduplicateTransportNames()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "kafka" };
		options.TransportBindings["OrderCreated"] = "kafka";
		options.TransportBindings["PaymentProcessed"] = "kafka";
		var store = CreateStore(inner, options);

		var transports = store.GetRegisteredTransports();

		transports.Count.ShouldBe(1);
		transports.ShouldContain("kafka");
	}

	[Fact]
	public async Task StageMessageAsync_ShouldResolveTransportByExactMatch()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "default" };
		options.TransportBindings["OrderCreated"] = "kafka";
		var store = CreateStore(inner, options);
		var message = new OutboundMessage { MessageType = "OrderCreated" };

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);

		message.TargetTransports.ShouldBe("kafka");
		A.CallTo(() => inner.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StageMessageAsync_ShouldResolveTransportByWildcard()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "default" };
		options.TransportBindings["Order*"] = "kafka";
		var store = CreateStore(inner, options);
		var message = new OutboundMessage { MessageType = "OrderCreated" };

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);

		message.TargetTransports.ShouldBe("kafka");
	}

	[Fact]
	public async Task StageMessageAsync_ShouldFallBackToDefaultTransport()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "rabbitmq" };
		options.TransportBindings["OrderCreated"] = "kafka";
		var store = CreateStore(inner, options);
		var message = new OutboundMessage { MessageType = "UnknownEvent" };

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);

		message.TargetTransports.ShouldBe("rabbitmq");
	}

	[Fact]
	public async Task StageMessageAsync_WithRequireExplicitBindings_ShouldThrowForUnknown()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions
		{
			DefaultTransport = "default",
			RequireExplicitBindings = true
		};
		options.TransportBindings["OrderCreated"] = "kafka";
		var store = CreateStore(inner, options);
		var message = new OutboundMessage { MessageType = "UnknownEvent" };

		await Should.ThrowAsync<InvalidOperationException>(
			() => store.StageMessageAsync(message, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task StageMessageAsync_WithNullMessage_ShouldThrow()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);

		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StageMessageAsync(null!, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task EnqueueAsync_ShouldDelegateToInnerStore()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		await store.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		A.CallTo(() => inner.EnqueueAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetUnsentMessagesAsync_ShouldDelegateToInnerStore()
	{
		var inner = A.Fake<IOutboxStore>();
		var expected = new List<OutboundMessage> { new() };
		A.CallTo(() => inner.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(expected));

		var store = CreateStore(inner);

		var result = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(expected);
	}

	[Fact]
	public async Task MarkSentAsync_ShouldDelegateToInnerStore()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);

		await store.MarkSentAsync("msg-1", CancellationToken.None).ConfigureAwait(true);

		A.CallTo(() => inner.MarkSentAsync("msg-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkFailedAsync_ShouldDelegateToInnerStore()
	{
		var inner = A.Fake<IOutboxStore>();
		var store = CreateStore(inner);

		await store.MarkFailedAsync("msg-1", "error", 2, CancellationToken.None).ConfigureAwait(true);

		A.CallTo(() => inner.MarkFailedAsync("msg-1", "error", 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StageMessageAsync_WildcardMatch_ShouldBeCaseInsensitive()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = new MultiTransportOutboxOptions { DefaultTransport = "default" };
		options.TransportBindings["order*"] = "kafka";
		var store = CreateStore(inner, options);
		var message = new OutboundMessage { MessageType = "OrderCreated" };

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);

		message.TargetTransports.ShouldBe("kafka");
	}
}

