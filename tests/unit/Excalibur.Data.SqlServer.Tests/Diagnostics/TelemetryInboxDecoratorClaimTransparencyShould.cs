// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Diagnostics;

namespace Excalibur.Data.SqlServer.Tests.Diagnostics;

// bd-pux4gk (S842, ADR-336 Wave 2, SA decorator-transparency mandate 14312) — the atomic-claim analogue of the S841
// dziy0x Processing-transparency lock. A TelemetryInboxStoreDecorator wrapping a claim-capable inner MUST itself
// resolve as IClaimableInboxStore (else the idempotency presence-guard would FALSE-TRIP on a decorated-but-capable
// store), MUST forward TryClaim/Release to the capable inner, and MUST throw NotSupportedException over a non-capable
// inner — never a silent no-op (which would re-create the check-then-act race). Independent engage-test (author≠impl).
// Home: TelemetryInboxStoreDecorator is internal to Excalibur.Inbox; this is the IVT-bearing project that references it.
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class TelemetryInboxDecoratorClaimTransparencyShould
{
	[Fact]
	public void RemainClaimCapable_WhenWrappingACapableInner()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IClaimableInboxStore>());
		var decorator = new TelemetryInboxStoreDecorator(inner);

		// The presence-guard checks `store is IClaimableInboxStore`; a decorated capable inner must stay capable so a
		// valid (decorated) configuration is not rejected at startup.
		_ = decorator.ShouldBeAssignableTo<IClaimableInboxStore>();
	}

	[Fact]
	public async Task ForwardTryClaim_ToACapableInner()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IClaimableInboxStore>());
		A.CallTo(() => ((IClaimableInboxStore)inner).TryClaimAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));
		var decorator = (IClaimableInboxStore)new TelemetryInboxStoreDecorator(inner);

		var claimed = await decorator.TryClaimAsync("msg-1", "TestHandler", CancellationToken.None);

		claimed.ShouldBeTrue("the decorator must return the inner store's claim result");
		A.CallTo(() => ((IClaimableInboxStore)inner).TryClaimAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ForwardRelease_ToACapableInner()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IClaimableInboxStore>());
		var decorator = (IClaimableInboxStore)new TelemetryInboxStoreDecorator(inner);

		await decorator.ReleaseAsync("msg-1", "TestHandler", CancellationToken.None);

		A.CallTo(() => ((IClaimableInboxStore)inner).ReleaseAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowNotSupported_WhenInnerCannotClaim()
	{
		var inner = A.Fake<IInboxStore>(); // implements IInboxStore only — NOT IClaimableInboxStore
		var decorator = (IClaimableInboxStore)new TelemetryInboxStoreDecorator(inner);

		// Fail LOUD over a non-capable inner — never a silent fallback that would re-open the check-then-act race.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.TryClaimAsync("msg-1", "TestHandler", CancellationToken.None).AsTask());
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.ReleaseAsync("msg-1", "TestHandler", CancellationToken.None).AsTask());
	}
}
