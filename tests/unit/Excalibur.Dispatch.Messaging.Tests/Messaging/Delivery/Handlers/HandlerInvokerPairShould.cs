// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Handlers")]
[Trait("Pattern", "Regression")]
public sealed class HandlerInvokerPairShould : IDisposable
{
	public HandlerInvokerPairShould() => HandlerInvokerRegistry.ClearCache();
	public void Dispose() => HandlerInvokerRegistry.ClearCache();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveBothMessageRegistrations(bool frozen)
	{
		HandlerInvokerRegistry.RegisterInvoker<Handler, First, string>(static (_, _, _) => Task.FromResult("first"));
		HandlerInvokerRegistry.RegisterInvoker<Handler, Second, string>(static (_, _, _) => Task.FromResult("second"));
		if (frozen)
		{
			HandlerInvokerRegistry.FreezeCache();
		}
		var invoker = new HandlerInvokerAot();
		(await invoker.InvokeAsync(new Handler(), new First(), CancellationToken.None)).ShouldBe("first");
		(await invoker.InvokeAsync(new Handler(), new Second(), CancellationToken.None)).ShouldBe("second");
	}

	[Fact]
	public async Task RejectUnregisteredAotHandlerWithoutReflecting()
	{
		var handler = new Handler();
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await new HandlerInvokerAot().InvokeAsync(handler, new First(), CancellationToken.None));
		handler.Calls.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepReflectionFallbackOutOfAotRegistrations(bool frozen)
	{
		HandlerInvokerRegistry.GetInvoker(typeof(Handler)).ShouldNotBeNull();
		if (frozen)
		{
			HandlerInvokerRegistry.FreezeCache();
		}
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await new HandlerInvokerAot().InvokeAsync(new Handler(), new First(), CancellationToken.None));
	}

	private sealed class First : IDispatchMessage;
	private sealed class Second : IDispatchMessage;
	private sealed class Handler
	{
		public int Calls { get; private set; }
		public Task HandleAsync(First message, CancellationToken cancellationToken)
		{
			Calls++;
			return Task.CompletedTask;
		}
	}
}
