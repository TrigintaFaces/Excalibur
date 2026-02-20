// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="TransportRouterMiddleware"/> verifying transport selection,
/// message kind validation, and fallback behavior.
/// Sprint 560 (S560.43).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class TransportRouterMiddlewareShould : UnitTestBase
{
	private readonly TransportRouterMiddleware _middleware;
	private readonly TestMessageContext _context;

	public TransportRouterMiddlewareShould()
	{
		_middleware = new TransportRouterMiddleware(NullLogger<TransportRouterMiddleware>.Instance);
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
	}

	[Fact]
	public async Task PassThroughWhenNoTransportBindingExists()
	{
		// Arrange — no transport binding set in context
		var message = A.Fake<IDispatchMessage>();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectMessageKindNotAcceptedByBinding()
	{
		// Arrange
		var binding = A.Fake<ITransportBinding>();
		A.CallTo(() => binding.AcceptedMessageKinds).Returns(MessageKinds.Action);
		A.CallTo(() => binding.Name).Returns("test-binding");
		var adapter = A.Fake<ITransportAdapter>();
		A.CallTo(() => adapter.Name).Returns("test-adapter");
		A.CallTo(() => binding.TransportAdapter).Returns(adapter);
		_context.Properties["Excalibur.Dispatch.TransportBinding"] = binding;

		var eventMessage = A.Fake<IDispatchEvent>();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(eventMessage, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		nextInvoked.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(ProblemDetailsTypes.Routing);
	}

	[Fact]
	public async Task AcceptMessageKindMatchingBinding()
	{
		// Arrange
		var binding = A.Fake<ITransportBinding>();
		A.CallTo(() => binding.AcceptedMessageKinds).Returns(MessageKinds.Event);
		A.CallTo(() => binding.Name).Returns("event-binding");
		var adapter = A.Fake<ITransportAdapter>();
		A.CallTo(() => adapter.Name).Returns("event-adapter");
		A.CallTo(() => binding.TransportAdapter).Returns(adapter);
		_context.Properties["Excalibur.Dispatch.TransportBinding"] = binding;

		var eventMessage = A.Fake<IDispatchEvent>();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(eventMessage, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task StoreTransportAdapterNameInContext()
	{
		// Arrange
		var binding = A.Fake<ITransportBinding>();
		A.CallTo(() => binding.AcceptedMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => binding.Name).Returns("all-binding");
		var adapter = A.Fake<ITransportAdapter>();
		A.CallTo(() => adapter.Name).Returns("rabbitmq-adapter");
		A.CallTo(() => binding.TransportAdapter).Returns(adapter);
		_context.Properties["Excalibur.Dispatch.TransportBinding"] = binding;

		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_context.Properties["TransportAdapter"].ShouldBe("rabbitmq-adapter");
	}

	[Fact]
	public void SetStageToRouting()
	{
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.Routing);
	}

	[Fact]
	public void AcceptAllMessageKinds()
	{
		_middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}
}
