// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using FakeItEasy.Creation;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption;

// Durable-capability transparency across the encrypting store decorators, which become the resolved
// IInboxStore / IOutboxStore in opt-in encryption scenarios.
//
// TWO DIFFERENT SEAMS, and this file now spans both — deliberately, because the divergence IS the finding.
//
//   INBOX  (EncryptingInboxStoreDecorator, unchanged): still declares IProcessingTrackingInboxStore and forwards
//          MarkProcessingAsync to a capable inner, throwing NotSupportedException over a non-capable one. The old
//          declare-then-degrade contract. Its two arms below are untouched.
//
//   OUTBOX (EncryptingOutboxStoreDecorator, l0qpxo deny-by-default): NO LONGER declares any capability interface.
//          A consumer discovers a capability by probing GetService(type). IDeadLetterableOutboxStore is in the
//          decorator's *forwardable* set (its surface carries no message payload), so:
//              capable inner   -> GetService returns the inner's own IDeadLetterableOutboxStore (raw forward)
//              incapable inner -> GetService returns null   (deny-by-default; the honest absence)
//
// THE OUTBOX INVERSION, RECORDED NOT DELETED. `ThrowNotSupported_WhenOutboxInnerCannotDeadLetter` asserted a THROW
// over a non-capable inner — correct while the decorator declared the interface unconditionally. The ruled seam
// removes the declaration, so the honest signal is a null from GetService, not a throw the consumer never asked
// for. The degradation did not vanish; it moved to the consumer entitled to choose it. The arm is inverted (verdict
// flipped, capability still named and pinned in BOTH directions), never relaxed, and each outbox arm carries its
// own non-vacuity/liveness half so a GetService that answered null to everything cannot pass it.
//
// FIXTURE HONESTY. A bare FakeItEasy fake answers GetService with null for every type — including interfaces it
// demonstrably implements. Forwarding through such a fake reports the capability absent even when present, a false
// RED that looks like diligence. So the outbox fakes answer GetService the way a real store does: return the inner
// for a capability it implements, null otherwise. Independent engage-test (author≠impl).
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingDecoratorCapabilityTransparencyShould
{
	private static EncryptingInboxStoreDecorator CreateInboxDecorator(IInboxStore inner) =>
		new(inner, A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()));

	private static EncryptingOutboxStoreDecorator CreateOutboxDecorator(IOutboxStore inner) =>
		new(inner, A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()));

	// An outbox fake whose GetService behaves like a real store's (see FIXTURE HONESTY above), so that raw-forwarded
	// capabilities resolve for a capable inner instead of collapsing to a fixture-induced null.
	private static IOutboxStore HonestOutboxFake(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);
		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);
		return fake;
	}

	[Fact]
	public async Task ForwardMarkProcessing_ToACapableInboxInner()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IProcessingTrackingInboxStore>());
		var decorator = CreateInboxDecorator(inner);

		await decorator.MarkProcessingAsync("msg-1", "TestHandler", CancellationToken.None);

		A.CallTo(() => ((IProcessingTrackingInboxStore)inner)
				.MarkProcessingAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowNotSupported_WhenInboxInnerCannotTrackProcessing()
	{
		var inner = A.Fake<IInboxStore>();
		var decorator = CreateInboxDecorator(inner);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.MarkProcessingAsync("msg-1", "TestHandler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ForwardMarkDeadLettered_ToACapableOutboxInner()
	{
		// Under the deny-by-default seam, dead-lettering is discovered through GetService, not off the decorator's
		// own type. IDeadLetterableOutboxStore is payload-free, so it is forwarded RAW: the resolved capability IS
		// the inner's, and the call lands on the inner store.
		var inner = HonestOutboxFake(b => b.Implements<IDeadLetterableOutboxStore>());
		var decorator = CreateOutboxDecorator(inner);

		var deadLetterable = decorator.GetService(typeof(IDeadLetterableOutboxStore)) as IDeadLetterableOutboxStore;
		deadLetterable.ShouldNotBeNull(
			"A capable inner must be discoverable as IDeadLetterableOutboxStore through the encrypting decorator. " +
			"If null, the forwardable-capability path is broken and dead-lettering silently disappears behind " +
			"encryption — the terminal-DeadLettered guarantee is lost with no throw and no log.");

		await deadLetterable.MarkDeadLetteredAsync("msg-1", "retries exhausted", CancellationToken.None);

		A.CallTo(() => ((IDeadLetterableOutboxStore)inner)
				.MarkDeadLetteredAsync("msg-1", "retries exhausted", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ResolveNull_ForDeadLettering_WhenOutboxInnerCannotDeadLetter()
	{
		// INVERTED from ThrowNotSupported_WhenOutboxInnerCannotDeadLetter (recorded, not deleted — see the class
		// header). The decorator no longer declares IDeadLetterableOutboxStore, so there is nothing to throw from.
		// Deny-by-default: an incapable inner resolves to a HONEST null, which routes the consumer to its own
		// documented fallback instead of a runtime exception it never requested.
		var inner = HonestOutboxFake(); // IOutboxStore only — NOT IDeadLetterableOutboxStore
		var decorator = CreateOutboxDecorator(inner);

		decorator.GetService(typeof(IDeadLetterableOutboxStore)).ShouldBeNull(
			"An incapable inner must resolve dead-lettering to null through the decorator. A non-null result is a " +
			"promise the operation will be performed; the encrypting decorator must not make that promise on behalf " +
			"of a store that cannot keep it.");
	}

	[Fact]
	public void ResolveWorkingDeadLettering_OverACapableInner_LivenessForTheNullAssertion()
	{
		// NON-VACUITY for ResolveNull_… above: ShouldBeNull is satisfied by a decorator that answers null to EVERY
		// type. The same capability, over a CAPABLE inner, must resolve non-null — otherwise the negative proves
		// nothing about dead-lettering specifically, only that the decorator resolves nothing at all.
		var inner = HonestOutboxFake(b => b.Implements<IDeadLetterableOutboxStore>());
		var decorator = CreateOutboxDecorator(inner);

		decorator.GetService(typeof(IDeadLetterableOutboxStore)).ShouldNotBeNull(
			"A capable inner must resolve dead-lettering to a non-null capability. If this is null, the ResolveNull " +
			"arm is vacuous: the decorator reports absence for everything, not for a genuinely absent capability.");
	}
}
