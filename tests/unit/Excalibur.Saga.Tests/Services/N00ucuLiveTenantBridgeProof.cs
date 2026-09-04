// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// LIVE REGRESSION LOCK -- TenantBridgeCensus (BackendDeveloper), thread n00ucu.
// Originally built to convert the traced n00ucu / ADR-349 findings from source-reasoning into real-DI,
// real-dispatcher, real-middleware-pipeline measurements ("reproduce, do not trace"). The two original
// tests were RED (ShouldBeNull()) against the pre-fix code, proving the defects
// (Excalibur_Dispatch-50witz, Excalibur_Dispatch-by71nv) live.
//
// CORRECTED after an independent (Lamport) review found the first fix wrong at the VALUE, not the
// location: resolving the fallback through ITenantContext meant the shipped default registration
// (SingleTenantContext, a FIXED constant that ignores the ambient holder) stamped __default__
// unconditionally on every root context -- overwriting deliberate untenanted decisions
// (SagaTimeoutDeliveryService's own partition.IsRealTenant ? partition.TenantId : null) and splitting
// existing outbox/event streams across a deploy (pre-fix NULL rows fold to __untenanted__; post-fix
// rows would land at __default__ -- two different partitions for what used to be one). The original
// tests never caught it because they hand-registered AmbientTenantContext via AddTenantContext() --
// binding a container no consumer gets by default. Corrected design: the fallback reads
// TenantContextHolder.Current directly (never ITenantContext), so it is registration-independent and
// absence stays absence (folds to KeyedTenantPartition.Untenanted, never TenantDefaults.DefaultTenantId).
// These tests now bind the DEFAULT container -- AddDispatch()/AddDispatchScheduling() with NO tenancy
// call at all -- which is what a consumer actually receives.
//
// NOTE ON HOW TO RUN: at the time this was written, src/Dispatch/Excalibur.Dispatch.Abstractions did
// not build against the shared working tree (another live lane's reserved, uncommitted ADR-348/
// "OneVerb" WIP in TenantScopedStoreServiceCollectionExtensions.cs -- CS1735 doc-comment errors,
// unrelated to tenancy behavior). If that recurs, build/run this file from an isolated `git worktree
// add --detach <path> HEAD` instead of touching the shared tree.

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Services;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Services")]
public sealed class N00ucuLiveTenantBridgeProof
{
	// ============================================================================================
	// PROOF 1: SagaTimeoutDeliveryService loses tenant on any message a timeout handler republishes.
	// Filed as Excalibur_Dispatch-50witz.
	// ============================================================================================

	// Mirrors what a saga instance would publish on timeout (e.g. "OrderTimedOutEvent").
	private sealed record FollowUpEvent : IDispatchEvent;

	// Mirrors the timeout-delivery message SagaTimeoutDeliveryService dispatches to saga handling
	// middleware (a stand-in for the deserialized IDispatchAction/IDispatchEvent timeout payload).
	private sealed record TimeoutFiredEvent : IDispatchEvent;

	// Captures the context the FollowUpEvent handler actually observed, so the test can assert on
	// GetTenantId() / OutboundMessage.FromContext(...) -- the exact channel n00ucu is about.
	private sealed class Capture
	{
		public bool TimeoutHandlerInvoked { get; set; }
		public bool FollowUpHandlerInvoked { get; set; }
		public string? AmbientTenantContextHolderDuringTimeoutHandler { get; set; }
		public string? TimeoutHandlerOwnContextTenantId { get; set; }
		public string? FollowUpContextTenantId { get; set; }
		public string? FollowUpOutboundMessageTenantId { get; set; }
	}

