// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Regression lock for the causation auto-child seam (yensbc / ADR-343).
/// </summary>
/// <remarks>
/// <para>
/// The seam ruling (mirrors <c>Activity</c>/OTel <c>StartActivity</c>): a context-free
/// <c>DispatchAsync(message, ct)</c> issued under an ambient context is a <b>child by default</b> —
/// fresh <c>MessageId</c>, <c>CausationId = parent.MessageId</c>, correlation/identity propagated.
/// With no ambient context it is a fresh root. The explicit-context overload
/// <c>DispatchAsync(message, context, ct)</c> is deliberate same-context reuse (the escape hatch).
/// <c>DispatchChildAsync</c> is retired — auto-child is the ergonomic default, making
/// "forgot to set CausationId on a nested dispatch" inexpressible.
/// </para>
/// <para>
/// This lock is RED on the pre-fix behavior, where the context-free overload <b>reuses</b> the ambient
/// context as-is (same <c>MessageId</c>, no causation link established).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DispatcherAutoChildCausationShould : IDisposable
{
	private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();

	public DispatcherAutoChildCausationShould()
	{
		MessageContextHolder.Current = null;
	}

	public void Dispose()
	{
		MessageContextHolder.Current = null;
	}

	private IMessageContext? CaptureDispatchedContext(IDispatchMessage message)
	{
		IMessageContext? captured = null;
		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => captured = ctx)
			.Returns(MessageResult.Success());
		return captured;
	}

	[Fact]
	public async Task Auto_child_a_context_free_dispatch_under_an_ambient_context()
	{
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parent = new MessageContext
		{
			MessageId = "parent-message-id",
			CorrelationId = "correlation-abc",
		};
		parent.GetOrCreateIdentityFeature().TenantId = "tenant-xyz";
		parent.Initialize(serviceProvider);

		IMessageContext? captured = null;
		_ = A.CallTo(() => _dispatcher.DispatchAsync(message, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => captured = ctx)
			.Returns(MessageResult.Success());

		MessageContextHolder.Current = parent;

		// Call the context-free extension explicitly (avoids interface method shadowing).
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		_ = captured.ShouldNotBeNull();
		captured.MessageId.ShouldNotBe(parent.MessageId,
			"a context-free dispatch under an ambient context must get a FRESH MessageId (auto-child), not reuse the parent's");
		captured.CausationId.ShouldBe(parent.MessageId,
			"the child's CausationId must be the parent's MessageId (the causation link)");
		captured.CorrelationId.ShouldBe(parent.CorrelationId,
			"correlation must propagate across the auto-child boundary");
		captured.GetTenantId().ShouldBe(parent.GetTenantId(),
			"identity must propagate across the auto-child boundary");
	}

	[Fact]
	public async Task Start_a_fresh_root_when_no_ambient_context_is_present()
	{
		var message = A.Fake<IDispatchMessage>();
		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);

		IMessageContext? captured = null;
		_ = A.CallTo(() => _dispatcher.DispatchAsync(message, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => captured = ctx)
			.Returns(MessageResult.Success());

		MessageContextHolder.Current = null;

		// Fresh root: must not throw (contrast with the retired DispatchChildAsync, which threw here).
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		_ = captured.ShouldNotBeNull();
		captured.CausationId.ShouldNotBe("parent-message-id",
			"a root dispatch has no parent — its causation is not tied to another message's id");
	}

	[Fact]
	public async Task Reuse_the_supplied_context_when_dispatched_with_an_explicit_context()
	{
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var explicitContext = new MessageContext
		{
			MessageId = "explicit-message-id",
			CorrelationId = "correlation-explicit",
		};
		explicitContext.Initialize(serviceProvider);

		IMessageContext? captured = null;
		_ = A.CallTo(() => _dispatcher.DispatchAsync(message, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => captured = ctx)
			.Returns(MessageResult.Success());

		// Explicit-context overload = deliberate same-context reuse (the escape hatch — no new child).
		_ = await _dispatcher.DispatchAsync(message, explicitContext, CancellationToken.None);

		_ = captured.ShouldNotBeNull();
		captured.MessageId.ShouldBe(explicitContext.MessageId,
			"the explicit-context overload reuses the supplied context verbatim — it does NOT auto-child");
	}

	[Fact]
	public async Task Not_leak_the_auto_child_context_into_the_ambient_holder()
	{
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parent = new MessageContext
		{
			MessageId = "parent-message-id",
			CorrelationId = "correlation-abc",
		};
		parent.Initialize(serviceProvider);

		_ = A.CallTo(() => _dispatcher.DispatchAsync(message, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());

		MessageContextHolder.Current = parent;

		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		// The freshly-created child must not escape into the AsyncLocal ambient holder — after the
		// dispatch returns, the caller's ambient context is unchanged (no AsyncLocal leak).
		MessageContextHolder.Current.ShouldBe(parent,
			"the auto-child context must be scoped to the dispatch — it must not overwrite the caller's ambient context");
	}
}
