// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Diagnostics;

namespace Excalibur.Data.SqlServer.Tests.Diagnostics;

// bd-yp11r6 — the backoff analogue of the claim-transparency lock beside it, and the same rule: a decorator
// that forwards a capability it cannot honour must fail LOUDLY, never silently degrade.
//
// The defect this binds: MarkFailedWithBackoffAsync used to fall back to the inner store's plain
// MarkFailedAsync when that store could not schedule. That returns Applied — which on THIS member asserts
// the entry was mutated AND a backoff was scheduled — while no schedule existed. The caller is told the
// retry is throttled when it is not, and nothing downstream ever learns otherwise.
//
// Home: TelemetryInboxStoreDecorator is internal to Excalibur.Inbox; this is the IVT-bearing project that
// references it, matching where its sibling transparency locks already live.
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class TelemetryInboxDecoratorBackoffTransparencyShould
{
	private static readonly DateTimeOffset NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);

	// SAFETY: the capability is forwarded but cannot be honoured, so calling it is a programming error and
	// must surface as one. Returning Applied here is the defect.
	[Fact]
	public async Task ThrowRatherThanSilentlyDegrade_WhenTheInnerStoreCannotSchedule()
	{
		var inner = A.Fake<IInboxStore>();
		var decorator = (IBackoffSchedulableInboxStore)new TelemetryInboxStoreDecorator(inner);

		var thrown = await Should.ThrowAsync<NotSupportedException>(
			async () => await decorator.MarkFailedWithBackoffAsync(
				"msg-1", "TestHandler", "boom", 1, NextAttemptAt, CancellationToken.None));

		// The message must name the probe the caller skipped, or the throw tells them nothing actionable.
		thrown.Message.ShouldContain("SupportsBackoffScheduling");

		// And it must NOT have quietly marked the entry failed on the way past.
		A.CallTo(() => inner.MarkFailedAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// LIVENESS: without this, "throw always" would satisfy the arm above. A capable inner must still be
	// forwarded to, and its outcome returned verbatim rather than re-derived.
	[Fact]
	public async Task ForwardAndReturnTheInnerOutcomeVerbatim_WhenTheInnerStoreCanSchedule()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IBackoffSchedulableInboxStore>());
		A.CallTo(() => ((IBackoffSchedulableInboxStore)inner).MarkFailedWithBackoffAsync(
				"msg-1", "TestHandler", "boom", 1, NextAttemptAt, A<CancellationToken>._))
			.Returns(new ValueTask<InboxMarkFailedOutcome>(InboxMarkFailedOutcome.AlreadyProcessed));

		var decorator = (IBackoffSchedulableInboxStore)new TelemetryInboxStoreDecorator(inner);

		var outcome = await decorator.MarkFailedWithBackoffAsync(
			"msg-1", "TestHandler", "boom", 1, NextAttemptAt, CancellationToken.None);

		// AlreadyProcessed rather than Applied on purpose: a decorator that fabricated a success would pass
		// an Applied assertion, so the arm returns a non-default outcome the decorator cannot have invented.
		outcome.ShouldBe(
			InboxMarkFailedOutcome.AlreadyProcessed,
			"the decorator must return the inner store's outcome verbatim, not re-derive one");
	}

	// The flag is the contract's documented probe, so it must tell the truth in both directions — otherwise
	// a caller doing the right thing is still misled.
	[Fact]
	public void ReportTheEffectiveBackoffCapability_InBothDirections()
	{
		var incapable = (IInboxStoreCapabilities)new TelemetryInboxStoreDecorator(A.Fake<IInboxStore>());
		var capable = (IInboxStoreCapabilities)new TelemetryInboxStoreDecorator(
			A.Fake<IInboxStore>(b => b.Implements<IBackoffSchedulableInboxStore>()));

		incapable.SupportsBackoffScheduling.ShouldBeFalse();
		capable.SupportsBackoffScheduling.ShouldBeTrue();
	}

	// The decorator must STILL declare the interface either way — that is why it forwards at all, and why a
	// bare type test is not a sufficient probe. Pins the premise the other arms rest on.
	[Fact]
	public void RemainTypeAssignable_EvenWhenTheInnerStoreCannotSchedule()
	{
		var decorator = new TelemetryInboxStoreDecorator(A.Fake<IInboxStore>());

		_ = decorator.ShouldBeAssignableTo<IBackoffSchedulableInboxStore>();
	}
}
