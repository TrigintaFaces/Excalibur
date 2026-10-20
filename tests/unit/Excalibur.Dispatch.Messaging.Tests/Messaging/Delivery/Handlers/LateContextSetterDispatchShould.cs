// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class LateContextSetterDispatchShould
{
	[Fact]
	public async Task PreserveCallerContextAfterCachedCommandQueryAndEventPlansAreWarmed()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var commandHandler = new CommandHandler();
		var queryHandler = new QueryHandler();
		var eventHandlerA = new EventHandlerA();
		var eventHandlerB = new EventHandlerB();
		services.AddSingleton(commandHandler);
		services.AddSingleton(queryHandler);
		services.AddSingleton(eventHandlerA);
		services.AddSingleton(eventHandlerB);
		services.AddSingleton<IActionHandler<SetterCommand>>(commandHandler);
		services.AddSingleton<IActionHandler<SetterQuery, int>>(queryHandler);
		services.AddSingleton<IEventHandler<SetterEvent>>(eventHandlerA);
		services.AddSingleton<IEventHandler<SetterEvent>>(eventHandlerB);
		services.AddDispatchPipeline();
		services.AddDispatchHandlers();
		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var command = new SetterCommand();
		var query = new SetterQuery();
		var evt = new SetterEvent();
		async Task Invoke(IMessageContext context)
		{
			(await dispatcher.DispatchAsync(command, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
			(await dispatcher.DispatchAsync<SetterQuery, int>(query, context, CancellationToken.None)).ReturnValue.ShouldBe(42);
			(await dispatcher.DispatchAsync((IDispatchMessage)query, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
			(await dispatcher.DispatchAsync(evt, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
		}
		await Invoke(new MessageContext(command, provider));
		provider.GetRequiredService<CommandHandler>().Observed.ShouldBeNull();
		for (var version = 1; version <= 2; version++)
		{
			var currentVersion = version;
			HandlerActivatorRegistry.RegisterContextSetter<CommandHandler>((h, c) => h.Set(c, currentVersion));
			HandlerActivatorRegistry.RegisterContextSetter<QueryHandler>((h, c) => h.Set(c, currentVersion));
			HandlerActivatorRegistry.RegisterContextSetter<EventHandlerA>((h, c) => h.Set(c, currentVersion));
			HandlerActivatorRegistry.RegisterContextSetter<EventHandlerB>((h, c) => h.Set(c, currentVersion));
			var context = new MessageContext(command, provider);
			await Invoke(context);
			foreach (var handler in new ObservedHandler[] { provider.GetRequiredService<CommandHandler>(), provider.GetRequiredService<QueryHandler>(), provider.GetRequiredService<EventHandlerA>(), provider.GetRequiredService<EventHandlerB>() })
			{
				handler.Observed.ShouldBeSameAs(context);
				handler.Version.ShouldBe(version);
			}
		}
	}

	[Fact]
	public async Task RecheckRetainedExportedDelegateWhenFirstAndReplacementSetterArrive()
	{
		var services = new ServiceCollection();
		services.AddSingleton<RetainedHandler>();
		services.AddSingleton(new HandlerLifetimeRegistry(services));
		using var provider = services.BuildServiceProvider();
		var registry = new HandlerRegistry();
		registry.Register(typeof(RetainedCommand), typeof(RetainedHandler), expectsResponse: false);
		var bus = new LocalMessageBus(provider, registry, new HandlerActivator(), new HandlerInvoker(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalMessageBus>.Instance);
		bus.TryGetUltraLocalFastInvokers(typeof(RetainedCommand), out _, out var requiresContext, out var retained, out _).ShouldBeTrue();
		requiresContext.ShouldBeFalse();
		retained.ShouldNotBeNull();
		await retained!(new RetainedCommand(), CancellationToken.None);
		var handler = provider.GetRequiredService<RetainedHandler>();
		handler.Version.ShouldBe(0);
		HandlerActivatorRegistry.RegisterContextSetter<RetainedHandler>((h, c) => h.Set(c, 1));
		await retained(new RetainedCommand(), CancellationToken.None);
		handler.Version.ShouldBe(1);
		handler.Observed.ShouldNotBeNull();
		HandlerActivatorRegistry.RegisterContextSetter<RetainedHandler>((h, c) => h.Set(c, 2));
		await retained(new RetainedCommand(), CancellationToken.None);
		handler.Version.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnFactoryOwnedContextExactlyOnceAfterRetainedQuerySuccessFaultAndCancellation()
	{
		await VerifyFactoryOwnership<SuccessOwnership>(0);
		await VerifyFactoryOwnership<FaultOwnership>(1);
		await VerifyFactoryOwnership<CancelOwnership>(2);
	}

	private static async Task VerifyFactoryOwnership<TMarker>(int outcome)
	{
		var factory = new TrackingContextFactory();
		var handler = new OwnershipHandler<TMarker>();
		var services = new ServiceCollection();
		services.AddSingleton(handler);
		services.AddSingleton<IMessageContextFactory>(factory);
		services.AddSingleton(new HandlerLifetimeRegistry(services));
		using var provider = services.BuildServiceProvider();
		var registry = new HandlerRegistry();
		registry.Register(typeof(OwnershipQuery<TMarker>), typeof(OwnershipHandler<TMarker>), expectsResponse: true, responseType: typeof(int));
		var bus = new LocalMessageBus(provider, registry, new HandlerActivator(), new HandlerInvoker(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalMessageBus>.Instance);
		bus.TryGetUltraLocalFastInvokers(typeof(OwnershipQuery<TMarker>), out _, out var requiresContext, out _, out var retained).ShouldBeTrue();
		requiresContext.ShouldBeFalse();
		retained.ShouldNotBeNull();
		(await retained!(new OwnershipQuery<TMarker>(), CancellationToken.None)).ShouldBe(84);
		factory.Created.Count.ShouldBe(0);
		HandlerActivatorRegistry.RegisterContextSetter<OwnershipHandler<TMarker>>((h, c) => h.Observed = c);
		handler.Outcome = outcome;
		using var cancellation = new CancellationTokenSource();
		if (outcome == 2)
		{
			cancellation.Cancel();
		}
		if (outcome == 1)
		{
			var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
				{
					_ = await retained(new OwnershipQuery<TMarker>(), cancellation.Token);
				});
			failure.Message.ShouldBe("ownership-fault");
		}
		else if (outcome == 2)
		{
			var failure = await Should.ThrowAsync<OperationCanceledException>(async () =>
				{
					_ = await retained(new OwnershipQuery<TMarker>(), cancellation.Token);
				});
			failure.CancellationToken.ShouldBe(cancellation.Token);
		}
		else
		{
			(await retained(new OwnershipQuery<TMarker>(), cancellation.Token)).ShouldBe(84);
		}
		factory.Created.Count.ShouldBe(1);
		factory.Returned.Count.ShouldBe(1);
		factory.Returned[0].ShouldBeSameAs(factory.Created[0]);
		handler.Observed.ShouldBeSameAs(factory.Created[0]);
		handler.ReturnedBeforeCompletion.ShouldBeFalse();
		handler.SeenToken.ShouldBe(cancellation.Token);
	}

	[Fact]
	public async Task KeepCallerScopeAndAmbientAccessorWhenSetterArrivesAfterScopedQueryWarmup()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IMessageContextAccessor>(new MessageContextAccessor());
		services.AddScoped<ScopeIdentity>();
		services.AddScoped<ScopedSetterHandler>();
		services.AddScoped<IActionHandler<ScopedSetterQuery, int>, ScopedSetterHandler>();
		services.AddDispatchPipeline();
		services.AddDispatchHandlers();
		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		using var scope = provider.CreateScope();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var query = new ScopedSetterQuery();
		var context = new MessageContext(query, scope.ServiceProvider);
		var warm = await dispatcher.DispatchAsync<ScopedSetterQuery, int>(query, context, CancellationToken.None);
		warm.Succeeded.ShouldBeTrue();
		HandlerActivatorRegistry.RegisterContextSetter<ScopedSetterHandler>((h, c) => h.Observed = c);
		var result = await dispatcher.DispatchAsync<ScopedSetterQuery, int>(query, context, CancellationToken.None);
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(84);
		context.RequestServices.ShouldBeSameAs(scope.ServiceProvider);
	}

	internal sealed record ScopedSetterQuery : IDispatchAction<int>;
	internal sealed class ScopeIdentity { public ScopeIdentity() { } }
	internal sealed class ScopedSetterHandler : IActionHandler<ScopedSetterQuery, int>
	{
		private readonly ScopeIdentity _identity;
		private readonly IMessageContextAccessor _accessor;
		public ScopedSetterHandler(ScopeIdentity identity, IMessageContextAccessor accessor)
		{
			_identity = identity;
			_accessor = accessor;
		}
		internal IMessageContext? Observed;
		public Task<int> HandleAsync(ScopedSetterQuery action, CancellationToken cancellationToken) =>
			Task.FromResult(Observed is not null && ReferenceEquals(Observed, _accessor.MessageContext) &&
				ReferenceEquals(_identity, Observed.RequestServices?.GetRequiredService<ScopeIdentity>()) ? 84 : -1);
	}

	internal sealed record OwnershipQuery<TMarker> : IDispatchAction<int>;
	internal sealed class OwnershipHandler<TMarker> : IActionHandler<OwnershipQuery<TMarker>, int>
	{
		public OwnershipHandler() { }
		internal IMessageContext? Observed;
		internal int Outcome;
		internal bool ReturnedBeforeCompletion;
		internal CancellationToken SeenToken;
		public async Task<int> HandleAsync(OwnershipQuery<TMarker> action, CancellationToken cancellationToken)
		{
			await Task.Yield();
			SeenToken = cancellationToken;
			if (Observed is not null)
			{
				ReturnedBeforeCompletion = Observed.GetItem<bool>("ownership-returned");
			}
			if (Outcome == 1)
			{
				throw new InvalidOperationException("ownership-fault");
			}
			cancellationToken.ThrowIfCancellationRequested();
			return 84;
		}
	}
	internal sealed class TrackingContextFactory : IMessageContextFactory
	{
		internal readonly List<IMessageContext> Created = [];
		internal readonly List<IMessageContext> Returned = [];
		public IMessageContext CreateContext()
		{
			var context = new MessageContext();
			Created.Add(context);
			return context;
		}
		public IMessageContext CreateContext(IDictionary<string, object> properties) => CreateContext();
		public IMessageContext CreateChildContext(IMessageContext parent) => CreateContext();
		public void Return(IMessageContext context)
		{
			context.SetItem("ownership-returned", true);
			Returned.Add(context);
		}
	}
	private sealed class SuccessOwnership;
	private sealed class FaultOwnership;
	private sealed class CancelOwnership;

	internal sealed record RetainedCommand : IDispatchAction;
	internal sealed class RetainedHandler : ObservedHandler, IActionHandler<RetainedCommand>
	{
		public RetainedHandler() { }
		public Task HandleAsync(RetainedCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	internal abstract class ObservedHandler
	{
		internal IMessageContext? Observed;
		internal int Version;
		internal void Set(IMessageContext context, int version) { Observed = context; Version = version; }
	}
	internal sealed record SetterCommand : IDispatchAction;
	internal sealed record SetterQuery : IDispatchAction<int>;
	internal sealed record SetterEvent : IDispatchEvent;
	internal sealed class CommandHandler : ObservedHandler, IActionHandler<SetterCommand>
	{
		public CommandHandler() { }
		public Task HandleAsync(SetterCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}
	internal sealed class QueryHandler : ObservedHandler, IActionHandler<SetterQuery, int>
	{
		public QueryHandler() { }
		public Task<int> HandleAsync(SetterQuery action, CancellationToken cancellationToken) => Task.FromResult(42);
	}
	internal sealed class EventHandlerA : ObservedHandler, IEventHandler<SetterEvent>
	{
		public EventHandlerA() { }
		public Task HandleAsync(SetterEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
	}
	internal sealed class EventHandlerB : ObservedHandler, IEventHandler<SetterEvent>
	{
		public EventHandlerB() { }
		public Task HandleAsync(SetterEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
