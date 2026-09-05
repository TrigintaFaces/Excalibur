// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// What a NESTED context-free dispatch observes when the OUTER handler declares no context and no
/// middleware is registered -- the configuration that makes the ultra-local no-response fast path
/// eligible, and the one where propagation used to break.
/// </summary>
/// <remarks>
/// <para>
/// The fast path publishes an ambient context because the FRAMEWORK reads it on the handler's behalf:
/// <c>DispatcherContextExtensions.GetOrCreateChildContext</c> consults
/// <c>MessageContextHolder.Current</c> to decide child-vs-root, and the public two-argument
/// <c>DispatchAsync(message, ct)</c> is exactly what a handler calls to dispatch a nested message.
/// It previously did not, on the reasoning that a handler declaring no context cannot read one --
/// true of the handler, false of the framework, and every nested dispatch became a fresh root.
/// </para>
/// <para>
/// These arms measure, they do not assume. Each records the ambient context observed INSIDE the outer
/// handler (the direct evidence of whether a push happened) and the identifiers observed INSIDE the
/// inner handler, and reports both.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class NestedDispatchContextPropagationShould(ITestOutputHelper output)
{
	private const string OuterCorrelationId = "correlation-OUTER-distinctive";
	private const string OuterTenantId = "tenant-OUTER-distinctive";
	private const string OuterUserId = "user-OUTER-distinctive";

	private sealed record OuterAction : IDispatchAction;

	private sealed record OuterContextDeclaringAction : IDispatchAction;

	private sealed record InnerAction : IDispatchAction;

	/// <summary>
	/// Snapshot sink. The dispatcher rents and returns contexts, so every value is copied out while the
	/// handler is still running -- holding the context reference would read a recycled instance.
	/// </summary>
	private sealed class Probe
	{
		public bool OuterRan { get; set; }

		public bool AmbientPresentInOuterHandler { get; set; }

		public string? AmbientMessageIdInOuterHandler { get; set; }

		public bool InnerRan { get; set; }

		public bool InnerContextWasInjected { get; set; }

		public string? InnerMessageId { get; set; }

		public string? InnerCausationId { get; set; }

		public string? InnerCorrelationId { get; set; }

		public string? InnerTenantId { get; set; }

		public string? InnerUserId { get; set; }
	}

	/// <summary>Declares NO context, so the per-type dispatch info marks it fast-path eligible.</summary>
	private sealed class OuterHandler(IDispatcher dispatcher, Probe probe) : IActionHandler<OuterAction>
	{
		public async Task HandleAsync(OuterAction action, CancellationToken cancellationToken)
		{
			probe.OuterRan = true;

			// MessageContextHolder is internal and this assembly has InternalsVisibleTo, so the arm can
			// read the ambient directly. Reading it here does NOT declare context injection (only a
			// settable IMessageContext property or an IMessageContextAccessor constructor parameter
			// does), so the handler stays fast-path eligible.
			var ambient = MessageContextHolder.Current;
			probe.AmbientPresentInOuterHandler = ambient is not null;
			probe.AmbientMessageIdInOuterHandler = ambient?.MessageId;

			_ = await DispatcherContextExtensions
				.DispatchAsync(dispatcher, new InnerAction(), cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Identical to <see cref="OuterHandler"/> except it DECLARES a context, which routes it off the
	/// no-push fast path. This is the positive control: it proves the assertions can pass.
	/// </summary>
	private sealed class OuterContextDeclaringHandler(IDispatcher dispatcher, Probe probe)
		: IActionHandler<OuterContextDeclaringAction>
	{
		public IMessageContext? Context { get; set; }

		public async Task HandleAsync(OuterContextDeclaringAction action, CancellationToken cancellationToken)
		{
			probe.OuterRan = true;

			var ambient = MessageContextHolder.Current;
			probe.AmbientPresentInOuterHandler = ambient is not null;
			probe.AmbientMessageIdInOuterHandler = ambient?.MessageId;

			_ = await DispatcherContextExtensions
				.DispatchAsync(dispatcher, new InnerAction(), cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>Declares a context so it can report the one the nested dispatch handed it.</summary>
	private sealed class InnerHandler(Probe probe) : IActionHandler<InnerAction>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(InnerAction action, CancellationToken cancellationToken)
		{
			probe.InnerRan = true;

			var context = Context;
			probe.InnerContextWasInjected = context is not null;
			if (context is not null)
			{
				probe.InnerMessageId = context.MessageId;
				probe.InnerCausationId = context.CausationId;
				probe.InnerCorrelationId = context.CorrelationId;
				probe.InnerTenantId = context.GetTenantId();
				probe.InnerUserId = context.GetUserId();
			}

			return Task.CompletedTask;
		}
	}

	/// <summary>Changes nothing. Registering it must only change WHICH PATH the dispatch takes.</summary>
	private sealed class PassThroughMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(nextDelegate);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private static ServiceProvider BuildProvider(Probe probe, bool withMiddleware)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddSingleton(probe);
		_ = services.AddTransient<IActionHandler<OuterAction>, OuterHandler>();
		_ = services.AddTransient<IActionHandler<OuterContextDeclaringAction>, OuterContextDeclaringHandler>();
		_ = services.AddTransient<IActionHandler<InnerAction>, InnerHandler>();

		if (withMiddleware)
		{
			_ = services.AddSingleton<IDispatchMiddleware, PassThroughMiddleware>();
		}

		return services.BuildServiceProvider();
	}

	private static MessageContext CreateOuterContext(IServiceProvider provider)
	{
		var context = new MessageContext
		{
			MessageId = "outer-message-id-distinctive",
			CorrelationId = OuterCorrelationId,
		};

		var identity = context.GetOrCreateIdentityFeature();
		identity.TenantId = OuterTenantId;
		identity.UserId = OuterUserId;
		context.Initialize(provider);
		return context;
	}

	private void Report(string arm, Probe probe, bool canBypassOuter, bool canBypassInner, MessageContext outer)
	{
		output.WriteLine($"--- {arm} ---");
		output.WriteLine($"CanBypassMiddlewareFor(outer)       = {canBypassOuter}");
		output.WriteLine($"CanBypassMiddlewareFor(InnerAction) = {canBypassInner}");
		output.WriteLine($"outer.MessageId                     = {outer.MessageId}");
		output.WriteLine($"outer.CorrelationId                 = {outer.CorrelationId}");
		output.WriteLine($"outer.TenantId                      = {outer.GetTenantId()}");
		output.WriteLine($"outer handler ran                   = {probe.OuterRan}");
		output.WriteLine($"AMBIENT present in outer handler    = {probe.AmbientPresentInOuterHandler}");
		output.WriteLine($"ambient.MessageId in outer handler  = {probe.AmbientMessageIdInOuterHandler ?? "<null>"}");
		output.WriteLine($"inner handler ran                   = {probe.InnerRan}");
		output.WriteLine($"inner context injected              = {probe.InnerContextWasInjected}");
		output.WriteLine($"inner.MessageId                     = {probe.InnerMessageId ?? "<null>"}");
		output.WriteLine($"inner.CausationId                   = {probe.InnerCausationId ?? "<null>"}");
		output.WriteLine($"inner.CorrelationId                 = {probe.InnerCorrelationId ?? "<null>"}");
		output.WriteLine($"inner.TenantId                      = {probe.InnerTenantId ?? "<null>"}");
		output.WriteLine($"inner.UserId                        = {probe.InnerUserId ?? "<null>"}");
	}

	private static void AssertReached(Probe probe)
	{
		probe.OuterRan.ShouldBeTrue("the outer handler must have run for this arm to measure anything");
		probe.InnerRan.ShouldBeTrue("the nested dispatch must have reached the inner handler");
		probe.InnerContextWasInjected.ShouldBeTrue("the inner handler declares a context, so one must be supplied");
	}

	private static void AssertChildSemantics(Probe probe, MessageContext outerContext)
	{
		AssertReached(probe);

		probe.AmbientPresentInOuterHandler.ShouldBeTrue(
			"propagation is only possible when an ambient context was published around the outer handler");

		probe.InnerCausationId.ShouldBe(outerContext.MessageId,
			"the nested dispatch must be a CHILD -- its causation is the outer message's id");
		probe.InnerCorrelationId.ShouldBe(OuterCorrelationId,
			"correlation must propagate across the nested dispatch boundary");
		probe.InnerTenantId.ShouldBe(OuterTenantId,
			"TENANT must propagate across the nested dispatch boundary");
		probe.InnerUserId.ShouldBe(OuterUserId,
			"identity must propagate across the nested dispatch boundary");
	}

	/// <summary>
	/// Outer handler declares no context and nothing disables the ultra-local fast path -- the hardest
	/// configuration for propagation, and the one that used to break it. The nested dispatch must still
	/// be a CHILD: causation linked, correlation, tenant and user carried across.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm was a characterization of a defect: it asserted four nulls, because the fast path pushed
	/// no ambient context and every nested dispatch therefore started a fresh root. The fast path now
	/// pushes, so the arm asserts the same child semantics as the other two. Its value is unchanged --
	/// it is the only arm that exercises the no-middleware fast path, so it is the one that goes red if
	/// the push is ever removed again for the 18.64 ns it costs.
	/// </para>
	/// <para>
	/// TENANT IS THE SERIOUS ROW. A nested dispatch losing the tenant of the message that caused it is a
	/// tenancy-isolation failure, not a diagnostics inconvenience.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Propagate_the_outer_context_on_a_nested_dispatch_with_no_middleware_registered()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe, withMiddleware: false);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var direct = (Dispatcher)dispatcher;
		var outerContext = CreateOuterContext(provider);

		_ = await dispatcher.DispatchAsync(new OuterAction(), outerContext, CancellationToken.None);

		Report(
			"NO MIDDLEWARE (fast path eligible)",
			probe,
			direct.CanBypassMiddlewareFor(typeof(OuterAction)),
			direct.CanBypassMiddlewareFor(typeof(InnerAction)),
			outerContext);

		// The mechanism: the fast path publishes an ambient context around the outer handler, which is
		// what the nested context-free dispatch children from. Without it the four rows below are null.
		probe.AmbientPresentInOuterHandler.ShouldBeTrue(
			"the ultra-local fast path must publish an ambient context -- this is the mechanism");

		AssertChildSemantics(probe, outerContext);
	}

	/// <summary>
	/// Same shape, one difference: a no-op middleware applies, which is claimed to disable the fast path
	/// and therefore hide the defect.
	/// </summary>
	[Fact]
	public async Task Propagate_the_outer_context_to_a_nested_dispatch_with_middleware_registered()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe, withMiddleware: true);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var direct = (Dispatcher)dispatcher;
		var outerContext = CreateOuterContext(provider);

		_ = await dispatcher.DispatchAsync(new OuterAction(), outerContext, CancellationToken.None);

		Report(
			"WITH PASS-THROUGH MIDDLEWARE",
			probe,
			direct.CanBypassMiddlewareFor(typeof(OuterAction)),
			direct.CanBypassMiddlewareFor(typeof(InnerAction)),
			outerContext);

		AssertChildSemantics(probe, outerContext);
	}

	/// <summary>
	/// NON-VACUITY CONTROL. Identical to the first arm except the outer handler declares a context,
	/// which routes it off the no-push fast path. If this arm cannot go green, the two arms above prove
	/// nothing -- they would be failing on a broken probe rather than on the behaviour under test.
	/// </summary>
	[Fact]
	public async Task Control_propagate_when_the_outer_handler_declares_a_context()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe, withMiddleware: false);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var direct = (Dispatcher)dispatcher;
		var outerContext = CreateOuterContext(provider);

		// The isolating fact: middleware bypass is STILL enabled here, exactly as in the defective arm.
		// The only difference is that the outer handler declares a context, which routes it off the
		// no-push fast path -- so the trigger is the fast path, not the absence of middleware.
		direct.CanBypassMiddlewareFor(typeof(OuterContextDeclaringAction)).ShouldBeTrue();

		_ = await dispatcher.DispatchAsync(new OuterContextDeclaringAction(), outerContext, CancellationToken.None);

		Report(
			"CONTROL: outer handler DECLARES a context",
			probe,
			direct.CanBypassMiddlewareFor(typeof(OuterContextDeclaringAction)),
			direct.CanBypassMiddlewareFor(typeof(InnerAction)),
			outerContext);

		AssertChildSemantics(probe, outerContext);
	}
}
