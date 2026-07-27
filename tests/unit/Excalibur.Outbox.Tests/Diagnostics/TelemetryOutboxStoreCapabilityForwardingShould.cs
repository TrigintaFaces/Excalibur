// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

using FakeItEasy.Creation;

namespace Excalibur.Outbox.Tests.Diagnostics;

// Independent regression lock (author != implementer): I did not write the decorator and I am not writing the fix.
//
// TelemetryOutboxStoreDecorator wraps the SqlServer store UNCONDITIONALLY (OutboxBuilderSqlServerExtensions:150,
// keyed "sqlserver"; "default" resolves to it). Consumers discover optional store capabilities by probing the
// instance they were handed — `outboxStore as IOutboxStoreAdmin` (MessageBusOutboxPublisher:67-69). The decorator
// declares IOutboxStoreBatch, IDeadLetterableOutboxStore and IBackoffSchedulableOutboxStore, so those probes
// survive. It declares NONE of IOutboxStoreAdmin / IMultiTransportOutboxStore / IMultiTransportOutboxStoreAdmin,
// so those probes return null even when the inner store implements all three (SqlServerOutboxStore does).
//
// The damage is silent, not loud: MessageBusOutboxPublisher:206,227 converts the null probe into
// `PublishingResult.Success(0, 0, TimeSpan.Zero)`. Scheduled messages are never dispatched and failed messages
// are never retried, and the caller is told it succeeded.
//
// A sibling lock (TelemetryOutboxStoreBackoffForwardingShould, S849) already binds exactly this property for the
// backoff capability, for exactly this reason. It was never generalized. The capability nobody remembered to lock
// is the capability that shipped stripped — which is the whole thesis of the conformance-instrument work.
//
// WHY BOTH ARMS. "The decorator exposes IOutboxStoreAdmin" is satisfiable by simply adding the interface to the
// class declaration. That would be a lie: for an inner store that is not admin-capable, every admin call would
// then throw or no-op, and consumers would stop seeing the honest `null` that tells them the capability is absent.
// The paired arm — a non-capable inner must NOT advertise the capability — forbids that fix and forces a
// capability-aware wrapper. Neither arm alone constrains anything useful.
//
// MECHANISM NOTE, and the seam it warned about ARRIVED. These arms used to probe with `as`/`is` because that is
// how production probed. The note said: "If the capability seam is later replaced, these arms MUST be rewritten
// against the new seam rather than deleted. The property is 'a consumer can discover a capability the inner store
// really has, through the decorator' — not 'the decorator implements this specific interface'."
//
// The seam is now `IOutboxStore : IServiceProvider`. A decorator declares no capability interfaces and answers
// `GetService` by deferring to the store it wraps, so `decorator as IOutboxStoreAdmin` no longer compiles —
// correctly, because a sealed decorator can never declare the 2^N interface combinations its inner store might
// have. The arms below are rewritten against the new seam, exactly as instructed. Every assertion still binds the
// PROPERTY. Not one of them was deleted, and not one verdict was relaxed to make the file compile.
//
// FIXTURE NOTE, and it is the difference between a real RED and a fake one. A bare FakeItEasy fake answers
// `GetService` with null for EVERY type, including interfaces it demonstrably implements. Probing such a fake
// through the seam reports zero capabilities and every arm below would go RED blaming the decorator for a defect
// in this file. `FakeInnerStore` therefore makes the fake answer the way `OutboxStoreDecorator` documents a real
// store must: return yourself for what you implement, null otherwise. A false RED is exactly as dishonest as a
// false GREEN, and it is the easier of the two to publish because it looks like diligence.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class TelemetryOutboxStoreCapabilityForwardingShould
{
	/// <summary>
	/// Can a consumer DISCOVER <paramref name="capability"/> on <paramref name="store"/>? The property, not the
	/// mechanism: either a direct declaration or a <c>GetService</c> answer satisfies a consumer.
	/// </summary>
	private static object? Discover(IOutboxStore store, Type capability) =>
		capability.IsInstanceOfType(store) ? store : store.GetService(capability);

	/// <summary>A fake inner store that answers <c>GetService</c> the way a real store does.</summary>
	private static IOutboxStore FakeInnerStore(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);

		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);

		return fake;
	}

	[Fact]
	public void ExposeTheAdminCapability_WhenTheInnerStoreIsAdminCapable()
	{
		var inner = FakeInnerStore(b => b.Implements<IOutboxStoreAdmin>());

		var decorator = new TelemetryOutboxStoreDecorator(inner);

		// SAFETY. MessageBusOutboxPublisher probes exactly this; when it gets null it reports
		// PublishingResult.Success(0) forever and scheduled messages are never dispatched.
		Discover(decorator, typeof(IOutboxStoreAdmin)).ShouldNotBeNull(
			"The telemetry decorator wraps the SqlServer store unconditionally. A consumer probing the resolved " +
			"IOutboxStore cannot discover an admin capability the inner store genuinely has, so " +
			"PublishScheduledMessagesAsync returns Success(0) without dispatching anything.");
	}

	[Fact]
	public void NotAdvertiseTheAdminCapability_WhenTheInnerStoreIsNotAdminCapable()
	{
		// Inner implements IOutboxStore only.
		var inner = FakeInnerStore();

		var decorator = new TelemetryOutboxStoreDecorator(inner);

		// Liveness half, and the reason the fix cannot be "add the interface to the class declaration".
		// An honest null is information: it tells the consumer the capability is genuinely absent.
		Discover(decorator, typeof(IOutboxStoreAdmin)).ShouldBeNull(
			"The decorator must not advertise a capability its inner store does not have. Declaring " +
			"IOutboxStoreAdmin unconditionally would make every admin call against a non-capable store throw " +
			"or silently no-op, which is strictly worse than the honest null the consumer sees today.");
	}

	[Fact]
	public void ExposeTheMultiTransportCapability_WhenTheInnerStoreIsMultiTransportCapable()
	{
		var inner = FakeInnerStore(b => b.Implements<IMultiTransportOutboxStore>());

		var decorator = new TelemetryOutboxStoreDecorator(inner);

		// SqlServerOutboxStore implements IMultiTransportOutboxStore; the DI extension used to work around the
		// strip by registering the RAW store for that interface — which never helped MessageBusOutboxPublisher,
		// because it probes the instance it was handed, not the container.
		Discover(decorator, typeof(IMultiTransportOutboxStore)).ShouldNotBeNull(
			"Multi-transport dispatch is invisible to any consumer that probes the decorated store.");
	}

	[Fact]
	public void NotAdvertiseTheMultiTransportCapability_WhenTheInnerStoreIsNotMultiTransportCapable()
	{
		var inner = FakeInnerStore();

		var decorator = new TelemetryOutboxStoreDecorator(inner);

		Discover(decorator, typeof(IMultiTransportOutboxStore)).ShouldBeNull(
			"A decorator that advertises multi-transport over a single-transport store would route messages " +
			"to a capability that cannot honor them.");
	}

	[Fact]
	public void PreserveTheCapabilitiesItAlreadyForwards()
	{
		// Non-vacuity floor: capabilities that already survived decoration must keep surviving it. If this arm
		// ever goes RED, the fix regressed capabilities that already worked.
		var inner = FakeInnerStore(b => b
			.Implements<IOutboxStoreBatch>()
			.Implements<IDeadLetterableOutboxStore>()
			.Implements<IBackoffSchedulableOutboxStore>());

		var decorator = new TelemetryOutboxStoreDecorator(inner);

		Discover(decorator, typeof(IOutboxStoreBatch)).ShouldNotBeNull();
		Discover(decorator, typeof(IDeadLetterableOutboxStore)).ShouldNotBeNull();
		Discover(decorator, typeof(IBackoffSchedulableOutboxStore)).ShouldNotBeNull();
	}

	[Fact]
	public async Task ForwardScheduledMessageReads_ToAnAdminCapableInner()
	{
		var inner = FakeInnerStore(b => b.Implements<IOutboxStoreAdmin>());
		var decorator = new TelemetryOutboxStoreDecorator(inner);

		var admin = Discover(decorator, typeof(IOutboxStoreAdmin)) as IOutboxStoreAdmin;
		admin.ShouldNotBeNull("Capability must be discoverable before it can be forwarded.");

		var asOf = DateTimeOffset.UtcNow;
		_ = await admin.GetScheduledMessagesAsync(asOf, 100, CancellationToken.None);

		// Discoverability is not forwarding. A decorator that answers the probe and then answers the CALL from
		// nowhere is the same silent no-op wearing a different costume. The seam changed; this arm did not.
		A.CallTo(() => ((IOutboxStoreAdmin)inner).GetScheduledMessagesAsync(asOf, 100, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
