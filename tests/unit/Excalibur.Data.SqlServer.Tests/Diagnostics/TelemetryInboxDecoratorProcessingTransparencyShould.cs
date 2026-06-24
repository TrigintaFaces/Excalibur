// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Diagnostics;

namespace Excalibur.Data.SqlServer.Tests.Diagnostics;

// bd-dziy0x (S841, ADR-336 decorator-transparency ruling, SA 14115/PM 14116) — completes the decorator-transparency
// coverage for the 4th store decorator (TelemetryInboxStoreDecorator). It MUST forward IProcessingTrackingInboxStore
// to a capable inner (durable Processing persists through the telemetry layer, keeping the at-most-once guard live)
// and MUST throw NotSupportedException over a non-capable inner — never a silent no-op. Independent engage-test
// (author≠impl). NOTE on home: TelemetryInboxStoreDecorator is internal to Excalibur.Inbox; this project is the
// IVT-bearing project that references Excalibur.Inbox (there is no Excalibur.Inbox.Tests project).
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class TelemetryInboxDecoratorProcessingTransparencyShould
{
	[Fact]
	public async Task ForwardMarkProcessing_ToACapableInner()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IProcessingTrackingInboxStore>());
		var decorator = new TelemetryInboxStoreDecorator(inner);

		await decorator.MarkProcessingAsync("msg-1", "TestHandler", CancellationToken.None);

		A.CallTo(() => ((IProcessingTrackingInboxStore)inner)
				.MarkProcessingAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowNotSupported_WhenInnerCannotTrackProcessing()
	{
		var inner = A.Fake<IInboxStore>(); // implements IInboxStore only — NOT IProcessingTrackingInboxStore
		var decorator = new TelemetryInboxStoreDecorator(inner);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.MarkProcessingAsync("msg-1", "TestHandler", CancellationToken.None).AsTask());
	}
}
