// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

namespace Excalibur.Outbox.Tests.Diagnostics;

// bd-stlcgg (S841, ADR-336 decorator-transparency ruling, SA 14115/PM 14116) — a store decorator becomes the
// resolved IOutboxStore in opt-in telemetry scenarios. It MUST forward IDeadLetterableOutboxStore to a capable
// inner (so the terminal transition still works through decoration), and MUST throw NotSupportedException over
// a non-capable inner — NEVER a silent no-op (a silent no-op = the re-claim bug, relocated behind the decorator).
// Independent engage-test (author≠impl): forward over capable, loud-throw over non-capable.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class TelemetryOutboxDecoratorDeadLetterTransparencyShould
{
	[Fact]
	public async Task ForwardMarkDeadLettered_ToACapableInner()
	{
		var inner = A.Fake<IOutboxStore>(b => b.Implements<IDeadLetterableOutboxStore>());
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		await decorator.MarkDeadLetteredAsync("msg-1", "retries exhausted", CancellationToken.None);

		A.CallTo(() => ((IDeadLetterableOutboxStore)inner)
				.MarkDeadLetteredAsync("msg-1", "retries exhausted", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowNotSupported_WhenInnerCannotDeadLetter()
	{
		var inner = A.Fake<IOutboxStore>(); // implements IOutboxStore only — NOT IDeadLetterableOutboxStore
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.MarkDeadLetteredAsync("msg-1", "retries exhausted", CancellationToken.None).AsTask());
	}
}
