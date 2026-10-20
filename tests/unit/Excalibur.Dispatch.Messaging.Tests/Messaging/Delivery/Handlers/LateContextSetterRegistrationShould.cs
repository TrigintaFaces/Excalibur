// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class LateContextSetterRegistrationShould
{
	[Fact]
	public void ObserveFirstAndReplacementSetterThroughWarmedActivationPlans()
	{
		Verify<Normal>(0);
		Verify<Registered>(1);
		Verify<Factory>(2);
	}

	[Fact]
	public void PreserveInterfacePrecedenceOverReplacementRegisteredSetters()
	{
		using var provider = new ServiceCollection().AddSingleton<AwareHandler>().BuildServiceProvider();
		var activator = new HandlerActivator();
		var context = new MessageContext();
		HandlerActivatorRegistry.RegisterContextSetter<AwareHandler>((_, _) => throw new InvalidOperationException("Registry must not override interface"));
		var handler = (AwareHandler)activator.ActivateHandler(typeof(AwareHandler), context, provider);
		handler.Observed.ShouldBeSameAs(context);
	}

	internal sealed class AwareHandler : IMessageContextAware
	{
		public AwareHandler() { }
		internal IMessageContext? Observed;
		public void SetContext(IMessageContext context) => Observed = context;
	}

	private static void Verify<TMarker>(int mode)
	{
		using var provider = new ServiceCollection().AddTransient<Handler<TMarker>>().BuildServiceProvider();
		var activator = new HandlerActivator();
		var first = new MessageContext();
		var second = new MessageContext();
		Handler<TMarker> Activate(IMessageContext context) => (Handler<TMarker>)(mode switch
		{
			1 => activator.ActivateRegisteredHandler(typeof(Handler<TMarker>), context, provider),
			2 => activator.ActivateFactoryHandler(typeof(Handler<TMarker>), context, provider),
			_ => activator.ActivateHandler(typeof(Handler<TMarker>), context, provider),
		});

		Activate(first).Observed.ShouldBeNull();
		HandlerActivator.RequiresContextInjection(typeof(Handler<TMarker>)).ShouldBeFalse();
		// Ordinary parallel tests do not mutate the process-wide frozen-cache state.
		HandlerActivatorRegistry.RegisterContextSetter<Handler<TMarker>>((handler, context) => handler.Assign(context, 1));
		var afterFirst = Activate(first);
		afterFirst.Observed.ShouldBeSameAs(first);
		afterFirst.SetterVersion.ShouldBe(1);
		HandlerActivator.RequiresContextInjection(typeof(Handler<TMarker>)).ShouldBeTrue();
		HandlerActivatorRegistry.RegisterContextSetter<Handler<TMarker>>((handler, context) => handler.Assign(context, 2));
		var afterReplacement = Activate(second);
		afterReplacement.Observed.ShouldBeSameAs(second);
		afterReplacement.SetterVersion.ShouldBe(2);
	}

	internal sealed class Handler<TMarker>
	{
		public Handler() { }
		internal IMessageContext? Observed;
		internal int SetterVersion;
		internal void Assign(IMessageContext context, int version) { Observed = context; SetterVersion = version; }
	}
	private sealed class Normal;
	private sealed class Registered;
	private sealed class Factory;
}

[CollectionDefinition("LateContextSetterFrozenCache", DisableParallelization = true)]
public sealed class LateContextSetterFrozenCacheCollection;

[Collection("LateContextSetterFrozenCache")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class LateContextSetterFrozenCacheShould
{
	[Fact]
	public void ObserveRegistrationAfterActivationCacheIsFrozen()
	{
		HandlerActivator.ClearCache();
		try
		{
			using var provider = new ServiceCollection().AddSingleton<FrozenHandler>().BuildServiceProvider();
			var activator = new HandlerActivator();
			var context = new MessageContext();
			var handler = (FrozenHandler)activator.ActivateHandler(typeof(FrozenHandler), context, provider);
			handler.Observed.ShouldBeNull();
			HandlerActivator.FreezeCache();
			HandlerActivatorRegistry.RegisterContextSetter<FrozenHandler>((h, c) => h.Observed = c);
			activator.ActivateHandler(typeof(FrozenHandler), context, provider).ShouldBeSameAs(handler);
			handler.Observed.ShouldBeSameAs(context);
		}
		finally
		{
			HandlerActivator.ClearCache();
		}
	}
	internal sealed class FrozenHandler { public FrozenHandler() { } internal IMessageContext? Observed; }
}
