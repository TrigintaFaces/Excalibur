// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.DependencyInjection;

using FakeItEasy;
using FakeItEasy.Creation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.DependencyInjection;

// Independent regression lock (author != implementer, TestsDeveloper) for the safety-critical vmy75v fix.
//
// THE BUG (vmy75v): the outbox fencing fail-fast lived ONLY in the OutboxProcessor constructor. But the DEFAULT
// non-partitioned drain (OutboxBackgroundService -> single MessageBusOutboxPublisher) NEVER constructs an
// OutboxProcessor, so on that path NOTHING checked fencing: leader-election + a store that cannot enforce a
// fencing high-water mark drained UNFENCED, letting a superseded leader claim and complete messages it no
// longer owns (split-brain) — directly contradicting the shipped claim "the processor refuses to start at
// startup rather than draining unfenced".
//
// THE FIX moved the invariant into the startup validator (OutboxPrerequisiteValidator.StartAsync, an
// IHostedService), so it covers EVERY drain path at host start — including the default drain that never builds
// an OutboxProcessor. This lock binds THAT seam: the real DI / IHost.StartAsync() path, NOT a hand-constructed
// OutboxProcessor. The sibling FencingStartupGuardResolvesThroughGetServiceShould binds the CONSTRUCTOR path;
// this file binds the default-drain STARTUP-VALIDATOR path that the constructor guard structurally cannot reach.
//
// SAFETY + LIVENESS (testing-patterns §3): a guard asserted only on its safety half is satisfied by a validator
// that always throws (or that never starts anything). Each safety arm is paired with a liveness arm proving the
// legal composition STILL starts, so the fix cannot be "resolved" by neutering the validator.
//
// NON-VACUITY: the SAFETY arm is RED against committed HEAD BEFORE the fix — pre-fix, StartAsync only checked for
// a missing store and did not call the fencing invariant, so the default-drain leader-election + non-fenced
// composition started silently. Reverting the EnsureFencingCapableStore(...) call in
// OutboxPrerequisiteValidator.StartAsync turns the SAFETY arm RED, confirming it binds the real fix.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxFencingStartupValidatorFailsClosedShould
{
	[Fact]
	public async Task Throw_WhenLeaderElectionActiveAndDefaultStoreCannotFence()
	{
		// SAFETY + THE BUG ARM. Default drain path (no OutboxProcessor is ever constructed): a leader gate is
		// registered and the consumer has NOT opted out (SingleActiveWriter=false), but the "default" store cannot
		// enforce a fencing high-water mark. The host MUST refuse to start rather than drain unfenced.
		// RED against pre-fix HEAD (validator did not enforce fencing); GREEN after the invariant moved into StartAsync.
		await using var provider = BuildProvider(
			store: HonestFake(),                          // NON-fenced store
			leaderGate: A.Fake<ILeaderProcessingGate>(),  // leader election active
			singleActiveWriter: false);                   // consumer did NOT opt out

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"Leader election + a non-fencing default store + no AsSingleWriter() opt-out must FAIL host start. The " +
			"default drain (OutboxBackgroundService -> IOutboxPublisher) never constructs OutboxProcessor, so the " +
			"processor's own guard cannot fire on it; the startup validator is the only thing standing between this " +
			"composition and an unfenced split-brain drain.");

		ex.Message.ShouldContain("does not implement IFencedOutboxStore", Case.Sensitive);
	}

	[Fact]
	public async Task Start_WhenLeaderElectionActiveAndDefaultStoreIsFenced()
	{
		// LIVENESS (a). The permitted composition: leader election + a genuinely FENCED store. A validator that
		// resolved the safety arm by always throwing would fail here. The correct guard starts cleanly.
		await using var provider = BuildProvider(
			store: HonestFake(b => b.Implements<IFencedOutboxStore>()),
			leaderGate: A.Fake<ILeaderProcessingGate>(),
			singleActiveWriter: false);

		await Should.NotThrowAsync(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"Leader election + a fencing-capable default store is the exact deployment fencing exists to support; " +
			"it must start. If this throws, the validator over-rejects and the guard is broken.");
	}

	[Fact]
	public async Task Start_WhenNonFencedStoreButConsumerOptedOutWithAsSingleWriter()
	{
		// LIVENESS (b). AsSingleWriter() is the explicit, observable opt-out: the consumer asserts a genuinely
		// single-active-writer topology and takes responsibility for the guarantee, so a non-fenced store is
		// permitted even under a leader gate. The validator must honor the opt-out and start.
		await using var provider = BuildProvider(
			store: HonestFake(),                          // NON-fenced store
			leaderGate: A.Fake<ILeaderProcessingGate>(),  // leader election active
			singleActiveWriter: true);                    // consumer explicitly opted out via AsSingleWriter()

		await Should.NotThrowAsync(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"AsSingleWriter() is the sanctioned opt-out; a non-fenced store under a leader gate must start when the " +
			"consumer has explicitly asserted single-active-writer ownership, or the guard punishes a legal topology.");
	}

	[Fact]
	public async Task Start_WhenNoLeaderElectionAndNonFencedStore()
	{
		// LIVENESS (c). No leader election is configured, so fencing is not required at all — a plain non-fenced
		// store is a perfectly valid single-instance deployment and must start. This proves the guard keys on the
		// leader gate, not merely on the absence of a fencing capability.
		await using var provider = BuildProvider(
			store: HonestFake(),   // NON-fenced store
			leaderGate: null,      // no leader election
			singleActiveWriter: false);

		await Should.NotThrowAsync(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"With no leader election, fencing is not required; a plain store must start. If this throws, the guard " +
			"fires without an active leader gate and would reject every single-instance non-fenced deployment.");
	}

	// Resolves the REAL OutboxPrerequisiteValidator (an IHostedService) the host would run at IHost.StartAsync().
	private static OutboxPrerequisiteValidator Validator(IServiceProvider provider) =>
		provider.GetServices<IHostedService>().OfType<OutboxPrerequisiteValidator>().Single();

	// Builds the REAL DI graph exactly as the host would. No hand-constructed OutboxProcessor — the whole point of
	// vmy75v is that the default drain path never has one, so the lock must exercise the validator the host runs.
	// The provider is returned (not disposed here) so StartAsync can resolve from it; each arm owns its lifetime.
	private static ServiceProvider BuildProvider(
		IOutboxStore store,
		ILeaderProcessingGate? leaderGate,
		bool singleActiveWriter)
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x =>
			x.AddOutbox(o =>
			{
				if (singleActiveWriter)
				{
					// The real, observable opt-out — IOutboxBuilder.AsSingleWriter() sets
					// OutboxDeliveryOptions.SingleActiveWriter = true, which the validator reads through IOptions.
					_ = o.AsSingleWriter();
				}
			}));

		// The consumer's provider extension registers the concrete store as keyed "default"; mirror that.
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => store);

		if (leaderGate is not null)
		{
			// The validator resolves ILeaderProcessingGate via non-keyed GetService (as a leader-election package
			// registers it). Present => fencing is active unless the consumer opted out.
			services.AddSingleton(leaderGate);
		}

		return services.BuildServiceProvider(validateScopes: false);
	}

	// FIXTURE HONESTY (shared l0qpxo seam). A bare FakeItEasy fake answers GetService(Type) with a non-null dummy,
	// which would masquerade as a fencing capability and defeat the SAFETY arm (the invariant probes
	// store.GetService(typeof(IFencedOutboxStore)) is null). A real store returns itself for a capability it
	// implements and null otherwise; the fake must too.
	private static IOutboxStore HonestFake(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);
		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);
		return fake;
	}
}
