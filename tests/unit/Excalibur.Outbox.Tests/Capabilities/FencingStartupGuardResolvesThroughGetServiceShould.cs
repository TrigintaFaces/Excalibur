// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox.Diagnostics;
using Excalibur.Outbox.Processing;

using FakeItEasy.Creation;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.Capabilities;

// Independent regression lock (author != implementer) for REVIEW_CODE BLOCKING B2.
//
// The l0qpxo commit (6f0c92875) migrated the outbox fencing capability to the GetService seam because "a cast
// sees only the outermost decorator and would silently report the capability absent." It did that at the DRAIN
// path (FencedStore, OutboxProcessor:92) but LEFT the constructor STARTUP GUARD (OutboxProcessor:258) reading
// the lossy cast:
//
//     if (leaderGate is not null && outboxStore is not IFencedOutboxStore) throw ...   // WRONG
//
// A fencing-capable store wrapped in ANY decorator (EncryptingOutboxStoreDecorator / TelemetryOutboxStore
// Decorator — which expose fencing via GetService/WrapCapability, NOT their interface list) makes
// `outboxStore is IFencedOutboxStore` false, so leader-election + a decorated fenced store THROWS AT STARTUP —
// rejecting the exact decorated deployment the capability seam was built for, even though the drain path
// (:99/:114 via FencedStore) would resolve it correctly. The fix is the one-liner the sibling drain path
// already uses: `outboxStore.GetService(typeof(IFencedOutboxStore)) is null`.
//
// The sibling lock ConsumersDiscoverCapabilitiesThroughGetServiceShould binds the DRAIN-path property
// (FencedStore resolves through a decorator) but constructs the processor with NO leaderGate, so it never
// exercises the :258 startup guard. This file binds the guard.
//
// SAFETY + LIVENESS (testing-patterns §3):
//   REGRESSION/LIVENESS — leader-election + a FENCED store behind a decorator must CONSTRUCT (no throw). This is
//     the arm the bug fails: it is RED against the `is` cast at :258, GREEN once the guard resolves via
//     GetService. Without it, a correct decorated fencing deployment cannot start.
//   SAFETY — leader-election + a genuinely UNFENCED store must STILL THROW. A fix that resolved the liveness arm
//     by neutering the guard (never throwing) would reopen the split-brain hole the guard exists to close. This
//     arm keeps the fix honest: the guard must still fire when the store truly cannot fence.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class FencingStartupGuardResolvesThroughGetServiceShould
{
	[Fact]
	public async Task NotThrowAtStartup_WhenLeaderElectionWrapsAFencedStoreInADecorator()
	{
		// REGRESSION / LIVENESS — RED against the :258 `is` cast (throws), GREEN once it resolves via GetService.
		var fencedInner = HonestFake(b => b.Implements<IFencedOutboxStore>());
		var decorated = new TelemetryOutboxStoreDecorator(fencedInner);

		var construct = () => CreateProcessor(decorated, leaderGate: A.Fake<ILeaderProcessingGate>());

		var processor = construct.ShouldNotThrow(
			"Leader election + a FENCED store behind a decorator threw at startup. The constructor guard " +
			"(OutboxProcessor:258) resolved the fencing capability by casting (`outboxStore is not " +
			"IFencedOutboxStore`), which is null through any decorator — so it rejects the exact decorated fencing " +
			"deployment the capability seam was built for, though the drain path (:99/:114 via FencedStore) resolves " +
			"it correctly. The guard must resolve via GetService, like the drain path it guards.");

		await processor.DisposeAsync();
	}

	[Fact]
	public void ThrowAtStartup_WhenLeaderElectionWrapsATrulyUnfencedStore()
	{
		// SAFETY, and the non-vacuity partner of the liveness arm. If a fix resolved the liveness arm by making the
		// guard never throw, this arm fails — the guard must still fire when the store genuinely cannot fence, or a
		// superseded leader could claim and complete messages it no longer owns (the split-brain hole).
		var plainInner = HonestFake();
		var decorated = new TelemetryOutboxStoreDecorator(plainInner);

		Should.Throw<InvalidOperationException>(
			() => CreateProcessor(decorated, leaderGate: A.Fake<ILeaderProcessingGate>()),
			"Leader election + a genuinely UNFENCED store (fencing absent on the inner store, so absent through the " +
			"decorator via GetService) must fail closed at startup. A store that cannot enforce a fencing high-water " +
			"mark under an active leader gate is the 'looks fenced but isn't' split-brain window the guard closes.");
	}

	[Fact]
	public async Task NotThrowAtStartup_WhenLeaderElectionUsesABareFencedStore()
	{
		// NON-VACUITY baseline. A bare fenced store (no decorator) + an active gate must construct both before AND
		// after the fix — proving the guard does not simply always-throw and that the liveness arm's difference is
		// caused by decoration, not by the presence of a gate.
		var fenced = HonestFake(b => b.Implements<IFencedOutboxStore>());

		var construct = () => CreateProcessor(fenced, leaderGate: A.Fake<ILeaderProcessingGate>());

		var processor = construct.ShouldNotThrow(
			"A bare fenced store (no decorator) + an active leader gate threw at startup. The guard should only " +
			"reject stores that cannot fence; if this throws, the guard is broken independently of the decorator bug.");

		await processor.DisposeAsync();
	}

	private static OutboxProcessor CreateProcessor(IOutboxStore outboxStore, ILeaderProcessingGate leaderGate)
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
			circuitBreakerRegistry: null,
			backoffCalculator: null,
			deliveryGuaranteeOptions: null,
			leaderGate: leaderGate);
	}

	// FIXTURE HONESTY (l0qpxo seam). A bare FakeItEasy fake answers GetService(object-returning) with a non-null
	// dummy, not null, and the base decorator forwards unknown capabilities straight to Inner.GetService — so a
	// dummy would leak through as a phantom fencing capability and defeat the SAFETY arm. A real store returns
	// itself for a capability it implements and null otherwise; the fake must too.
	private static IOutboxStore HonestFake(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);
		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);
		return fake;
	}
}