	// IEventHandler<TEvent> (not the low-level IDispatchHandler<T>) is what AddDispatchHandlers's
	// registry-building step actually recognizes (it scans DI descriptors for
	// IActionHandler<>/IActionHandler<,>/IEventHandler<>/IDocumentHandler<> specifically -- confirmed
	// by reading DispatchServiceCollectionExtensions.cs directly after IDispatchHandler<T> silently
	// produced a "succeeded, zero handlers invoked" no-op). IEventHandler<T> has no context parameter,
	// so the handler reads the context through IMessageContextAccessor -- exactly the real mechanism a
	// saga handler would use, and exactly what n00ucu is about. (Injecting the accessor is also the
	// handler declaring it reads the context, which keeps it off the no-ambient fast path.)
	private sealed class TimeoutFiredHandler(Capture capture, IDispatcher dispatcher, IMessageContextAccessor contextAccessor)
		: IEventHandler<TimeoutFiredEvent>
	{
		public async Task HandleAsync(TimeoutFiredEvent eventMessage, CancellationToken cancellationToken)
		{
			capture.TimeoutHandlerInvoked = true;

			var context = contextAccessor.MessageContext;

			// Channel A: what SagaTimeoutDeliveryService.DeliverTimeoutAsync correctly re-establishes
			// via `using var tenantScope = TenantContextHolder.BeginScope(partition.TenantId);` around
			// the whole delivery -- confirm it IS ambient here, exactly as production code has it.
			capture.AmbientTenantContextHolderDuringTimeoutHandler = TenantContextHolder.Current;

			// Channel B: what the SAME production code leaves unset on the context it constructs
			// (`new MessageContext(dispatchMessage, scope.ServiceProvider) { MessageId = ... }`) until
			// ApplyAmbientTenantFallback runs.
			capture.TimeoutHandlerOwnContextTenantId = context?.GetTenantId();

			// Now do exactly what a saga handler does on timeout: publish a follow-up event via the
			// AMBIENT no-context overload (DispatcherContextExtensions.DispatchAsync), which is what
			// real handler code calls -- not the explicit-context overload.
			_ = await dispatcher.DispatchAsync(new FollowUpEvent(), cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class FollowUpHandler(Capture capture, IMessageContextAccessor contextAccessor)
		: IEventHandler<FollowUpEvent>
	{
		public Task HandleAsync(FollowUpEvent eventMessage, CancellationToken cancellationToken)
		{
			capture.FollowUpHandlerInvoked = true;

			var context = contextAccessor.MessageContext;
			capture.FollowUpContextTenantId = context?.GetTenantId();

			if (context is not null)
			{
				// The exact call every IOutboxStore.EnqueueAsync path is documented to go through
				// (OutboundMessage.cs's own remarks: "the single canonical mapping from IMessageContext
				// to a staged OutboundMessage"). This is the outbox row's TenantId, live.
				var outbound = OutboundMessage.FromContext(
					messageType: typeof(FollowUpEvent).FullName!,
					payload: [],
					destination: "test-destination",
					context: context);
				capture.FollowUpOutboundMessageTenantId = outbound.TenantId;
			}

			return Task.CompletedTask;
		}
	}

	private static ServiceProvider BuildSagaTimeoutProviderOnDefaultContainer(Capture capture)
	{
		// The DEFAULT container: AddDispatch() only, no AddTenantContext()/AddMultiTenancy() call at
		// all. This is the container a consumer actually receives, per the corrected review.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(capture);
		services.AddSingleton<IEventHandler<TimeoutFiredEvent>, TimeoutFiredHandler>();
		services.AddSingleton<IEventHandler<FollowUpEvent>, FollowUpHandler>();
		_ = services.AddDispatch(typeof(N00ucuLiveTenantBridgeProof).Assembly);
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task SagaTimeoutRedeliveryContext_ThreadsRealAmbientTenantOntoRepublishedFollowUpEvent_OnTheDefaultContainer()
	{
		var capture = new Capture();
		await using var provider = BuildSagaTimeoutProviderOnDefaultContainer(capture);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		const string realTenantId = "tenant-42";

		// Act: reproduce SagaTimeoutDeliveryService.DeliverTimeoutAsync's EXACT construction, byte for
		// byte against current HEAD (src/Excalibur/Excalibur.Saga/Services/SagaTimeoutDeliveryService.cs):
		//
		//   var partition = KeyedTenantPartition.FromStoredValue(timeout.TenantId);
		//   using var tenantScope = TenantContextHolder.BeginScope(partition.TenantId);
		//   ...
		//   var context = new MessageContext(dispatchMessage, scope.ServiceProvider) { MessageId = timeout.TimeoutId };
		//   context.ApplyAmbientTenantFallback();
		//   ...
		//   _ = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);
		string? sanityCheckJustBeforeDispatch;
		IMessageResult firstDispatchResult;
		using (TenantContextHolder.BeginScope(realTenantId))
		{
			sanityCheckJustBeforeDispatch = TenantContextHolder.Current;

			var context = new MessageContext(new TimeoutFiredEvent(), provider)
			{
				MessageId = "timeout-1",
			};
			context.ApplyAmbientTenantFallback();

			firstDispatchResult = await dispatcher.DispatchAsync(new TimeoutFiredEvent(), context, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Diagnostics first, so a failure anywhere shows the whole picture, not just the first assert.
		sanityCheckJustBeforeDispatch.ShouldBe(realTenantId, "BeginScope itself did not take effect");
		firstDispatchResult.Succeeded.ShouldBeTrue(
			$"first dispatch failed: {firstDispatchResult.ErrorMessage}");
		capture.TimeoutHandlerInvoked.ShouldBeTrue("TimeoutFiredHandler was never invoked");
		capture.FollowUpHandlerInvoked.ShouldBeTrue("FollowUpHandler was never invoked");

		capture.AmbientTenantContextHolderDuringTimeoutHandler.ShouldBe(realTenantId);
		capture.TimeoutHandlerOwnContextTenantId.ShouldBe(realTenantId);

		// THE MEASUREMENT: the follow-up event the timeout handler republished via the ambient overload
		// -- CreateChildContext() correctly propagates IdentityFeature.TenantId parent->child.
		capture.FollowUpContextTenantId.ShouldBe(realTenantId);

		// THE MEASUREMENT THAT MATTERS: what actually lands on the outbox row for this tenant's own
		// timeout-triggered follow-up event -- on the DEFAULT container, no ITenantContext registered.
		capture.FollowUpOutboundMessageTenantId.ShouldBe(realTenantId);
	}

	[Fact]
	public async Task SagaTimeoutRedeliveryContext_StaysUntenanted_ForAnEstateWideTimeout_EvenWithSingleTenantContextRegistered()
	{
		// THE REGRESSION THIS TEST EXISTS TO CATCH (found by independent review after the first fix
		// shipped): an estate-wide timeout was never scoped to a specific tenant, and the redelivery it
		// triggers must stay untenanted. A fallback sourced from ITenantContext would ignore that absence
		// whenever the DEFAULT SingleTenantContext is registered (its TenantId is a FIXED constant --
		// TenantDefaults.DefaultTenantId -- that never consults the ambient holder), silently converting
		// an untenanted operation into an owned one.
		//
		// This test registers AddDefaultTenantContext() explicitly (SingleTenantContext, exactly the
		// shipped default for a host that has not opted into multi-tenancy) and establishes no tenant --
		// proving the TenantContextHolder-direct fallback does not stamp __default__ over an absence.
		//
		// The arm establishes the ambient with BeginScope(null) rather than with the untenanted sentinel
		// SagaTimeoutDeliveryService now binds, because the property under test is that ABSENCE is not
		// promoted to __default__, and null is the stronger input for it: the fallback maps every spelling
		// of untenanted onto the same absence, so an arm that passes on null passes on the sentinel. The
		// spellings are proven interchangeable by ApplyAmbientTenantFallback's own fold
		// (KeyedTenantPartition.ToSignedTenantId), not assumed here.
		var capture = new Capture();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(capture);
		services.AddSingleton<IEventHandler<TimeoutFiredEvent>, TimeoutFiredHandler>();
		services.AddSingleton<IEventHandler<FollowUpEvent>, FollowUpHandler>();
		_ = services.AddDispatch(typeof(N00ucuLiveTenantBridgeProof).Assembly);
		_ = services.AddDefaultTenantContext(); // registers SingleTenantContext -- TenantId is ALWAYS "__default__"

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		IMessageResult firstDispatchResult;
		using (TenantContextHolder.BeginScope(null)) // partition.IsRealTenant == false
		{
			var context = new MessageContext(new TimeoutFiredEvent(), provider)
			{
				MessageId = "timeout-estate-wide",
			};
			context.ApplyAmbientTenantFallback();

			firstDispatchResult = await dispatcher.DispatchAsync(new TimeoutFiredEvent(), context, CancellationToken.None)
				.ConfigureAwait(false);
		}

		firstDispatchResult.Succeeded.ShouldBeTrue($"first dispatch failed: {firstDispatchResult.ErrorMessage}");
		capture.FollowUpHandlerInvoked.ShouldBeTrue("FollowUpHandler was never invoked");

		// THE MEASUREMENT: must be null (untenanted), NEVER "__default__" -- despite SingleTenantContext
		// being registered and its ITenantContext.TenantId always answering "__default__".
		capture.TimeoutHandlerOwnContextTenantId.ShouldBeNull();
		capture.FollowUpContextTenantId.ShouldBeNull();
		capture.FollowUpOutboundMessageTenantId.ShouldBeNull();
	}

	// ============================================================================================
	// PROOF 2: IDispatchScheduler.ScheduleOnceAsync/ScheduleRecurringAsync* capture tenant from the
	// message's own ITenantAware.TenantId when it declares one, else the ambient tenant established at
	// the moment of scheduling (RecurringDispatchScheduler.ExtractTenantId). Before the fix, ITenantAware
	// was the ONLY mechanism, and grep across src/ (positive-controlled against IDispatchEvent, 29 hits)
	// found ZERO production message types implementing it -- so every schedule was persisted with
	// TenantId = null, regardless of the caller's ambient tenant. Filed as Excalibur_Dispatch-by71nv.
	// ============================================================================================

	private sealed record PlainScheduledCommand : IDispatchAction;

	[Fact]
	public async Task ScheduleOnceAsync_PersistsAmbientTenantId_ForAPlainMessageType_OnTheDefaultContainer()
	{
		// DEFAULT container: AddDispatch() + AddDispatchScheduling() only, no tenancy call at all.
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddDispatch(typeof(N00ucuLiveTenantBridgeProof).Assembly);
		_ = services.AddDispatchScheduling();

		await using var provider = services.BuildServiceProvider();
		var scheduler = provider.GetRequiredService<IDispatchScheduler>();
		var store = provider.GetRequiredService<IScheduleStore>();

		const string realTenantId = "tenant-42";

		// Act: schedule a message with a REAL ambient tenant established -- exactly what an
		// authenticated multi-tenant request handler calling `_scheduler.ScheduleOnceAsync(...)` would
		// have active. PlainScheduledCommand does NOT implement ITenantAware -- which matches every
		// production message type in this framework today (grep-confirmed: zero implementers of
		// ITenantAware in src/, positive-controlled).
		using (TenantContextHolder.BeginScope(realTenantId))
		{
			await scheduler.ScheduleOnceAsync(
				DateTimeOffset.UtcNow.AddMinutes(5),
				new PlainScheduledCommand(),
				CancellationToken.None).ConfigureAwait(false);
		}

		// Assert: read back what was ACTUALLY persisted -- not what the caller's ambient tenant was.
		var stored = (await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false)).ToList();
		stored.Count.ShouldBe(1, "the schedule was not persisted at all");

		// THE MEASUREMENT: the real ambient tenant active at the moment of scheduling is now the
		// persisted row's TenantId, on the DEFAULT container (no ITenantContext registered at all).
		stored[0].TenantId.ShouldBe(realTenantId);
	}

	[Fact]
	public async Task ScheduleOnceAsync_PrefersMessageLevelTenantId_OverAmbientTenant_WhenMessageIsTenantAware()
	{
		// The message-level source must win -- a more specific source than the ambient fallback.
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddDispatch(typeof(N00ucuLiveTenantBridgeProof).Assembly);
		_ = services.AddDispatchScheduling();

		await using var provider = services.BuildServiceProvider();
		var scheduler = provider.GetRequiredService<IDispatchScheduler>();
		var store = provider.GetRequiredService<IScheduleStore>();

		// Ambient tenant is "tenant-ambient"; the message explicitly declares "tenant-explicit" -- the
		// explicit, message-level value must be the one persisted.
		using (TenantContextHolder.BeginScope("tenant-ambient"))
		{
			await scheduler.ScheduleOnceAsync(
				DateTimeOffset.UtcNow.AddMinutes(5),
				new TenantAwareScheduledCommand("tenant-explicit"),
				CancellationToken.None).ConfigureAwait(false);
		}

		var stored = (await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false)).ToList();
		stored.Count.ShouldBe(1, "the schedule was not persisted at all");
		stored[0].TenantId.ShouldBe("tenant-explicit");
	}

	[Fact]
	public async Task ScheduleOnceAsync_StaysUntenanted_WhenNoAmbientTenantEstablished_OnTheDefaultContainer()
	{
		// SAFETY ARM for PROOF 2: no ambient tenant, no ITenantAware message -- the schedule must stay
		// untenanted, not acquire a default. On the DEFAULT container (no tenancy call at all).
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddDispatch(typeof(N00ucuLiveTenantBridgeProof).Assembly);
		_ = services.AddDispatchScheduling();

		await using var provider = services.BuildServiceProvider();
		var scheduler = provider.GetRequiredService<IDispatchScheduler>();
		var store = provider.GetRequiredService<IScheduleStore>();

		// Deliberately no TenantContextHolder.BeginScope(...) around this call.
		await scheduler.ScheduleOnceAsync(
			DateTimeOffset.UtcNow.AddMinutes(5),
			new PlainScheduledCommand(),
			CancellationToken.None).ConfigureAwait(false);

		var stored = (await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false)).ToList();
		stored.Count.ShouldBe(1, "the schedule was not persisted at all");
		stored[0].TenantId.ShouldBeNull();
	}

	private sealed record TenantAwareScheduledCommand(string TenantId) : IDispatchAction, ITenantAware;
}
