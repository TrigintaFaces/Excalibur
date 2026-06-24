// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

// bd-dziy0x + bd-stlcgg (S841, ADR-336 clause 2) — anti-silent-absence presence guards. The new durable
// capabilities (IProcessingTrackingInboxStore / IDeadLetterableOutboxStore) are segregated interfaces; if a
// custom store omits one while the guard/terminal behavior is expected, the framework would silently degrade
// (dead at-most-once guard / re-claimed dead-letters). These ValidateOnStart guards make that inexpressible:
// a registered store lacking the capability fails fast at startup. Independent engage-test (author≠impl): the
// guard FAILS for a non-capable store and SUCCEEDS for a capable one. RED if either guard rubber-stamps a
// non-capable store (the silent-degrade the guard exists to prevent).
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CapabilityPresenceGuardsShould
{
	// --- stlcgg: OutboxDeadLetterCapabilityValidator ---

	[Fact]
	public void FailFast_WhenOutboxStoreCannotDeadLetter()
	{
		var store = A.Fake<IOutboxStore>(); // implements IOutboxStore only — NOT IDeadLetterableOutboxStore
		var validator = new OutboxDeadLetterCapabilityValidator(store);

		var result = validator.Validate(name: null, new OutboxDeliveryOptions());

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldNotBeNull();
		result.FailureMessage.ShouldContain(nameof(IDeadLetterableOutboxStore));
	}

	[Fact]
	public void Succeed_WhenOutboxStoreCanDeadLetter()
	{
		var store = A.Fake<IOutboxStore>(b => b.Implements<IDeadLetterableOutboxStore>());
		var validator = new OutboxDeadLetterCapabilityValidator(store);

		var result = validator.Validate(null, new OutboxDeliveryOptions());

		result.Succeeded.ShouldBeTrue();
	}

	// --- dziy0x: InboxProcessingCapabilityValidator ---

	[Fact]
	public void FailFast_WhenInboxStoreCannotTrackProcessing()
	{
		var store = A.Fake<IInboxStore>(); // implements IInboxStore only — NOT IProcessingTrackingInboxStore
		var validator = new InboxProcessingCapabilityValidator(store);

		var result = validator.Validate(null, new InboxOptions());

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldNotBeNull();
		result.FailureMessage.ShouldContain(nameof(IProcessingTrackingInboxStore));
	}

	[Fact]
	public void Succeed_WhenInboxStoreCanTrackProcessing()
	{
		var store = A.Fake<IInboxStore>(b => b.Implements<IProcessingTrackingInboxStore>());
		var validator = new InboxProcessingCapabilityValidator(store);

		var result = validator.Validate(null, new InboxOptions());

		result.Succeeded.ShouldBeTrue();
	}
}
