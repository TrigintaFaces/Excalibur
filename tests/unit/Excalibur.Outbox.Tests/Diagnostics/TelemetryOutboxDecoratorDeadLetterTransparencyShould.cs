// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

using FakeItEasy.Creation;

namespace Excalibur.Outbox.Tests.Diagnostics;

// Decorator-transparency lock for the dead-letter capability. A store decorator becomes the resolved IOutboxStore
// in opt-in telemetry scenarios, so the terminal transition must still work through decoration.
//
// THE NON-CAPABLE CONTRACT INVERTED, DELIBERATELY. This file used to require the decorator to throw
// NotSupportedException over a non-capable inner — "loud beats silent," because a silent no-op leaves the message
// re-claimable forever. That reasoning was right about the danger and wrong about the remedy, and its own sibling
// lock said so: NotAdvertiseDeadLettering_OverANonDeadLetterableInner calls the unconditional declaration "the
// bug," because the throw is only reachable through a capability the decorator should never have advertised.
//
// The loud throw and the silent no-op are the same defect wearing different clothes: both are what a consumer gets
// AFTER it probed the capability, found it, and reasonably believed the store could honour it. Under the ruled
// `IOutboxStore : IServiceProvider` seam the decorator declares nothing and answers GetService — a measured
// wrapper when the inner is dead-letterable, an honest null when it is not. The consumer never gets far enough to
// be thrown at.
//
// So: forward over capable (unchanged), and report an honest absence over non-capable (was: throw). The property
// protected is identical and is asserted here — a dead-letter request is NEVER silently swallowed. What changed is
// that the absence is now discoverable BEFORE the call instead of explosive during it.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class TelemetryOutboxDecoratorDeadLetterTransparencyShould
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
	public async Task ForwardMarkDeadLettered_ToACapableInner()
	{
		var inner = FakeInnerStore(b => b.Implements<IDeadLetterableOutboxStore>()
			.Implements<IClaimScopedDeadLetterableOutboxStore>());

		var deadLetterable = Discover(new TelemetryOutboxStoreDecorator(inner), typeof(IDeadLetterableOutboxStore))
			as IDeadLetterableOutboxStore;

		deadLetterable.ShouldNotBeNull(
			"The terminal transition must survive decoration. If it does not, a message that exhausted its retries " +
			"is never dead-lettered and stays re-claimable forever.");

		await deadLetterable.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, CancellationToken.None);

		// Discoverability is not forwarding. A wrapper that answers the probe and swallows the call is the
		// re-claim bug relocated behind the decorator.
		A.CallTo(() => ((IDeadLetterableOutboxStore)inner)
				.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		var strongInner = (IClaimScopedDeadLetterableOutboxStore)inner;
		A.CallTo(() => strongInner.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, "claim-A", A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied));
		A.CallTo(() => strongInner.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, "claim-B", A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.ClaimLost));
		var strong = Discover(new TelemetryOutboxStoreDecorator(inner), typeof(IClaimScopedDeadLetterableOutboxStore))
			.ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>();
		(await strong.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, "claim-A", CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied);
		(await strong.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, "claim-B", CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.ClaimLost);
		A.CallTo(() => strongInner.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, "claim-A", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => strongInner.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7, "claim-B", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
		// The original weak call above is still exactly one: neither typed call may downgrade to it.
		A.CallTo(() => ((IDeadLetterableOutboxStore)inner)
			.MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void NotAdvertiseDeadLettering_WhenInnerCannotDeadLetter()
	{
		var inner = FakeInnerStore(); // implements IOutboxStore only — NOT IDeadLetterableOutboxStore
		var decorator = new TelemetryOutboxStoreDecorator(inner);
		Discover(decorator, typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeNull();

		// LIVENESS. Replaces the retired ThrowNotSupported arm. The consumer must learn the capability is absent by
		// probing, not by catching. An honest null is information; a NotSupportedException raised from a capability
		// the decorator should never have advertised is an accident presented as a contract.
		Discover(decorator, typeof(IDeadLetterableOutboxStore)).ShouldBeNull(
			"The decorator advertises dead-lettering over a store that cannot honour it. A consumer that probes " +
			"the capability, finds it, and calls it gets an exception instead of the honest null that would have " +
			"told it the capability is genuinely absent.");
	}

	[Fact]
	public void NeverSilentlySwallowADeadLetterRequest_OverANonCapableInner()
	{
		// NON-VACUITY for the arm above, and the property the retired throw actually protected.
		//
		// `ShouldBeNull` alone is satisfied by a decorator that answers null to EVERY capability — including ones
		// the inner genuinely has. That decorator would be catastrophically broken and this file would be green.
		// So: the same decorator, over a CAPABLE inner, must still answer. Absence must be reported because the
		// capability is absent, not because the decorator reports absence for everything.
		var capable = FakeInnerStore(b => b.Implements<IDeadLetterableOutboxStore>()
			.Implements<IClaimScopedDeadLetterableOutboxStore>());
		var nonCapable = FakeInnerStore();
		Discover(new TelemetryOutboxStoreDecorator(capable), typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldNotBeNull();
		Discover(new TelemetryOutboxStoreDecorator(nonCapable), typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeNull();

		Discover(new TelemetryOutboxStoreDecorator(capable), typeof(IDeadLetterableOutboxStore)).ShouldNotBeNull(
			"A dead-letterable inner must yield a discoverable capability. If this is null, the null in the arm " +
			"above proves nothing — the decorator is answering null unconditionally.");

		Discover(new TelemetryOutboxStoreDecorator(nonCapable), typeof(IDeadLetterableOutboxStore)).ShouldBeNull(
			"A non-dead-letterable inner must yield an honest absence.");
	}

    [Theory]
    [InlineData(false, OutboxCompletionOutcome.Applied)]
    [InlineData(true, OutboxCompletionOutcome.Applied)]
    [InlineData(true, OutboxCompletionOutcome.ClaimLost)]
    public async Task HonorTheOwningStoreConditionalClaimTerminalAdmission(bool advertised, OutboxCompletionOutcome outcome)
    {
        var inner = A.Fake<IOutboxStore>(options => options.Implements<IClaimScopedDeadLetterableOutboxStore>()
            .Implements<IDeadLetterableOutboxStore>());
        var admitted = (IClaimScopedDeadLetterableOutboxStore)inner;
        A.CallTo(() => inner.GetService(A<Type>._)).ReturnsLazily((Type type) =>
            advertised && type == typeof(IClaimScopedDeadLetterableOutboxStore) ? admitted : null);
        using var cancellation = new CancellationTokenSource();
        const string persistedId = "recorded-tenant-B-id";
        const string receipt = "opaque-exact-acquisition ";
        A.CallTo(() => admitted.MarkDeadLetteredAsync(persistedId, "terminal", 7, receipt, cancellation.Token))
            .Returns(new ValueTask<OutboxCompletionOutcome>(outcome));
        var outer = new TelemetryOutboxStoreDecorator(inner);
        var capability = outer.GetService(typeof(IClaimScopedDeadLetterableOutboxStore));
        if (advertised)
        {
            var strong = capability.ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>();
            (await strong.MarkDeadLetteredAsync(persistedId, "terminal", 7, receipt, cancellation.Token)).ShouldBe(outcome);
            A.CallTo(() => admitted.MarkDeadLetteredAsync(persistedId, "terminal", 7, receipt, cancellation.Token))
                .MustHaveHappenedOnceExactly();
        }
        else
        {
            capability.ShouldBeNull("concrete assignability cannot override the owning store's admission decision");
            A.CallTo(() => admitted.MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
        A.CallTo(() => ((IDeadLetterableOutboxStore)inner).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
