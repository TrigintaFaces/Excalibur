// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Regression lock for the causation auto-child seam (ADR-343): a context-free
/// <c>DispatchAsync(message, ct)</c> issued from inside a handler is a <b>child</b> — fresh
/// <c>MessageId</c>, <c>CausationId = parent.MessageId</c>, correlation and identity propagated.
/// </summary>
/// <remarks>
/// <para>
/// <b>This lock drives a REAL dispatcher through a REAL handler performing a REAL nested dispatch, and
/// never assigns <c>MessageContextHolder.Current</c> itself.</b> That is the whole point. The previous
/// version of this file got it wrong in both directions: its dispatcher was a fake, so the production
/// child-vs-root branch never ran, and it hand-set the ambient holder — manufacturing the exact value
/// the real path was failing to publish. It stayed green over a live defect for its whole life.
/// </para>
/// <para>
/// The outer handler here declares NO context. That is deliberate and load-bearing: declaring one
/// routes the dispatch off the ultra-local fast path, and that path is precisely the one that was not
/// publishing an ambient context. A handler that declares a context cannot detect this defect.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DispatcherAutoChildCausationShould
{
	private const string ParentCorrelationId = "correlation-PARENT-distinctive";
	private const string ParentTenantId = "tenant-PARENT-distinctive";
	private const string ParentUserId = "user-PARENT-distinctive";
	private const string ParentMessageId = "parent-message-id-distinctive";

	private sealed record OuterAction : IDispatchAction;

	private sealed record InnerAction : IDispatchAction;

	private sealed class Probe
	{
		public bool OuterRan { get; set; }

		public bool InnerRan { get; set; }

		public string? InnerMessageId { get; set; }

		public string? InnerCausationId { get; set; }

		public string? InnerCorrelationId { get; set; }

		public string? InnerTenantId { get; set; }

		public string? InnerUserId { get; set; }
	}

	/// <summary>
	/// Declares NO context, so the per-type dispatch info marks it ultra-local fast-path eligible, and
	/// dispatches a nested message the way a consumer would — through the two-argument overload.
	/// </summary>
	private sealed class OuterHandler(IDispatcher dispatcher, Probe probe) : IActionHandler<OuterAction>
	{
		public async Task HandleAsync(OuterAction action, CancellationToken cancellationToken)
		{
			probe.OuterRan = true;
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

	private static ServiceProvider BuildProvider(Probe probe)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddSingleton(probe);
		_ = services.AddTransient<IActionHandler<OuterAction>, OuterHandler>();
		_ = services.AddTransient<IActionHandler<InnerAction>, InnerHandler>();
		return services.BuildServiceProvider();
	}

	private static MessageContext CreateParentContext(IServiceProvider provider)
	{
		var context = new MessageContext
		{
			MessageId = ParentMessageId,
			CorrelationId = ParentCorrelationId,
		};

		var identity = context.GetOrCreateIdentityFeature();
		identity.TenantId = ParentTenantId;
		identity.UserId = ParentUserId;
		context.Initialize(provider);
		return context;
	}

	[Fact]
	public async Task Auto_child_a_nested_context_free_dispatch_from_inside_a_handler()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var parent = CreateParentContext(provider);

		// The ambient context is published by the DISPATCHER, not by this test. If the fast path stops
		// pushing it, every assertion below goes red -- which is the property this lock exists to hold.
		_ = await dispatcher.DispatchAsync(new OuterAction(), parent, CancellationToken.None);

		probe.OuterRan.ShouldBeTrue("the outer handler must have run for this arm to measure anything");
		probe.InnerRan.ShouldBeTrue("the nested dispatch must have reached the inner handler");

		probe.InnerMessageId.ShouldNotBeNullOrEmpty("the child must get its own MessageId");
		probe.InnerMessageId.ShouldNotBe(parent.MessageId,
			"a nested context-free dispatch must get a FRESH MessageId (auto-child), not reuse the parent's");
		probe.InnerCausationId.ShouldBe(parent.MessageId,
			"the child's CausationId must be the parent's MessageId -- this is the causation link");
		probe.InnerCorrelationId.ShouldBe(ParentCorrelationId,
			"correlation must propagate across the nested dispatch boundary");
		probe.InnerTenantId.ShouldBe(ParentTenantId,
			"TENANT must propagate across the nested dispatch boundary");
		probe.InnerUserId.ShouldBe(ParentUserId,
			"identity must propagate across the nested dispatch boundary");
	}

	[Fact]
	public async Task Start_a_fresh_root_when_dispatched_with_no_ambient_context()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Top-level: nothing published an ambient context, so this dispatch is a root.
		_ = await DispatcherContextExtensions
			.DispatchAsync(dispatcher, new InnerAction(), CancellationToken.None);

		probe.InnerRan.ShouldBeTrue("the dispatch must have reached the handler");
		probe.InnerMessageId.ShouldNotBeNullOrEmpty("a fresh root still gets its own message id");
		probe.InnerCausationId.ShouldNotBe(ParentMessageId,
			"a root dispatch has no parent -- its causation is not tied to another message's id");
	}

	[Fact]
	public async Task Reuse_the_supplied_context_when_dispatched_with_an_explicit_context()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var explicitContext = CreateParentContext(provider);

		// The explicit-context overload is deliberate same-context reuse -- the escape hatch, no child.
		_ = await dispatcher.DispatchAsync(new InnerAction(), explicitContext, CancellationToken.None);

		probe.InnerRan.ShouldBeTrue("the dispatch must have reached the handler");
		probe.InnerMessageId.ShouldBe(explicitContext.MessageId,
			"the explicit-context overload reuses the supplied context verbatim -- it does NOT auto-child");
	}

	[Fact]
	public async Task Not_leak_the_dispatch_context_into_the_caller_ambient_holder()
	{
		var probe = new Probe();
		await using var provider = BuildProvider(probe);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var parent = CreateParentContext(provider);

		MessageContextHolder.Current.ShouldBeNull(
			"non-vacuity: the holder must start empty, or the assertion below could pass without a pop");

		_ = await dispatcher.DispatchAsync(new OuterAction(), parent, CancellationToken.None);

		// The dispatcher pushes an ambient context around the handler; it must pop it again. Otherwise
		// the caller's flow is left holding a context that was returned to the factory and reset.
		MessageContextHolder.Current.ShouldBeNull(
			"the ambient context must be scoped to the dispatch -- it must not escape to the caller");
	}
}
