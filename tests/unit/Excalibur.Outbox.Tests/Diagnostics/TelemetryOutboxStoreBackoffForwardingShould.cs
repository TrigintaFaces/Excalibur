// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

using FakeItEasy.Creation;

namespace Excalibur.Outbox.Tests.Diagnostics;

// Decorated-path lock for the outbox backoff capability. In opt-in telemetry scenarios the resolved IOutboxStore
// is the TelemetryOutboxStoreDecorator, so backoff must survive decoration or NextAttemptAt is never persisted.
//
// WHERE FAIL-OPEN LIVES, AND WHY THIS FILE CHANGED. Backoff is an optimization, not a mandatory terminal
// transition: a store that cannot schedule must still record the failure via the plain MarkFailedAsync — never a
// throw, never a silent drop. That fail-open is still required. It has MOVED.
//
// The decorator used to DECLARE IBackoffSchedulableOutboxStore unconditionally and fail-open inside itself. Under
// the ruled `IOutboxStore : IServiceProvider` seam a decorator declares no capability interfaces at all — it
// cannot, because a sealed class cannot express the 2^N interface combinations an inner store might have. It
// answers GetService instead: a MEASURED wrapper when the inner is capable, and an honest null when it is not.
//
// So the arm that asserted "the decorator fail-opens internally over a non-capable inner" is asserting a contract
// the ruling ABOLISHED. It is not deleted and its verdict is not relaxed. It is INVERTED to the property that
// replaced it — the decorator must report the honest absence — and the fail-open it used to guard is re-bound one
// layer out, at the consumer that now owns it (OutboxProcessor.MarkFailedWithBackoffOrFallbackAsync:817).
//
// A lock is not a record of what the code does; it is a claim about what the code must do. The claim is unchanged:
// a failure is never lost. Only the seam that delivers it moved.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class TelemetryOutboxStoreBackoffForwardingShould
{
	/// <summary>The property, not the mechanism: can a consumer discover the capability at all?</summary>
	private static object? Discover(IOutboxStore store, Type capability) =>
		capability.IsInstanceOfType(store) ? store : store.GetService(capability);

	/// <summary>
	/// A fake inner store that answers <c>GetService</c> the way a real store does. A bare FakeItEasy fake returns
	/// null for every capability, including ones it implements — probing that through the seam is a false RED.
	/// </summary>
	private static IOutboxStore FakeInnerStore(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);

		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);

		return fake;
	}

	[Fact]
	public void ExposeTheBackoffSchedulableCapability_WhenTheInnerStoreCanSchedule()
	{
		var inner = FakeInnerStore(b => b.Implements<IBackoffSchedulableOutboxStore>());
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		// SAFETY. The capability must survive decoration, or the processor never schedules a next attempt.
		Discover(decorator, typeof(IBackoffSchedulableOutboxStore)).ShouldNotBeNull(
			"The telemetry decorator wraps the store unconditionally. If backoff does not survive decoration, " +
			"NextAttemptAt is never persisted and the claim query stops throttling re-delivery.");
	}

	[Fact]
	public async Task ForwardMarkFailedWithBackoff_ToACapableInner()
	{
		var inner = FakeInnerStore(b => b.Implements<IBackoffSchedulableOutboxStore>());

		var decorator = Discover(new TelemetryOutboxStoreDecorator(inner), typeof(IBackoffSchedulableOutboxStore))
			as IBackoffSchedulableOutboxStore;

		decorator.ShouldNotBeNull("Capability must be discoverable before it can be forwarded.");

		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
		await decorator.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, CancellationToken.None);

		// Discoverability is not forwarding. The absolute next-attempt time must reach the inner store unchanged
		// (it is what gets persisted), not be recomputed or dropped by the measuring wrapper.
		A.CallTo(() => ((IBackoffSchedulableOutboxStore)inner)
				.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void NotAdvertiseBackoff_WhenTheInnerStoreCannotSchedule()
	{
		// Inner implements IOutboxStore only — NOT IBackoffSchedulableOutboxStore.
		var inner = FakeInnerStore();
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		// LIVENESS, and the replacement for the retired internal fail-open. An honest null is information: it tells
		// the consumer the capability is genuinely absent, so the consumer takes its own MarkFailedAsync fallback.
		// Advertising it and then degrading inside the decorator hid that decision from the only component
		// (OutboxProcessor) entitled to make it.
		Discover(decorator, typeof(IBackoffSchedulableOutboxStore)).ShouldBeNull(
			"The decorator advertises backoff over a store that cannot schedule it. The consumer reads a non-null " +
			"capability as a promise that NextAttemptAt will be persisted; it will not be. An honest null routes " +
			"the consumer to its own fail-open (OutboxProcessor:817) instead.");
	}

	[Fact]
	public async Task NeverLoseTheFailure_WhenTheInnerStoreCannotSchedule()
	{
		// The property the retired arm actually protected: a failure is RECORDED even with no backoff capability.
		// Bound here at the seam the decorator still owns — the plain MarkFailedAsync must pass straight through.
		var inner = FakeInnerStore();
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		await decorator.MarkFailedAsync("msg-1", "boom", 2, CancellationToken.None);

		A.CallTo(() => inner.MarkFailedAsync("msg-1", "boom", 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
