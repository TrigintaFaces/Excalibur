// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// <see cref="ExactlyOnceOptions.RequireTransactionalExactlyOnce"/> has no consumer downstream of this
/// validator, by design: its whole contract is to turn a silent degrade into a startup refusal. That
/// makes the validator the only place the value can be observed, so this is where it has to be locked.
/// <para>
/// The three arms discriminate. Only the middle one may fail: a store that cannot claim and handle in
/// one transaction is accepted while the flag is clear (the documented at-least-once boundary) and
/// refused once the flag is set, and a store that can is accepted either way.
/// </para>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TransactionalExactlyOnceCapabilityValidatorShould
{
	[Fact]
	public void AcceptANonTransactionalStoreWhenTheFlagIsClear()
	{
		var result = Validate(NonTransactionalStore(), requireTransactional: false);

		result.Failed.ShouldBeFalse();
	}

	[Fact]
	public void RefuseANonTransactionalStoreWhenTheFlagIsSet()
	{
		var result = Validate(NonTransactionalStore(), requireTransactional: true);

		result.Failed.ShouldBeTrue();

		// The refusal has to be actionable: it names the store that fell short, the capability it
		// lacks, and both ways out.
		result.FailureMessage.ShouldNotBeNull();
		result.FailureMessage.ShouldContain(nameof(ITransactionalInboxStore));
		result.FailureMessage.ShouldContain(nameof(ExactlyOnceOptions.RequireTransactionalExactlyOnce));
	}

	[Fact]
	public void AcceptATransactionalStoreWhenTheFlagIsSet()
	{
		var result = Validate(TransactionalStore(), requireTransactional: true);

		result.Failed.ShouldBeFalse();
	}

	/// <summary>
	/// A store may declare <see cref="ITransactionalInboxStore"/> statically and wrap a non-transactional
	/// inner. The effective capability is what counts, so a declared-but-not-capable store is refused.
	/// </summary>
	[Fact]
	public void RefuseAStoreThatDeclaresTheInterfaceButReportsNoCapability()
	{
		var store = A.Fake<IInboxStore>(o => o.Implements<ITransactionalInboxStore>().Implements<IInboxStoreCapabilities>());
		A.CallTo(() => ((IInboxStoreCapabilities)store).SupportsTransactional).Returns(false);

		var result = Validate(store, requireTransactional: true);

		result.Failed.ShouldBeTrue();
	}

	private static IInboxStore NonTransactionalStore() => A.Fake<IInboxStore>();

	private static IInboxStore TransactionalStore() =>
		A.Fake<IInboxStore>(o => o.Implements<ITransactionalInboxStore>());

	private static ValidateOptionsResult Validate(IInboxStore store, bool requireTransactional) =>
		new TransactionalExactlyOnceCapabilityValidator(store).Validate(
			Microsoft.Extensions.Options.Options.DefaultName,
			new ExactlyOnceOptions { RequireTransactionalExactlyOnce = requireTransactional });
}
