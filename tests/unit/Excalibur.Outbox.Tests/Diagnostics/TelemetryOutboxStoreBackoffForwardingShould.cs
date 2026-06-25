// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

namespace Excalibur.Outbox.Tests.Diagnostics;

// Sprint 849 / Lane R3 (gejhft+ffxglb), decorated-path lock. In opt-in telemetry scenarios the resolved
// IOutboxStore is the TelemetryOutboxStoreDecorator. For the outbox backoff-apply to work under decoration, the
// decorator MUST implement IBackoffSchedulableOutboxStore and forward MarkFailedWithBackoffAsync to a capable
// inner store (so NextAttemptAt is persisted). Unlike dead-lettering (a mandatory terminal transition that fails
// LOUD), backoff is an optimization: over a NON-capable inner the decorator MUST fail-open to the plain
// MarkFailedAsync — never throw, never silently drop the failure. Independent engage-test (author≠impl):
// forward over capable, fail-open over non-capable. RED pre-fix: the decorator did not implement the capability.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class TelemetryOutboxStoreBackoffForwardingShould
{
	[Fact]
	public void ExposeTheBackoffSchedulableCapability()
	{
		var inner = A.Fake<IOutboxStore>(b => b.Implements<IBackoffSchedulableOutboxStore>());
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		// The capability must survive decoration so the processor's `is IBackoffSchedulableOutboxStore` check
		// passes when telemetry is enabled.
		decorator.ShouldBeAssignableTo<IBackoffSchedulableOutboxStore>();
	}

	[Fact]
	public async Task ForwardMarkFailedWithBackoff_ToACapableInner()
	{
		var inner = A.Fake<IOutboxStore>(b => b.Implements<IBackoffSchedulableOutboxStore>());
		var decorator = (IBackoffSchedulableOutboxStore)new TelemetryOutboxStoreDecorator(inner);

		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
		await decorator.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, CancellationToken.None);

		// The absolute next-attempt time must reach the inner store unchanged (it is what gets persisted).
		A.CallTo(() => ((IBackoffSchedulableOutboxStore)inner)
				.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FailOpenToMarkFailed_WhenInnerCannotSchedule()
	{
		// Inner implements IOutboxStore only — NOT IBackoffSchedulableOutboxStore.
		var inner = A.Fake<IOutboxStore>();
		var decorator = (IBackoffSchedulableOutboxStore)new TelemetryOutboxStoreDecorator(inner);

		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
		await decorator.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, CancellationToken.None);

		// Fail-open: the failure is still recorded via the plain capability (backoff is an optimization, not a
		// mandatory terminal transition) — never a throw, never a silent no-op.
		A.CallTo(() => inner.MarkFailedAsync("msg-1", "boom", 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
