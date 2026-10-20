// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Pattern", "DependencyInjection")]
public sealed class PipelineBuilderScopeAvailabilityShould
{
	[Fact]
	public void RefuseScopedStartupBeforeMiddlewareFactoryActivation()
	{
		var counters = new Counters();
		var builder = new PipelineBuilder("NoScope", Root(counters, scoped: true));
		builder.Use<ProbeMiddleware>();
		var error = Should.Throw<InvalidOperationException>(() => builder.Build());
		error.Message.ShouldContain(nameof(ProbeMiddleware));
		counters.RootActivations.ShouldBe(0);
	}

	[Fact]
	public void RefuseAnUninspectableFactoryBeforeStartupActivation()
	{
		var counters = new Counters();
		var builder = new PipelineBuilder("UnknownFactory", Root(counters, scoped: false));
		builder.Use(_ => { counters.RootActivations++; return new ProbeMiddleware(counters); });
		Should.Throw<InvalidOperationException>(() => builder.Build());
		counters.RootActivations.ShouldBe(0);
	}

	[Fact]
	public async Task RetainKnownSingletonMiddlewareWithoutScopeInfrastructure()
	{
		var counters = new Counters();
		var root = Root(counters, scoped: false);
		var builder = new PipelineBuilder("Singleton", root);
		builder.Use<ProbeMiddleware>();
		var pipeline = builder.Build();
		var message = new ProbeAction();
		(await pipeline.ExecuteAsync(message, new MessageContext(message, root), Next, CancellationToken.None)).Succeeded.ShouldBeTrue();
		counters.RootActivations.ShouldBe(1);
		counters.Invocations.ShouldBe(1);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task DeferRealActivationUntilAnAcceptedRequestScopeExists(bool requestScope, bool narrowFilter)
	{
		var counters = new Counters();
		var root = Root(counters, scoped: true);
		var child = Child(counters);
		var builder = new PipelineBuilder("Deferred", root);
		builder.ForMessageKinds(narrowFilter ? MessageKinds.Action | MessageKinds.Event : MessageKinds.All);
		builder.UseDeferred<ProbeMiddleware>(provider => provider.GetRequiredService<ProbeMiddleware>(),
			DispatchMiddlewareStage.Start, MessageKinds.Action);
		var pipeline = builder.Build();
		counters.RootActivations.ShouldBe(0);
		counters.ChildActivations.ShouldBe(0);
		var message = new ProbeAction();
		var execution = pipeline.ExecuteAsync(message, new MessageContext(message, requestScope ? child : root), Next, CancellationToken.None).AsTask();
		if (requestScope)
		{
			(await execution).Succeeded.ShouldBeTrue();
			counters.ChildActivations.ShouldBe(1);
			counters.Invocations.ShouldBe(1);
		}
		else
		{
			await Should.ThrowAsync<InvalidOperationException>(() => execution);
			counters.ChildActivations.ShouldBe(0);
			counters.Invocations.ShouldBe(0);
		}
		counters.RootActivations.ShouldBe(0);
		A.CallTo(() => ((IDisposable)child).Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public void BorrowAnAmbientProbeScopeRatherThanActivatingMiddlewareAtRoot()
	{
		var counters = new Counters();
		var child = Child(counters);
		var ambient = A.Fake<IDispatchAmbientScopeAccessor>();
		A.CallTo(() => ambient.CurrentServiceProvider).Returns(child);
		var builder = new PipelineBuilder("AmbientProbe", Root(counters, scoped: true, ambient));
		builder.Use<ProbeMiddleware>();
		builder.Build();
		counters.RootActivations.ShouldBe(0);
		counters.ChildActivations.ShouldBe(1);
		A.CallTo(() => ((IDisposable)child).Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public void SkipOptionalScopedMiddlewareWithoutActivatingItsFactory()
	{
		var counters = new Counters();
		var profile = A.Fake<IPipelineProfile>();
		A.CallTo(() => profile.Name).Returns("OptionalScope");
		A.CallTo(() => profile.MiddlewareEntries).Returns(new[] { new MiddlewareEntry(typeof(ProbeMiddleware), MiddlewareCriticality.Optional) });
		var builder = new PipelineBuilder("OptionalScope", Root(counters, scoped: true));
		builder.UseProfile(profile);
		Should.NotThrow(() => builder.Build());
		builder.ResolvedMiddleware.ShouldBeEmpty();
		counters.RootActivations.ShouldBe(0);
	}

	[Fact]
	public async Task SkipNonApplicableDeferredMessagesWithoutActivationOrScopeAcquisition()
	{
		var counters = new Counters();
		var root = Root(counters, scoped: true);
		var builder = new PipelineBuilder("DeferredKinds", root);
		builder.ForMessageKinds(MessageKinds.Event);
		builder.UseDeferred<ProbeMiddleware>(provider => provider.GetRequiredService<ProbeMiddleware>(),
			DispatchMiddlewareStage.PreProcessing, MessageKinds.Action);
		var pipeline = builder.Build();
		builder.ResolvedMiddleware.Single().Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
		var message = new ProbeEvent();
		(await pipeline.ExecuteAsync(message, new MessageContext(message, root), Next, CancellationToken.None)).Succeeded.ShouldBeTrue();
		counters.RootActivations.ShouldBe(0);
		counters.ChildActivations.ShouldBe(0);
		counters.Invocations.ShouldBe(0);
	}

	private static IServiceProvider Root(Counters counters, bool scoped, IDispatchAmbientScopeAccessor? ambient = null)
	{
		var services = new ServiceCollection();
		services.AddSingleton(counters);
		if (scoped) { services.AddScoped<ProbeMiddleware>(); }
		else { services.AddSingleton<ProbeMiddleware>(); }
		var root = A.Fake<IServiceProvider>();
		A.CallTo(() => root.GetService(A<Type>._)).Returns(null);
		A.CallTo(() => root.GetService(typeof(HandlerLifetimeRegistry))).Returns(new HandlerLifetimeRegistry(services));
		A.CallTo(() => root.GetService(typeof(IDispatchAmbientScopeAccessor))).Returns(ambient);
		A.CallTo(() => root.GetService(typeof(ProbeMiddleware))).ReturnsLazily(() =>
		{
			counters.RootActivations++; return new ProbeMiddleware(counters);
		});
		return root;
	}

	private static IServiceProvider Child(Counters counters)
	{
		var child = A.Fake<IServiceProvider>(o => o.Implements<IServiceScope>());
		A.CallTo(() => child.GetService(A<Type>._)).Returns(null);
		A.CallTo(() => ((IServiceScope)child).ServiceProvider).Returns(child);
		A.CallTo(() => child.GetService(typeof(ProbeMiddleware))).ReturnsLazily(() =>
		{
			counters.ChildActivations++; return new ProbeMiddleware(counters);
		});
		return child;
	}

	private static ValueTask<IMessageResult> Next(IDispatchMessage message, IMessageContext context, CancellationToken token)
		=> ValueTask.FromResult<IMessageResult>(MessageResult.Success());
	private sealed class ProbeAction : IDispatchAction;
	private sealed class ProbeEvent : IDispatchEvent;
	private sealed class Counters
	{
		public int RootActivations { get; set; }
		public int ChildActivations { get; set; }
		public int Invocations { get; set; }
	}
	private sealed class ProbeMiddleware(Counters counters) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;
		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			counters.Invocations++;
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
