// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Pattern", "Regression")]
public sealed class DirectRoutingInvokerShould
{
	[Fact]
	public async Task HonorRoutingRejectionWithoutDispatcherPrerouting()
	{
		var router = A.Fake<IDispatchRouter>();
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(RoutingDecision.Failure("No permitted route"));
		var invoker = new DispatchMiddlewareInvoker([new RoutingMiddleware(router, NullLogger<RoutingMiddleware>.Instance)]);
		var calls = 0;
		var result = await invoker.InvokeAsync<IMessageResult>(new Command(), new MessageContext(), (_, _, _) =>
		{
			calls++;
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		}, CancellationToken.None);
		result.Succeeded.ShouldBeFalse();
		calls.ShouldBe(0);
	}

	private sealed class Command : IDispatchAction;
}
