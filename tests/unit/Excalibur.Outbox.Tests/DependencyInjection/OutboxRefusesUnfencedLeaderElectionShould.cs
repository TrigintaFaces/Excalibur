// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Outbox.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.DependencyInjection;

/// <summary>
/// Regression lock for the outbox fencing guard's <b>enabling predicate</b>.
/// </summary>
/// <remarks>
/// <para>
/// THE BUG. The fencing invariant keyed on the presence of the <b>gate</b> - the very component the guard
/// exists to require. A guard whose enabling condition is supplied by the thing it guards cannot detect the
/// one case it exists to detect. A host that registered a leader election through a path that never wired
/// the gate resolved no gate, so the predicate read "single instance", startup passed <b>silently</b>, and
/// every instance drained the outbox unfenced - on a deployment whose operator registered an election
/// precisely to prevent that.
/// </para>
/// <para>
/// THE FIX makes the <b>election</b> an independent enabling signal and refuses to start when fencing is
/// required but no gate resolved. The election is probed with <see cref="IServiceProviderIsService"/> - the
/// container's own "is this registered" question - so the election is not constructed merely to be validated.
/// </para>
/// <para>
/// SAFETY + LIVENESS. A refusal guard asserted only on its safety half is satisfied by a validator that
/// refuses everything, which would brick every single-node host. The liveness arms therefore prove not merely
/// that an un-elected host <i>starts</i> but that it actually <b>drains</b> - the arm that separates this fix
/// from an over-fix.
/// </para>
/// <para>
/// NON-VACUITY. The SAFETY arm is RED against the pre-fix predicate: with no gate registered, a gate-keyed
/// predicate yields an inactive fencing state and startup returns silently. Restoring the gate-only predicate
/// in <c>OutboxFencingStartupInvariant</c> turns this arm RED, confirming it binds the real fix.
/// </para>
/// <para>
/// Everything is resolved from a REAL <see cref="ServiceProvider"/>. The election and the store are the
/// subject matter under test; no dependency of the code under test is hand-injected past the container.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxRefusesUnfencedLeaderElectionShould
{
	[Fact]
	public async Task Refuse_WhenLeaderElectionRegisteredButNoGateWired()
	{
		// SAFETY - THE BUG ARM. An ILeaderElection is resolvable (the operator asked for coordination) but no
		// ILeaderProcessingGate was ever wired, so nothing fences the drain. The host MUST refuse to start.
		// The store here CAN fence: this arm isolates the missing GATE, not a store capability gap, so it
		// cannot pass for the wrong reason.
		await using var provider = BuildProvider(
			store: new FencedStore(),
			registerElection: true,    // operator registered a leader election ...
			registerGate: false,       // ... through a path that never wired the gate
			singleActiveWriter: false);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"A registered leader election with no leader gate means every instance drains the outbox " +
			"concurrently - the exact split-brain the election was added to prevent. Startup must refuse. If " +
			"this does not throw, the guard is keyed on the gate it exists to require and is blind by " +
			"construction to a leader-election package that forgets to wire it.");

		ex.Message.ShouldContain(
			"AsSingleWriter",
			Case.Insensitive,
			"The refusal must name the explicit single-writer opt-out, so an operator who genuinely runs one " +
			"drainer can proceed deliberately instead of being stuck.");
		ex.Message.ShouldContain(
			"WithLeaderElection",
			Case.Insensitive,
			"The refusal must name the wiring call that fixes it, not merely state that something is wrong.");
	}

	[Fact]
	public async Task Start_WhenNoLeaderElectionAndNoGate()
	{
		// LIVENESS (a) - the single-node host still starts. This is the arm that separates the fix from an
		// over-fix: no election, no gate, and a store that cannot fence is a perfectly valid single-instance
		// deployment. If this throws, the fix has bricked every single-node consumer.
		await using var provider = BuildProvider(
			store: new PlainStore(),
			registerElection: false,
			registerGate: false,
			singleActiveWriter: false);

		await Should.NotThrowAsync(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"With no leader election registered, fencing is not required at all. Refusing here would reject " +
			"every single-node deployment - an over-fix strictly worse than the bug.");
	}

	[Fact]
	public async Task Drain_WhenNoLeaderElectionAndNoGate()
	{
		// LIVENESS (b) - and it must actually DRAIN, not merely start. A host that starts and then never
		// dispatches is indistinguishable from a healthy one at startup and silently stops delivering. This
		// arm resolves the REAL IOutboxProcessor from the container and drives one claim.
		await using var provider = BuildProvider(
			store: new PlainStore(),
			registerElection: false,
			registerGate: false,
			singleActiveWriter: false);

		var store = provider.GetRequiredService<IOutboxStore>().ShouldBeOfType<PlainStore>();
		var processor = provider.GetRequiredService<IOutboxProcessor>();

		await DriveOneDrainAsync(processor).ConfigureAwait(false);

		store.UnfencedClaimCalled.ShouldBeTrue(
			"An un-elected single-node host must drain through the unfenced claim. If this is false the outbox " +
			"starts and silently delivers nothing.");
	}

	[Fact]
	public async Task StartAndDrainUnfenced_WhenLeaderElectionRegisteredAndSingleWriterOptOut()
	{
		// OPT-OUT - the consumer registered an election but explicitly asserted single-active-writer ownership
		// with AsSingleWriter(). No gate is wired and the store cannot fence, yet the host must start and
		// drain unfenced: the opt-out is the operator taking responsibility, and the guard must honour it
		// rather than second-guessing it.
		await using var provider = BuildProvider(
			store: new PlainStore(),
			registerElection: true,
			registerGate: false,
			singleActiveWriter: true);

		await Should.NotThrowAsync(
			() => Validator(provider).StartAsync(CancellationToken.None),
			"AsSingleWriter() is the explicit, documented opt-out. With it set the guard must not fire, even " +
			"with an election registered, no gate, and a non-fencing store.");

		var store = provider.GetRequiredService<IOutboxStore>().ShouldBeOfType<PlainStore>();
		var processor = provider.GetRequiredService<IOutboxProcessor>();

		await DriveOneDrainAsync(processor).ConfigureAwait(false);

		store.UnfencedClaimCalled.ShouldBeTrue(
			"Under the opt-out the drain must run unfenced. Starting but not draining would make the opt-out a " +
			"silent outage instead of a deliberate trade.");
	}

	// Drives exactly one producer claim: the store returns an empty batch, so the loops exit
	// deterministically - no timing, no wall-clock dependency.
	private static async Task DriveOneDrainAsync(IOutboxProcessor processor)
	{
		processor.Init("outbox-unfenced-election-lock");
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);
	}

	// Resolves the REAL OutboxPrerequisiteValidator (an IHostedService) the host runs at IHost.StartAsync().
	private static OutboxPrerequisiteValidator Validator(IServiceProvider provider) =>
		provider.GetServices<IHostedService>().OfType<OutboxPrerequisiteValidator>().Single();

	private static ServiceProvider BuildProvider(
		IOutboxStore store,
		bool registerElection,
		bool registerGate,
		bool singleActiveWriter)
	{
		var services = new ServiceCollection();

		// The real host supplies logging; AddExcalibur does not. OutboxProcessor takes
		// ILogger<OutboxProcessor> as a REQUIRED constructor parameter, so the DI-resolved
		// IOutboxProcessor the drain arms exercise cannot be constructed without it.
		_ = services.AddLogging();

		_ = services.AddExcalibur(x =>
			x.AddOutbox(o =>
			{
				if (singleActiveWriter)
				{
					// The real, observable opt-out: IOutboxBuilder.AsSingleWriter() sets
					// OutboxDeliveryOptions.SingleActiveWriter, which the guard reads through IOptions.
					_ = o.AsSingleWriter();
				}
			}));

		// A provider extension registers its concrete store as keyed "default"; the DI-resolved
		// IOutboxProcessor factory resolves the store non-keyed. Register both onto the one instance so the
		// validator and the drain judge the same store.
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => store);
		services.AddSingleton<IOutboxStore>(_ => store);

		if (registerElection)
		{
			// Exactly what every leader-election registration path makes resolvable, and the registration the
			// gate itself consumes: a NON-KEYED ILeaderElection.
			services.AddSingleton(A.Fake<ILeaderElection>());
		}

		if (registerGate)
		{
			services.AddSingleton(A.Fake<ILeaderProcessingGate>());
		}

		return services.BuildServiceProvider(validateScopes: false);
	}

	#region Fixtures

	// FIXTURE HONESTY. Both stores implement their interface DIRECTLY - no first-party base supplies the
	// fencing member - so the capability probe goes through the real GetService seam and answers honestly. A
	// bare FakeItEasy fake would answer GetService with a non-null dummy and masquerade as fencing-capable,
	// defeating the arms that depend on the store's true capability.

	private sealed class PlainStore : IOutboxStore
	{
		public bool UnfencedClaimCalled { get; private set; }

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			UnfencedClaimCalled = true;
			return new ValueTask<IEnumerable<OutboundMessage>>([]);
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}

	private sealed class FencedStore : IFencedOutboxStore
	{
		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize, long fencingToken, CancellationToken cancellationToken)
			=> new([]);

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
			=> new([]);

		public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}

	#endregion
}
