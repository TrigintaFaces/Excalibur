// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using FakeItEasy.Creation;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption;

// Decorated-path backoff lock. When field-level encryption is enabled the resolved IOutboxStore is the
// EncryptingOutboxStoreDecorator, and outbox backoff-apply (MarkFailedWithBackoffAsync, carrying the absolute
// NextAttemptAt) must survive the encryption layer.
//
// INVERTED for the l0qpxo deny-by-default seam, recorded not deleted. The original contract was: the decorator
// MUST implement IBackoffSchedulableOutboxStore, forward to a capable inner, and FAIL OPEN to plain MarkFailedAsync
// over a non-capable inner. That was correct while a decorator could declare interfaces unconditionally. The ruled
// seam removes the declaration:
//
//   - IBackoffSchedulableOutboxStore is payload-free, so it is in the decorator's FORWARDABLE set: discovered
//     through GetService, forwarded RAW to a capable inner.
//   - Over a non-capable inner, GetService returns a HONEST null. There is no longer a decorator-level fail-open —
//     the fail-open MOVED to the consumer entitled to choose it (OutboxProcessor.MarkFailedWithBackoffOrFallback).
//     Advertising the capability and degrading inside the decorator hid that choice from the only component
//     qualified to make it.
//
// The three arms keep the SAME capability named and pinned in BOTH directions; only the mechanism (GetService, not
// a cast) and the incapable-inner verdict (null, not a silent MarkFailedAsync) changed, on an explicit ruling.
//
// FIXTURE HONESTY: a bare FakeItEasy fake answers GetService with null even for interfaces it implements, which
// would false-RED the forward arms. The fakes here answer GetService as a real store does. Independent
// engage-test (author≠impl). RED pre-inversion: the decorator no longer implements the capability, so the old
// `(IBackoffSchedulableOutboxStore)decorator` cast throws InvalidCastException.
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingOutboxStoreBackoffForwardingShould
{
	[Fact]
	public void ExposeBackoffThroughGetService_OverACapableInner()
	{
		// KeepAdvertising, inverted: the decorator no longer IS an IBackoffSchedulableOutboxStore (deny-by-default).
		// The property is that a consumer can DISCOVER backoff through GetService when the inner can honor it.
		var decorator = CreateDecorator(HonestFake(b => b.Implements<IBackoffSchedulableOutboxStore>()));

		decorator.ShouldNotBeAssignableTo<IBackoffSchedulableOutboxStore>(
			"Under deny-by-default the decorator must NOT declare the capability interface on its own type — that " +
			"is what makes 'I forgot to wrap the ninth capability' unwritable. Capabilities are discovered through " +
			"GetService, never off the decorator's static type.");

		decorator.GetService(typeof(IBackoffSchedulableOutboxStore)).ShouldNotBeNull(
			"A capable inner must be discoverable as IBackoffSchedulableOutboxStore through the decorator, or " +
			"backoff scheduling silently disappears behind encryption.");
	}

	[Fact]
	public async Task ForwardMarkFailedWithBackoff_ToACapableInner()
	{
		var inner = HonestFake(b => b.Implements<IBackoffSchedulableOutboxStore>());
		var decorator = CreateDecorator(inner);

		var backoff = decorator.GetService(typeof(IBackoffSchedulableOutboxStore)) as IBackoffSchedulableOutboxStore;
		backoff.ShouldNotBeNull("A capable inner must resolve backoff scheduling through the decorator.");

		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
		await backoff.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, CancellationToken.None);

		A.CallTo(() => ((IBackoffSchedulableOutboxStore)inner)
				.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ResolveNullForBackoff_WhenInnerCannotSchedule()
	{
		// FailOpenToMarkFailed, inverted: there is no decorator-level fail-open any more. An incapable inner resolves
		// backoff to a HONEST null; the consumer's own fallback (plain MarkFailedAsync) is what runs. The decorator
		// must not silently substitute a lesser operation on the consumer's behalf.
		var inner = HonestFake(); // IOutboxStore only — NOT IBackoffSchedulableOutboxStore
		var decorator = CreateDecorator(inner);

		decorator.GetService(typeof(IBackoffSchedulableOutboxStore)).ShouldBeNull(
			"An incapable inner must resolve backoff to null. A non-null result is a promise the operation will be " +
			"performed; the honest null routes the consumer to its documented MarkFailedAsync fallback instead.");
	}

	[Fact]
	public void ResolveWorkingBackoff_OverACapableInner_LivenessForTheNullAssertion()
	{
		// NON-VACUITY for ResolveNullForBackoff_…: ShouldBeNull is satisfied by a decorator that answers null to
		// everything. The same capability over a capable inner must resolve non-null, or the negative proves nothing.
		var inner = HonestFake(b => b.Implements<IBackoffSchedulableOutboxStore>());
		var decorator = CreateDecorator(inner);

		decorator.GetService(typeof(IBackoffSchedulableOutboxStore)).ShouldNotBeNull(
			"A capable inner must resolve backoff to a non-null capability, or ResolveNullForBackoff is vacuous.");
	}

	private static EncryptingOutboxStoreDecorator CreateDecorator(IOutboxStore inner)
	{
		var registry = A.Fake<IEncryptionProviderRegistry>();
		var options = Options.Create(new EncryptionOptions());
		return new EncryptingOutboxStoreDecorator(inner, registry, options);
	}

	// A fake whose GetService behaves like a real store's, so raw-forwarded capabilities resolve for a capable inner
	// instead of collapsing to a fixture-induced null (a false RED that looks like diligence).
	private static IOutboxStore HonestFake(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);
		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);
		return fake;
	}
}
