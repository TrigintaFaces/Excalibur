// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox.Diagnostics;

using FakeItEasy.Creation;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.Capabilities;

// The CONSUMER side of the l0qpxo capability seam. The sibling lock EveryOutboxDecoratorPreservesCapabilities
// proves the DECORATOR preserves fencing; it says nothing about whether the consumer that reads the store
// actually discovers it. Its own header called that gap out: "OutboxProcessor:90 still reads
// _outboxStore as IFencedOutboxStore, which is null through any decorator, so fencing is silently disabled on
// every decorated deployment even when every assertion there is green."
//
// That consumer cast was migrated to _outboxStore.GetService(typeof(IFencedOutboxStore)) (OutboxProcessor:92).
// This file binds the migration so it cannot regress to a cast: it constructs the real OutboxProcessor over a
// decorated store and reads its private fencing resolution.
//
// SAFETY vs LIVENESS (testing-patterns §3), and the split matters here more than usual:
//
//   LIVENESS — a FENCED store behind a decorator must be SEEN as fenced by the processor. This is the arm that
//     catches the split-brain bug: if the processor resolved via `as`, the decorator (which declares no
//     capability interface) answers null, the processor falls through to the UNFENCED claim path, and leadership
//     fencing silently disables itself on every decorated deployment — no throw, no log, no failing test. A
//     processor that resolved null for everything would satisfy the safety arm perfectly; only this arm fails.
//
//   SAFETY — an UNFENCED store behind a decorator must NOT be seen as fenced, or the processor would present a
//     leadership token to a store that cannot enforce one.
//
// FencedStore is a private property; reflection is the seam (testing-patterns: reflection over the public API
// when the property under test is internal to the type). Independent engage-test (author != implementer).
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class ConsumersDiscoverCapabilitiesThroughGetServiceShould
{
	private static readonly PropertyInfo FencedStoreProperty =
		typeof(OutboxProcessor).GetProperty("FencedStore", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"OutboxProcessor.FencedStore was not found by reflection. If it was renamed or made public, update this " +
			"lock — it exists to prove the consumer resolves fencing through the capability seam, not by casting.");

	[Fact]
	public async Task SeeAFencedStore_AsFenced_EvenBehindADecorator()
	{
		// LIVENESS — the arm that catches the split-brain regression.
		var fencedInner = HonestFake(b => b.Implements<IFencedOutboxStore>());
		await using var processor = CreateProcessor(new TelemetryOutboxStoreDecorator(fencedInner));

		ResolvedFencedStore(processor).ShouldNotBeNull(
			"A fenced inner store behind a decorator was NOT discovered as fenced by OutboxProcessor. That means the " +
			"processor resolved the capability by casting (`_outboxStore as IFencedOutboxStore`), which is null " +
			"through any decorator — so it falls through to the UNFENCED claim path and leadership fencing silently " +
			"disables itself on every decorated deployment. The consumer must resolve via GetService, which walks " +
			"the decorator to the inner store.");
	}

	[Fact]
	public async Task NotSeeAnUnfencedStore_AsFenced_BehindADecorator()
	{
		// SAFETY, and the non-vacuity partner of the liveness arm above. If the liveness arm passed only because the
		// processor reports fenced for EVERYTHING, this arm fails — so the pair together prove the resolution
		// mirrors the inner store rather than answering a constant.
		var plainInner = HonestFake();
		await using var processor = CreateProcessor(new TelemetryOutboxStoreDecorator(plainInner));

		ResolvedFencedStore(processor).ShouldBeNull(
			"An unfenced inner store behind a decorator was seen as fenced by OutboxProcessor. The processor would " +
			"then present a leadership token to a store that cannot enforce a fencing high-water mark. The capability " +
			"must mirror the inner store: absent on the inner ⇒ absent to the consumer.");
	}

	[Fact]
	public async Task SeeAFencedStore_AsFenced_WhenUndecorated()
	{
		// NON-VACUITY for the reflection seam itself. If FencedStoreProperty read the wrong member or the resolution
		// never works, the liveness arm could pass or fail for the wrong reason. A bare fenced store (no decorator)
		// must resolve non-null, proving the property and the GetService path are live before decoration is added.
		var fenced = HonestFake(b => b.Implements<IFencedOutboxStore>());
		await using var processor = CreateProcessor(fenced);

		ResolvedFencedStore(processor).ShouldNotBeNull(
			"A bare fenced store (no decorator) did not resolve as fenced. The reflection seam or the GetService " +
			"resolution is broken, and the decorated-path arms above prove nothing.");
	}

	private static IFencedOutboxStore? ResolvedFencedStore(OutboxProcessor processor) =>
		(IFencedOutboxStore?)FencedStoreProperty.GetValue(processor);

	private static OutboxProcessor CreateProcessor(IOutboxStore outboxStore)
	{
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 3,
			EnableBatchDatabaseOperations = true,
		});

		return new OutboxProcessor(
			options,
			outboxStore,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null,
			deadLetterQueue: null,
			circuitBreakerRegistry: null);
	}

	// FIXTURE HONESTY. A bare FakeItEasy fake answers GetService(object-returning) with a non-null dummy, not null,
	// and the base decorator forwards unknown capabilities straight to Inner.GetService — so a dummy would leak
	// through as a phantom fencing capability. A real store returns itself for a capability it implements; the fake
	// must too, or these arms are worthless.
	private static IOutboxStore HonestFake(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);
		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);
		return fake;
	}
}
