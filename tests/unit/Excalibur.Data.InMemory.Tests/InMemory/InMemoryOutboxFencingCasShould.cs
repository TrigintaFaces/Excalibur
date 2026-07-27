// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Author≠impl fence-CAS regression lock for item-2 (sd36sc server-side outbox fencing) —
/// <see cref="InMemoryOutboxStore"/> enforces a monotonic fencing high-water mark so a superseded leader
/// (a token below the high-water) is fenced off: <c>MarkSentAsync</c> <b>fails closed</b> with
/// <see cref="StaleOutboxFencingTokenException"/>, and <c>GetUnsentMessagesAsync</c> (a set-based claim)
/// simply yields nothing — never throwing. A valid token (≥ high-water) is honored and advances the mark.
/// Same guard+advance contract as the SqlServer / Mongo fence siblings, in-process backend.
/// </summary>
/// <remarks>
/// Deterministic (no wall-clock, no Docker). The high-water starts at 0 and only ever advances.
/// <para>
/// <b>RED-on-mutant:</b> drop the CAS guard (accept any token) ⇒
/// <see cref="MarkSent_WithAStaleToken_FailsClosed"/> no longer throws → RED. Make
/// <c>GetUnsentMessagesAsync</c> throw on a stale token (instead of yielding empty) ⇒
/// <see cref="GetUnsent_WithAStaleToken_YieldsEmpty_NeverThrows"/> → RED. Change the guard from
/// <c>&lt;</c> to <c>&lt;=</c> (reject an equal token) ⇒ <see cref="MarkSent_WithAnEqualToken_IsAccepted"/> → RED.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class InMemoryOutboxFencingCasShould : IDisposable
{
	private readonly InMemoryOutboxStore _store;

	public InMemoryOutboxFencingCasShould()
	{
		var options = Options.Create(new InMemoryOutboxOptions { MaxMessages = 10_000 });
		_store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);
	}

	public void Dispose() => _store.Dispose();

	private async Task<string> StageAsync()
	{
		var msg = new OutboundMessage("test.message", [1], "dest");
		await _store.StageMessageAsync(msg, CancellationToken.None);
		return msg.Id;
	}

	[Fact]
	public async Task MarkSent_WithAStaleToken_FailsClosed()
	{
		var a = await StageAsync();
		var b = await StageAsync();

		// A valid tenure marks a message sent and advances the high-water to 10.
		await _store.MarkSentAsync(a, fencingToken: 10, CancellationToken.None);

		// A superseded leader presents a lower token — must be rejected fail-closed (never silently applied).
		var ex = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			() => _store.MarkSentAsync(b, fencingToken: 5, CancellationToken.None).AsTask());

		ex.PresentedToken.ShouldBe(5, "the exception must report the stale token that was presented");
		ex.HighWaterToken.ShouldBe(10, "the exception must report the high-water it was fenced against");
	}

	[Fact]
	public async Task MarkSent_WithAnEqualToken_IsAccepted()
	{
		var a = await StageAsync();
		var b = await StageAsync();

		await _store.MarkSentAsync(a, fencingToken: 10, CancellationToken.None); // high-water = 10

		// An equal token is the SAME tenure, still valid (guard is strictly-less-than, not <=).
		await Should.NotThrowAsync(
			() => _store.MarkSentAsync(b, fencingToken: 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetUnsent_WithAStaleToken_YieldsEmpty_NeverThrows()
	{
		_ = await StageAsync();

		// Advance the high-water to 10 via a valid claim.
		_ = await _store.GetUnsentMessagesAsync(10, fencingToken: 10, CancellationToken.None);

		// A stale token on the set-based claim yields nothing — it must NOT throw (per the IOutboxStore contract).
		var stale = (await _store.GetUnsentMessagesAsync(10, fencingToken: 5, CancellationToken.None)).ToList();
		stale.ShouldBeEmpty("a stale fencing token must yield zero claimable rows on the set-based claim, not throw");
	}

	[Fact]
	public async Task UnfencedClaim_IsPlainClaim_UnaffectedByTheHighWater()
	{
		var a = await StageAsync();
		var b = await StageAsync();

		await _store.MarkSentAsync(a, fencingToken: 10, CancellationToken.None); // high-water = 10

		// No fencing configured (single-instance / no leader election) → the caller uses the unfenced
		// IOutboxStore members, which take no token at all and are never gated by the high-water.
		await Should.NotThrowAsync(
			() => _store.MarkSentAsync(b, CancellationToken.None).AsTask());
		var claimed = (await _store.GetUnsentMessagesAsync(10, CancellationToken.None)).ToList();
		claimed.ShouldBeEmpty("both staged messages are now sent; an unfenced claim is unaffected by the fencing high-water");
	}
}
