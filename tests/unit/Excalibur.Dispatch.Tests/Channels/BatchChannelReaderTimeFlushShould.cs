// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Channels;

/// <summary>
/// Sprint 847 / Lane I (bead l0fip2) — author≠impl regression lock for the
/// <see cref="BatchChannelReader{T}"/> time-based flush defect (MS-I).
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (true pre-fix HEAD <c>301b4aa62</c>):</b> a single <c>timeoutCts</c> is created once OUTSIDE
/// the loop and linked into <c>linkedCts</c>. When the first timeout fires, <c>timeoutCts</c> cancels —
/// which also cancels the already-linked <c>linkedCts</c>, and <c>TryReset()</c> on the source cannot
/// un-cancel a linked CTS. After the first time-flush, every subsequent
/// <c>WaitToReadAsync(linkedCts.Token)</c> throws an <see cref="OperationCanceledException"/> whose source
/// is <c>linkedCts</c> (not <c>timeoutCts</c>, already reset), so the catch filter
/// <c>when (timeoutCts.Token.IsCancellationRequested)</c> is false and the exception ESCAPES the iterator —
/// terminating enumeration and dropping all subsequent items. Time-based flush is permanently dead after
/// the first window.
/// </para>
/// <para>
/// <b>Fix (FR-I1/I5):</b> do not reuse a linked CTS across timeout cycles (fresh CTS per iteration, or the
/// <c>WaitAsync(timeout)</c> pattern) so every timeout window flushes and no OCE escapes.
/// </para>
/// <para>
/// <b>Non-vacuity / determinism:</b> a real <see cref="Channel{T}"/> is driven through TWO timeout windows
/// (batchSize large enough never to fill, so each batch is time-triggered). The assertion is on batch
/// CONTENT, not wall-clock timing — the batch simply arrives when the (generous) timeout elapses, with a
/// 30s overall safety bound — so it is robust under full-suite load. Pre-fix: the SECOND
/// <c>MoveNextAsync</c> throws the escaped OCE (or never yields item B) ⇒ RED. Post-fix: a second
/// time-based batch containing item B is yielded ⇒ GREEN. The type is <c>internal sealed</c>; the test
/// project has <c>InternalsVisibleTo</c>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Feature", "Batching")]
public sealed class BatchChannelReaderTimeFlushShould
{
	private static readonly TimeSpan BatchTimeout = TimeSpan.FromMilliseconds(150);

	[Fact]
	public async Task FlushOnEveryTimeoutWindow_NotOnlyTheFirst()
	{
		// Arrange — batchSize=10 (never fills with 1 item/window) so each batch is time-triggered.
		var channel = Channel.CreateUnbounded<int>();
		var reader = new BatchChannelReader<int>(channel.Reader, batchSize: 10, batchTimeout: BatchTimeout);

		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await using var batches = reader.ReadBatchesAsync(safety.Token).GetAsyncEnumerator(safety.Token);

		// First window — write A, expect a time-flushed batch [1].
		await channel.Writer.WriteAsync(1, safety.Token);
		(await batches.MoveNextAsync()).ShouldBeTrue("the first timeout window must flush item A");
		batches.Current.ShouldBe([1]);

		// Second window — write B. Pre-fix: linkedCts is permanently cancelled after window #1, so this
		// MoveNextAsync either yields nothing or throws the escaped OCE -> RED. Post-fix: flushes [2].
		await channel.Writer.WriteAsync(2, safety.Token);
		(await batches.MoveNextAsync()).ShouldBeTrue(
			"the SECOND timeout window must STILL flush (item B) — time-based flush must survive repeated timeouts");
		batches.Current.ShouldBe([2]);
	}

	[Fact]
	public async Task DeliverEveryItemAcrossMultipleTimeoutWindows_NoItemLoss()
	{
		// AC-I3 — items written across >=2 windows, then channel completed, must all be delivered.
		var channel = Channel.CreateUnbounded<int>();
		var reader = new BatchChannelReader<int>(channel.Reader, batchSize: 10, batchTimeout: BatchTimeout);

		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		await channel.Writer.WriteAsync(10, safety.Token);
		await using var batches = reader.ReadBatchesAsync(safety.Token).GetAsyncEnumerator(safety.Token);

		(await batches.MoveNextAsync()).ShouldBeTrue();
		var delivered = new List<int>(batches.Current);

		// Second window after the first flush — the defect drops everything here on pre-fix.
		await channel.Writer.WriteAsync(20, safety.Token);
		(await batches.MoveNextAsync()).ShouldBeTrue("item 20 (window 2) must be delivered, not dropped");
		delivered.AddRange(batches.Current);

		delivered.ShouldBe([10, 20]);
	}
}
