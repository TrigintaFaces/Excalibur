// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Ordering;

namespace Excalibur.Dispatch.Tests.Middleware.Ordering;

/// <summary>
/// Author≠impl regression lock (TestsDeveloper) for wtezay — the <see cref="OrderingValidationMiddleware"/>
/// safety-critical fail-closed contract. Once ordering enforcement is active it MUST reject, never silently
/// pass: a message with no stamped sequence (advertised-but-unfed misconfiguration) and a non-strictly-
/// increasing sequence per key are both rejected with <see cref="OutOfOrderMessageException"/>; strictly
/// increasing sequences pass and advance the per-key high-water mark, and distinct ordering keys are tracked
/// independently.
/// </summary>
/// <remarks>
/// <b>RED mutants:</b> drop the unstamped-sequence throw ⇒ (a) RED (silent bypass re-opens the degrade);
/// weaken <c>sequence &lt;= last</c> to <c>sequence &lt; last</c> ⇒ (b) RED on the equal-sequence replay;
/// advance the watermark before the ordered check ⇒ (c)/(d) drift. Fail-closed means a reject path exists
/// for every non-conforming input — never a pass.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Ordering")]
public sealed class OrderingValidationMiddlewareShould
{
	[Fact]
	public async Task FailClosed_WhenActiveButMessageCarriesNoSequence()
	{
		var middleware = new OrderingValidationMiddleware();
		var nextCalled = false;

		_ = await Should.ThrowAsync<OutOfOrderMessageException>(async () => await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			UnstampedContext(),
			(_, _, _) => { nextCalled = true; return new ValueTask<IMessageResult>(A.Fake<IMessageResult>()); },
			CancellationToken.None));

		nextCalled.ShouldBeFalse("an unstamped message on an active ordering middleware must be rejected, never passed downstream.");
	}

	[Fact]
	public async Task FailClosed_WhenSequenceIsNotStrictlyIncreasing()
	{
		var middleware = new OrderingValidationMiddleware();

		// First message (seq 5) is accepted and sets the high-water mark.
		_ = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), StampedContext(5, "orders"), PassThrough(), CancellationToken.None);

		// A replay at the SAME sequence (5) — not strictly greater — must be rejected.
		var nextCalled = false;
		_ = await Should.ThrowAsync<OutOfOrderMessageException>(async () => await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			StampedContext(5, "orders"),
			(_, _, _) => { nextCalled = true; return new ValueTask<IMessageResult>(A.Fake<IMessageResult>()); },
			CancellationToken.None));
		nextCalled.ShouldBeFalse("an equal (non-increasing) sequence must be rejected and must NOT invoke the pipeline.");

		// A lower sequence (3) is likewise rejected.
		_ = await Should.ThrowAsync<OutOfOrderMessageException>(async () => await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(), StampedContext(3, "orders"), PassThrough(), CancellationToken.None));
	}

	[Fact]
	public async Task Passes_WhenStrictlyIncreasing_ForTheSameKey()
	{
		var middleware = new OrderingValidationMiddleware();

		foreach (var sequence in new long[] { 1, 2, 3, 100 })
		{
			var called = false;
			_ = await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(),
				StampedContext(sequence, "orders"),
				(_, _, _) => { called = true; return new ValueTask<IMessageResult>(A.Fake<IMessageResult>()); },
				CancellationToken.None);
			called.ShouldBeTrue($"a strictly-increasing sequence ({sequence}) must be accepted and passed downstream.");
		}
	}

	[Fact]
	public async Task TracksOrderingKeysIndependently()
	{
		var middleware = new OrderingValidationMiddleware();

		// Advance key "A" to a high watermark.
		_ = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), StampedContext(500, "A"), PassThrough(), CancellationToken.None);

		// A low sequence on a DIFFERENT key "B" must NOT be rejected — watermarks are per-key.
		var called = false;
		_ = await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			StampedContext(1, "B"),
			(_, _, _) => { called = true; return new ValueTask<IMessageResult>(A.Fake<IMessageResult>()); },
			CancellationToken.None);
		called.ShouldBeTrue("ordering keys are independent — key B's first message must not be gated by key A's watermark.");
	}

	[Fact]
	public async Task AcceptRedeliveryOfSameSequence_AfterHandlerFailed_AdvanceOnSuccessNotOnReceipt()
	{
		// smeh4k: the per-key watermark advances only AFTER the handler succeeds — NOT on receipt. So when a
		// handler FAILS on seq-N, the watermark stays put and an at-least-once REDELIVERY of seq-N is still
		// in-order → accepted. NON-VACUITY: the old advance-on-receipt behavior would have advanced past N
		// BEFORE the handler ran, so the redelivery of N would be rejected as out-of-order (N ≤ watermark) → RED.
		var middleware = new OrderingValidationMiddleware();

		// seq 0 processed successfully → watermark = 0.
		_ = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), StampedContext(0, "orders"), PassThrough(), CancellationToken.None);

		// seq 1: the handler THROWS → processing fails; the exception propagates and the watermark must NOT advance.
		_ = await Should.ThrowAsync<InvalidOperationException>(async () => await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			StampedContext(1, "orders"),
			(_, _, _) => throw new InvalidOperationException("handler failed processing seq 1"),
			CancellationToken.None));

		// seq 1 REDELIVERED with a succeeding handler → must be ACCEPTED (watermark never advanced past 0).
		var redeliveryAccepted = false;
		_ = await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			StampedContext(1, "orders"),
			(_, _, _) => { redeliveryAccepted = true; return new ValueTask<IMessageResult>(A.Fake<IMessageResult>()); },
			CancellationToken.None);

		redeliveryAccepted.ShouldBeTrue(
			"smeh4k: a handler failure must not advance the watermark, so the at-least-once redelivery of the same sequence is accepted (advance-on-success, not on-receipt).");
	}

	private static DispatchRequestDelegate PassThrough() =>
		(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

	private static IMessageContext UnstampedContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		return context;
	}

	private static IMessageContext StampedContext(long sequence, string orderingKey)
	{
		var context = UnstampedContext();
		context.SetOrderingSequence(sequence, orderingKey);
		return context;
	}
}
