// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatcherShould
{
	private readonly IDispatchMiddlewareInvoker _invoker = A.Fake<IDispatchMiddlewareInvoker>();
	private readonly IMessageBusProvider _busProvider = A.Fake<IMessageBusProvider>();
	private readonly FinalDispatchHandler _finalHandler;
	private readonly ITransportContextProvider _transportContextProvider = A.Fake<ITransportContextProvider>();
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	public DispatcherShould()
	{
		_finalHandler = new FinalDispatchHandler(
			_busProvider,
			NullLogger<FinalDispatchHandler>.Instance,
			null,
			new Dictionary<string, IMessageBusOptions>());
	}

	[Fact]
	public async Task ThrowWhenNotConfiguredForDispatchAsync()
	{
		// Arrange
		var dispatcher = new Dispatcher();
		var message = new FakeDispatchMessage();
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(message, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenNotConfiguredForDispatchWithResponseAsync()
	{
		// Arrange
		var dispatcher = new Dispatcher();
		var message = new TestDispatchActionWithResponse();
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync<TestDispatchActionWithResponse, string>(message, context, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageThroughMiddlewarePipeline()
	{
		// Arrange
		var successResult = Excalibur.Dispatch.Abstractions.MessageResult.Success();
		A.CallTo(() => _invoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		var dispatcher = new Dispatcher(_invoker, _finalHandler, _transportContextProvider, _serviceProvider);
		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task SetCorrelationIdIfNotSet()
	{
		// Arrange
		var successResult = Excalibur.Dispatch.Abstractions.MessageResult.Success();
		A.CallTo(() => _invoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		var dispatcher = new Dispatcher(_invoker, _finalHandler, _transportContextProvider, _serviceProvider);
		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider) { CorrelationId = null };

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task SetCausationIdIfNotSet()
	{
		// Arrange
		var successResult = Excalibur.Dispatch.Abstractions.MessageResult.Success();
		A.CallTo(() => _invoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		var dispatcher = new Dispatcher(_invoker, _finalHandler, _transportContextProvider, _serviceProvider);
		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider) { CorrelationId = "corr-1", CausationId = null };

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		context.CausationId.ShouldBe("corr-1");
	}

	[Fact]
	public async Task SetMessageTypeOnContext()
	{
		// Arrange
		var successResult = Excalibur.Dispatch.Abstractions.MessageResult.Success();
		A.CallTo(() => _invoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		var dispatcher = new Dispatcher(_invoker, _finalHandler, _transportContextProvider, _serviceProvider);
		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		context.MessageType.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task ResolveTransportBindingBeforeMiddleware()
	{
		// Arrange
		var binding = A.Fake<ITransportBinding>();
		A.CallTo(() => _transportContextProvider.GetTransportBinding(A<IMessageContext>._)).Returns(binding);

		var successResult = Excalibur.Dispatch.Abstractions.MessageResult.Success();
		A.CallTo(() => _invoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		var dispatcher = new Dispatcher(_invoker, _finalHandler, _transportContextProvider, _serviceProvider);
		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		context.Items.ContainsKey("Excalibur.Dispatch.TransportBinding").ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowWhenDispatchStreamingWithNoServiceProvider()
	{
		// Arrange
		var dispatcher = new Dispatcher();
		var doc = A.Fake<IDispatchDocument>();
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchStreamingAsync<IDispatchDocument, string>(doc, context, CancellationToken.None))
			{
			}
		});
	}

	[Fact]
	public async Task ThrowWhenDispatchStreamWithNoServiceProvider()
	{
		// Arrange
		var dispatcher = new Dispatcher();
		var docs = ToAsyncEnumerable(Array.Empty<IDispatchDocument>());
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchStreamAsync(docs, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDispatchTransformStreamWithNoServiceProvider()
	{
		// Arrange
		var dispatcher = new Dispatcher();
		var input = ToAsyncEnumerable(Array.Empty<IDispatchDocument>());
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchTransformStreamAsync<IDispatchDocument, string>(input, context, CancellationToken.None))
			{
			}
		});
	}

	[Fact]
	public async Task ThrowWhenDispatchWithProgressWithNoServiceProvider()
	{
		// Arrange
		var dispatcher = new Dispatcher();
		var doc = A.Fake<IDispatchDocument>();
		var context = new MessageContext();
		var progress = A.Fake<IProgress<DocumentProgress>>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchWithProgressAsync(doc, context, progress, CancellationToken.None));
	}

	[Fact]
	public void ExposeServiceProvider()
	{
		// Arrange
		var dispatcher = new Dispatcher(serviceProvider: _serviceProvider);

		// Act & Assert
		dispatcher.ServiceProvider.ShouldBe(_serviceProvider);
	}

	private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
	{
		foreach (var item in source)
		{
			yield return item;
		}

		await Task.CompletedTask;
	}

	private sealed class TestDispatchActionWithResponse : IDispatchAction<string>;
}
