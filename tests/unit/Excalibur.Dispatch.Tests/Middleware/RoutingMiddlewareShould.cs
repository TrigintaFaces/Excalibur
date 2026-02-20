// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="RoutingMiddleware"/> verifying route resolution, failure handling,
/// and context enrichment with routing decisions.
/// Sprint 560 (S560.43).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class RoutingMiddlewareShould : UnitTestBase
{
	private readonly IDispatchRouter _router;
	private readonly RoutingMiddleware _middleware;
	private readonly IDispatchMessage _message;
	private readonly TestMessageContext _context;

	public RoutingMiddlewareShould()
	{
		_router = A.Fake<IDispatchRouter>();
		_middleware = new RoutingMiddleware(_router, NullLogger<RoutingMiddleware>.Instance);
		_message = A.Fake<IDispatchMessage>();
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
			RoutingDecision = null,
		};
	}

	[Fact]
	public async Task RouteMessageSuccessfullyAndContinuePipeline()
	{
		// Arrange
		var decision = RoutingDecision.Success("rabbitmq", ["billing-service"]);
		A.CallTo(() => _router.RouteAsync(_message, _context, A<CancellationToken>._))
			.Returns(new ValueTask<RoutingDecision>(decision));

		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
		_context.RoutingDecision.ShouldBe(decision);
		_context.Items.ContainsKey("routing:transport").ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFailureResultWhenRoutingFails()
	{
		// Arrange
		var decision = RoutingDecision.Failure("No matching routes");
		A.CallTo(() => _router.RouteAsync(_message, _context, A<CancellationToken>._))
			.Returns(new ValueTask<RoutingDecision>(decision));

		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		nextInvoked.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(ProblemDetailsTypes.Routing);
	}

	[Fact]
	public async Task StoreRoutingDecisionOnContext()
	{
		// Arrange
		var endpoints = new[] { "service-a", "service-b" };
		var decision = RoutingDecision.Success("kafka", endpoints);
		A.CallTo(() => _router.RouteAsync(_message, _context, A<CancellationToken>._))
			.Returns(new ValueTask<RoutingDecision>(decision));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_context.RoutingDecision.ShouldBe(decision);
		_context.Items.ContainsKey("routing:decision").ShouldBeFalse();
		_context.Items.ContainsKey("routing:transport").ShouldBeFalse();
		_context.Items.ContainsKey("routing:endpoints").ShouldBeFalse();
	}

	[Fact]
	public async Task RespectCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		A.CallTo(() => _router.RouteAsync(_message, _context, A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _middleware.InvokeAsync(_message, _context, Next, cts.Token)
				.ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public void SetStageToRouting()
	{
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.Routing);
	}

	[Fact]
	public async Task PassMultipleEndpointsThrough()
	{
		// Arrange
		var endpoints = new[] { "billing", "inventory", "shipping" };
		var decision = RoutingDecision.Success("rabbitmq", endpoints);
		A.CallTo(() => _router.RouteAsync(_message, _context, A<CancellationToken>._))
			.Returns(new ValueTask<RoutingDecision>(decision));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_context.RoutingDecision.ShouldNotBeNull();
		_context.RoutingDecision.Endpoints.Count.ShouldBe(3);
	}
}
