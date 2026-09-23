// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		new(inner, A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()), global::Excalibur.Dispatch.UntenantedContext.Instance);

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
	public async Task ForwardClaimScopedMarkFailed_ToACapableOutboxInner()
	{
		// The claim-scoped capability is payload-free -- identifiers, an error string, a retry count, a
		// next-attempt time and a claim identity -- so it forwards RAW like dead-lettering. Without this arm
		// the capability is declared by the store, listed as forwardable, and asserted by nothing: a consumer
		// who registered crypto-shredding would probe it, get null, and silently fall back to the unscoped
		// completion, which is the defect the capability exists to close.
		var inner = HonestOutboxFake(b => b.Implements<IClaimScopedOutboxStore>());
		var decorator = CreateOutboxDecorator(inner);

		var claimScoped = decorator.GetService(typeof(IClaimScopedOutboxStore)) as IClaimScopedOutboxStore;
		claimScoped.ShouldNotBeNull(
			"A capable inner must be discoverable as IClaimScopedOutboxStore through the encrypting decorator. " +
			"If null, a superseded claim's failure report is written unscoped and the per-claim guarantee is " +
			"lost behind encryption, with no throw and no log.");

		await claimScoped.MarkFailedAsync("msg-1", "boom", 2, "claim-a", CancellationToken.None);

		A.CallTo(() => ((IClaimScopedOutboxStore)inner)
				.MarkFailedAsync("msg-1", "boom", 2, "claim-a", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// The FENCED dead-letter route must survive the decorator. SAFETY.
	/// </summary>
	/// <remarks>
	/// <b>The bare-fixture arm that locks this route is structurally blind to the forwarding half</b> — it
	/// implements the capability directly with no decorator present, so it passes whether or not the
	/// decorator forwards. Without this arm, a decorated store advertises the capability at compile time and
	/// answers absent at runtime, which is the exact failure the interface's own remarks describe: a cast
	/// sees only the outermost type and is lossy through any decorator.
	/// <para>
	/// The consequence is specific and irreversible. The drain picks the fenced TERMINAL route from this
	/// probe. Denied, it falls back to the unfenced one, and a superseded tenure's dead-letter decision is
	/// applied with no leadership check — for precisely the consumers who enabled crypto-shredding.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ForwardFencedDeadLettering_ToACapableOutboxInner()
	{
		var inner = HonestOutboxFake(b => b.Implements<IFencedDeadLetterableOutboxStore>());
		var decorator = CreateOutboxDecorator(inner);

		var fenced = decorator.GetService(typeof(IFencedDeadLetterableOutboxStore))
			as IFencedDeadLetterableOutboxStore;

		fenced.ShouldNotBeNull(
			"A capable inner must be discoverable as IFencedDeadLetterableOutboxStore through the encrypting "
			+ "decorator. If null, the drain takes the UNFENCED terminal route and a superseded tenure's "
			+ "dead-letter decision is applied with no leadership check.");

		_ = await fenced.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7L, CancellationToken.None);

		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)inner)
				.MarkDeadLetteredAsync("msg-1", "retries exhausted", 7L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// An inner that cannot honour the fenced terminal route must NOT appear to. SAFETY.
	/// </summary>
	[Fact]
	public void NotAdvertiseFencedDeadLettering_OverAnInnerThatLacksIt()
	{
		var inner = HonestOutboxFake(); // IOutboxStore only

		CreateOutboxDecorator(inner).GetService(typeof(IFencedDeadLetterableOutboxStore)).ShouldBeNull(
			"An inner without the fenced terminal capability must resolve to null through the decorator. "
			+ "Advertising it would route an irreversible transition into a store that cannot fence it.");
	}

	/// <summary>
	/// A delete-on-sent store must still say so through the decorator. SAFETY.
	/// </summary>
	/// <remarks>
	/// This capability is unlike the others in the file: it does not move data, it REPORTS ON THE STORE'S
	/// OWN SHAPE. Denied, it read as absent, and a delete-on-sent store became indistinguishable from a
	/// sent-tracking one for every consumer who enabled crypto-shredding — so callers and the conformance
	/// kit went looking for a row that store had already deleted. A decorator answering a question about
	/// the store underneath is not merely advertising what it cannot honour; it is giving a wrong answer
	/// about someone else.
	/// </remarks>
	[Fact]
	public void ForwardTheStoreShapeCapability_ToACapableOutboxInner()
	{
		var inner = HonestOutboxFake(b => b.Implements<IOutboxStoreCapabilities>());
		A.CallTo(() => ((IOutboxStoreCapabilities)inner).SupportsSentTracking).Returns(false);

		var decorator = CreateOutboxDecorator(inner);

		var capabilities = decorator.GetService(typeof(IOutboxStoreCapabilities)) as IOutboxStoreCapabilities;
		capabilities.ShouldNotBeNull(
			"A capable inner must be discoverable as IOutboxStoreCapabilities through the encrypting "
			+ "decorator. If null, a delete-on-sent store reports as a sent-tracking one and every caller "
			+ "looks for a row it deleted.");

		capabilities.SupportsSentTracking.ShouldBeFalse(
			"The decorator must report the INNER store's shape, not a default. Forwarding the capability and "
			+ "then answering it differently would be worse than denying it.");
	}

	/// <summary>
	/// An inner store that does NOT report its shape must resolve to null, not to a decorator-supplied
	/// default. LIVENESS' twin: the absence must survive too.
	/// </summary>
	[Fact]
	public void NotAdvertiseTheStoreShapeCapability_OverAnInnerThatLacksIt()
	{
		var inner = HonestOutboxFake(); // IOutboxStore only

		CreateOutboxDecorator(inner).GetService(typeof(IOutboxStoreCapabilities)).ShouldBeNull(
			"An inner that makes no claim about sent-tracking must resolve to null through the decorator "
			+ "rather than to a view that answers for it.");
	}

	[Fact]
	public async Task ForwardFencedClaimScopedMarkFailed_ToACapableOutboxInner()
	{
		// SAFETY. The FENCED claim-scoped capability must survive the confidentiality boundary too, and this
		// arm exists because it very nearly did not. The obvious spelling of that contract derives it from
		// the fencing contract to make the product explicit to the type system -- and the fencing contract
		// derives from IOutboxStore, whose surface carries message payloads. A capability that drags a
		// payload-bearing surface behind it CANNOT be forwarded raw, so it would have been denied here: the
		// probe returns null, the drain falls back to the completion that carries no fence, and the
		// guarantee is absent for exactly the consumers who enabled crypto-shredding. No throw, no log.
		//
		// The contract therefore declares NO base interfaces and carries the product on its member instead.
		//
		// WHAT THIS ARM ACTUALLY BINDS, measured rather than assumed: membership of the decorator's
		// forwardable set. Removing this capability from that set turns this arm RED and leaves its
		// liveness twin green, which is the discrimination we want. It does NOT catch the base list being
		// re-added -- that was tested and stayed green, because the set is keyed on the explicit type and
		// not on what the type inherits. The no-base decision is held by the reasoning above and by the
		// contract's own remarks; it is not held by this test, and saying otherwise would be the kind of
		// claim that reads as coverage and is not.
		var inner = HonestOutboxFake(b => b.Implements<IFencedClaimScopedOutboxStore>());
		var decorator = CreateOutboxDecorator(inner);

		var fencedScoped =
			decorator.GetService(typeof(IFencedClaimScopedOutboxStore)) as IFencedClaimScopedOutboxStore;
		fencedScoped.ShouldNotBeNull(
			"A capable inner must be discoverable as IFencedClaimScopedOutboxStore through the encrypting " +
			"decorator. If null, a superseded tenure's failure report is written with no fence term behind " +
			"encryption, which is the exact write this capability exists to refuse.");

		var authority = new OutboxWriteAuthority(7, "claim-a");
		_ = await fencedScoped.MarkFailedAsync("msg-1", "boom", 2, null, authority, CancellationToken.None);

		A.CallTo(() => ((IFencedClaimScopedOutboxStore)inner)
				.MarkFailedAsync("msg-1", "boom", 2, null, authority, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ResolveNull_ForFencedClaimScopedMarkFailed_WhenOutboxInnerIsNotFencedClaimScoped()
	{
		// LIVENESS' twin: forwarding must not manufacture the capability. A non-null result here would
		// promise a fenced, claim-scoped completion that the store beneath cannot perform -- and the drain
		// chooses its route from this probe, so a manufactured yes routes a fenced completion into nothing.
		var inner = HonestOutboxFake(); // IOutboxStore only

		CreateOutboxDecorator(inner).GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeNull(
			"An inner without the combined capability must resolve to null through the decorator rather " +
			"than a view that cannot honour the contract.");
	}

	[Fact]
	public void ResolveNull_ForClaimScopedMarkFailed_WhenOutboxInnerIsNotClaimScoped()
	{
		// The liveness arm's twin: forwarding must not manufacture a capability the inner lacks. A non-null
		// result here would promise per-claim scoping that the underlying store cannot perform.
		var inner = HonestOutboxFake(); // IOutboxStore only

		CreateOutboxDecorator(inner).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeNull(
			"An inner without the claim-scoped capability must resolve to null through the decorator rather " +
			"than a view that cannot honour the contract.");
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
