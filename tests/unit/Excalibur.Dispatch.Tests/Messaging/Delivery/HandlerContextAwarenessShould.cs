// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class HandlerContextAwarenessShould
{
	[Theory]
	[InlineData("explicit")]
	[InlineData("inherited")]
	[InlineData("throwing-property")]
	public void ApplyTheInterfaceExactlyOnceBeforeAnyPropertyFallback(string kind)
	{
		AwareHandler handler = kind switch
		{
			"inherited" => new InheritedAwareHandler(),
			"throwing-property" => new ThrowingPropertyHandler(),
			_ => new AwareHandler()
		};
		using var provider = new ServiceCollection().AddSingleton(handler.GetType(), handler).BuildServiceProvider();
		var context = DispatchContextInitializer.CreateDefaultContext(provider);
		HandlerActivator.RequiresContextInjection(handler.GetType()).ShouldBeTrue();
		new HandlerActivator().ActivateHandler(handler.GetType(), context, provider).ShouldBeSameAs(handler);
		handler.SetCalls.ShouldBe(1);
		handler.ReceivedContext.ShouldBeSameAs(context);
	}

	[Fact]
	public void PreservePropertyOnlyContextInjection()
	{
		var handler = new PropertyOnlyHandler();
		using var provider = new ServiceCollection().AddSingleton(handler).BuildServiceProvider();
		var context = DispatchContextInitializer.CreateDefaultContext(provider);
		HandlerActivator.RequiresContextInjection(typeof(PropertyOnlyHandler)).ShouldBeTrue();
		new HandlerActivator().ActivateHandler(typeof(PropertyOnlyHandler), context, provider).ShouldBeSameAs(handler);
		handler.Context.ShouldBeSameAs(context);
	}

	private class AwareHandler : IMessageContextAware
	{
		private IMessageContext? _receivedContext;
		public int SetCalls { get; private set; }
		public IMessageContext? ReceivedContext => _receivedContext;
		void IMessageContextAware.SetContext(IMessageContext context)
		{
			SetCalls++;
			_receivedContext = context;
		}
	}
	private sealed class InheritedAwareHandler : AwareHandler;
	private sealed class ThrowingPropertyHandler : AwareHandler
	{
		public IMessageContext? Context
		{
			get => null;
			set => throw new InvalidOperationException("Interface-aware handlers must not evaluate the legacy property setter.");
		}
	}
	private sealed class PropertyOnlyHandler
	{
		public IMessageContext? Context { get; set; }
	}
}
