// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Serialization;
using Excalibur.Inbox.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.SqlServer.Tests.SqlServer.Inbox.Diagnostics;

/// <summary>
/// Author≠implementer regression lock: wrapping a document-store inbox in
/// <see cref="TelemetryInboxStoreDecorator"/> must NOT silently downgrade the exactly-once guarantee.
/// </summary>
/// <remarks>
/// <para>
/// The decorated store must still be selected by <see cref="InboxMiddleware"/> on the highest-precedence
/// scoped-transactional path, and the atomic contract must still hold END TO END through the decorator.
/// </para>
/// <para>
/// Asserts PROPERTIES, never mechanisms: selection is proven by the handler observing a non-null
/// <see cref="IInboxTransactionScope"/> (the middleware exposes that only on the scoped path), and atomicity
/// is proven by reading the processed-mark and the handler's enlisted effect back from a FRESH store instance.
/// </para>
/// <para>
/// SAFETY arm: a handler that throws inside the scope leaves NEITHER the processed-mark NOR its enlisted
/// write behind. LIVENESS arm (mandatory): on the success path the handler runs, the mark commits, and both
/// are observable from a fresh instance — a decorator that aborted every transaction would satisfy safety
/// alone and must not pass.
/// </para>
/// <para>
/// NON-VACUOUS: against a decorator that does not implement <see cref="IScopedTransactionalInboxStore"/>
/// (the pre-fix shape), the middleware falls through to the weaker claim path — the handler observes a null
/// scope, and the direct interface casts throw InvalidCastException — so every arm here is RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class ScopedTransactionalTelemetryInboxDecoratorShould
{
	// Typed as IInboxStore so the interface casts below are legal regardless of the decorator’s declared
	// interface set — a decorator that does NOT implement the scoped seam then fails at RUNTIME (a test RED),
	// which is the non-vacuity signal, rather than failing to compile.
	private static IInboxStore Decorate(ScopedTransactionalInboxStoreDouble inner) => new TelemetryInboxStoreDecorator(inner);

	// ----- middleware selection (the silent-downgrade property) -----

	[Fact]
	public async Task Be_selected_by_the_middleware_on_the_scoped_transactional_path()
	{
		var backing = new InboxBacking();
		var middleware = CreateMiddleware(Decorate(new ScopedTransactionalInboxStoreDouble(backing)));

		IInboxTransactionScope? observedScope = null;
		var result = await InvokeAsync(middleware, ctx =>
		{
			observedScope = ctx.GetInboxTransactionScope();
			return true;
		});

		_ = observedScope.ShouldNotBeNull(
			"the middleware exposes a native transaction scope ONLY on the scoped-transactional path; a null scope means "
			+ "the telemetry-decorated document store was silently downgraded to the weaker at-least-once claim path");
		result.Succeeded.ShouldBeTrue("the handler's success result flows back through the scoped path");
	}

	[Fact]
	public async Task Commit_the_processed_mark_through_the_middleware_visible_on_a_fresh_store()
	{
		var backing = new InboxBacking();
		var middleware = CreateMiddleware(Decorate(new ScopedTransactionalInboxStoreDouble(backing)));

		_ = await InvokeAsync(middleware, _ => true);

		// LIVENESS — read back on a FRESH store instance over the same durable backing.
		var fresh = new ScopedTransactionalInboxStoreDouble(backing);
		(await fresh.IsProcessedAsync(MessageId, HandlerType, CancellationToken.None))
			.ShouldBeTrue("a successfully handled message is durably marked processed through the decorated scoped path");
	}

	[Fact]
	public async Task Leave_no_processed_mark_when_the_handler_reports_failure_through_the_middleware()
	{
		var backing = new InboxBacking();
		var middleware = CreateMiddleware(Decorate(new ScopedTransactionalInboxStoreDouble(backing)));

		var result = await InvokeAsync(middleware, _ => false);

		result.Succeeded.ShouldBeFalse("a failing handler's result must surface, not be swallowed");

		var fresh = new ScopedTransactionalInboxStoreDouble(backing);
		(await fresh.IsProcessedAsync(MessageId, HandlerType, CancellationToken.None))
			.ShouldBeFalse("a failed handler rolls the native transaction back so the message is redelivered");
	}

	// ----- atomicity directly through the decorator -----

	[Fact]
	public async Task Roll_back_both_the_mark_and_the_enlisted_write_when_the_handler_throws()
	{
		var backing = new InboxBacking();
		var sut = (IScopedTransactionalInboxStore)Decorate(new ScopedTransactionalInboxStoreDouble(backing));
		var marker = Guid.NewGuid().ToString();

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.TryProcessTransactionallyAsync(
				MessageId,
				HandlerType,
				(scope, _) =>
				{
					EnlistedWrite(scope, marker);
					throw new InvalidOperationException("simulated handler crash inside the native transaction");
				},
				CancellationToken.None));

		// SAFETY — NEITHER survives, read back on a FRESH instance.
		var fresh = new ScopedTransactionalInboxStoreDouble(backing);
		(await fresh.IsProcessedAsync(MessageId, HandlerType, CancellationToken.None))
			.ShouldBeFalse("a thrown handler must leave no processed-mark behind the telemetry decorator");
		backing.CommittedEffects.ShouldNotContain(marker,
			"the handler's enlisted write rolls back atomically with the processed-mark");
	}

	[Fact]
	public async Task Commit_both_the_mark_and_the_enlisted_write_on_the_success_path()
	{
		var backing = new InboxBacking();
		var sut = (IScopedTransactionalInboxStore)Decorate(new ScopedTransactionalInboxStoreDouble(backing));
		var marker = Guid.NewGuid().ToString();

		var processed = await sut.TryProcessTransactionallyAsync(
			MessageId,
			HandlerType,
			(scope, _) =>
			{
				EnlistedWrite(scope, marker);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None);

		// LIVENESS — a decorator that aborted every transaction would fail here.
		processed.ShouldBeTrue("a first successful transactional process returns true through the decorator");
		var fresh = new ScopedTransactionalInboxStoreDouble(backing);
		(await fresh.IsProcessedAsync(MessageId, HandlerType, CancellationToken.None))
			.ShouldBeTrue("the processed-mark commits and is durable on a fresh instance");
		backing.CommittedEffects.ShouldContain(marker,
			"the handler's enlisted write commits atomically with the processed-mark");
	}

	[Fact]
	public async Task Not_invoke_the_handler_for_an_already_processed_message()
	{
		var backing = new InboxBacking();
		var sut = (IScopedTransactionalInboxStore)Decorate(new ScopedTransactionalInboxStoreDouble(backing));
		var invocations = 0;

		_ = await sut.TryProcessTransactionallyAsync(
			MessageId, HandlerType, (_, _) => { invocations++; return ValueTask.CompletedTask; }, CancellationToken.None);
		var duplicate = await sut.TryProcessTransactionallyAsync(
			MessageId, HandlerType, (_, _) => { invocations++; return ValueTask.CompletedTask; }, CancellationToken.None);

		duplicate.ShouldBeFalse("an already-processed message is a duplicate");
		invocations.ShouldBe(1, "the handler runs exactly once — never on the duplicate delivery");
	}

	// ----- capability reporting (the second, distinct miss) -----

	[Fact]
	public void Report_SupportsTransactional_for_an_inner_that_implements_only_the_scoped_seam()
	{
		var sut = (IInboxStoreCapabilities)Decorate(new ScopedTransactionalInboxStoreDouble(new InboxBacking()));

		sut.SupportsTransactional.ShouldBeTrue(
			"a document-store inner (the Cosmos/Mongo shape) implements ONLY IScopedTransactionalInboxStore; reporting "
			+ "false here makes the middleware's capability probe skip the exactly-once path");
	}

	[Fact]
	public void Report_no_transactional_capability_for_an_inner_that_supports_neither_seam()
	{
		var sut = (IInboxStoreCapabilities)(IInboxStore)new TelemetryInboxStoreDecorator(A.Fake<IInboxStore>());

		sut.SupportsTransactional.ShouldBeFalse(
			"the capability report must be honest — a non-transactional inner must not be advertised as exactly-once");
	}

	// ----- harness -----

	private const string MessageId = "scoped-telemetry-msg";
	private static readonly string HandlerType = typeof(ScopedProbeMessage).FullName!;

	private static InboxMiddleware CreateMiddleware(IInboxStore store) => new(
		Options.Create(new InboxConfigurationOptions { Enabled = true }),
		store,
		deduplicator: null,
		new DispatchJsonSerializer(),
		NullLogger<InboxMiddleware>.Instance);

	private static async Task<IMessageResult> InvokeAsync(InboxMiddleware middleware, Func<IMessageContext, bool> handler)
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns(MessageId);

		DispatchRequestDelegate next = (_, ctx, _) =>
		{
			var succeeded = handler(ctx);
			var result = A.Fake<IMessageResult>();
			_ = A.CallTo(() => result.Succeeded).Returns(succeeded);
			return new ValueTask<IMessageResult>(result);
		};

		return await middleware.InvokeAsync(new ScopedProbeMessage(), context, next, CancellationToken.None);
	}

	private static void EnlistedWrite(IInboxTransactionScope scope, string marker) =>
		((TestInboxTransactionScope)scope).Enlist(marker);
}

/// <summary>A message whose type name is the handler-type key the middleware derives.</summary>
internal sealed class ScopedProbeMessage : IDispatchMessage;
