// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Liskov L7 (8z65sn) — the load-bearing behavioural-subtype postcondition for the
/// <see cref="IMessageBus"/> event-publish seam: publishing an event fans out to <b>every</b> subscribed
/// handler, and one handler faulting MUST NOT abort its siblings (handler fault-independence). The
/// <see cref="IEventHandler{TEvent}"/> contract already states this ("Event handlers execute independently;
/// one handler's failure doesn't affect others"), so every <see cref="IMessageBus"/> implementor is a
/// behavioural subtype only if it honours it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Premise status at HEAD (verify-before-claiming):</b> the shipped <c>LocalMessageBus.PublishAsync</c>
/// already implements this — its multi-handler fan-out starts every handler, awaits all, and aggregates the
/// faults into an <see cref="AggregateException"/> (see <c>LocalMessageBus.cs</c>, "Fan-out with fault
/// isolation"). So this lock is a <b>regression pin</b>, not a fix: it prevents a future <see cref="IMessageBus"/>
/// implementor (or a refactor of the fan-out) from silently regressing to fail-fast.
/// </para>
/// <para>
/// <b>Safety + liveness pair (testing-patterns §3).</b> Safety: the faulting handler's exception is not
/// swallowed — it surfaces to the publisher. Liveness: the <i>non-faulting</i> siblings still run. A bus that
/// did nothing at all would satisfy neither; a fail-fast bus satisfies safety but FAILS liveness (the sibling
/// after the throw never runs) — which is exactly the violation this lock catches.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The postcondition is asserted against a correct direct-<see cref="IMessageBus"/>
/// fixture (fault-isolating, mirroring LocalMessageBus semantics) → GREEN, and proven RED against a
/// fail-fast direct fixture (the sibling after the throwing handler never executes). Both fixtures implement
/// <see cref="IMessageBus"/> from scratch (only the interface in their base list), so the lock binds the
/// <i>interface's</i> requirement, not an inherited convenience (fixture-shape corollary).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessageBusEventFaultIndependenceConformanceShould
{
	private static readonly IMessageContext Context = new MessageContext();

	/// <summary>
	/// The correct contract: every handler runs even though the middle one throws, and the fault surfaces.
	/// This is the behavioural-subtype postcondition; the shipped LocalMessageBus satisfies it.
	/// </summary>
	[Fact]
	public async Task PublishEvent_RunsEverySiblingHandler_EvenWhenOneThrows_FaultIsolatingBus()
	{
		// Arrange — three subscribers; the middle one faults.
		var ran = new bool[3];
		var handlers = new Func<Task>[]
		{
			() => { ran[0] = true; return Task.CompletedTask; },
			() => throw new InvalidOperationException("handler-2 boom"),
			() => { ran[2] = true; return Task.CompletedTask; },
		};
		var bus = new FaultIsolatingEventBus(handlers);

		// Act + SAFETY: the fault is not swallowed — it surfaces to the publisher.
		var ex = await Should.ThrowAsync<AggregateException>(
			async () => await bus.PublishAsync(new FaultIndependenceEvent(), Context, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
		ex.InnerExceptions.ShouldContain(e => e is InvalidOperationException);

		// LIVENESS: the siblings on BOTH sides of the throwing handler still executed.
		ran[0].ShouldBeTrue("the handler before the faulting one must run");
		ran[2].ShouldBeTrue("the handler AFTER the faulting one must still run — fault-independence");
	}

	/// <summary>
	/// Non-vacuity proof: a fail-fast bus VIOLATES the postcondition — the sibling after the throwing handler
	/// never runs. The liveness assertion above would RED against this bus.
	/// </summary>
	[Fact]
	public async Task PublishEvent_FailFastBus_SkipsTheSiblingAfterAThrow_ProvingTheLockIsNonVacuous()
	{
		// Arrange — same subscribers, but a fail-fast bus.
		var ran = new bool[3];
		var handlers = new Func<Task>[]
		{
			() => { ran[0] = true; return Task.CompletedTask; },
			() => throw new InvalidOperationException("handler-2 boom"),
			() => { ran[2] = true; return Task.CompletedTask; },
		};
		var bus = new FailFastEventBus(handlers);

		// Act — the first throw propagates immediately.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await bus.PublishAsync(new FaultIndependenceEvent(), Context, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		// RED-proof: the sibling AFTER the throw did NOT run — this is the liveness failure the conformance
		// postcondition catches. (The before-sibling did run.)
		ran[0].ShouldBeTrue();
		ran[2].ShouldBeFalse(
			"a fail-fast bus abandons the sibling after the throwing handler — the exact violation L7 forbids");
	}
}

/// <summary>Representative event for the fault-independence conformance lock.</summary>
internal sealed class FaultIndependenceEvent : IDispatchEvent;

/// <summary>
/// Direct-<see cref="IMessageBus"/> CORRECT fixture: fans out to every handler, aggregates faults into an
/// <see cref="AggregateException"/> — mirroring the shipped <c>LocalMessageBus</c> fault-isolating fan-out.
/// Implements the interface from scratch (no first-party base).
/// </summary>
internal sealed class FaultIsolatingEventBus(IReadOnlyList<Func<Task>> handlers) : IMessageBus
{
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		List<Exception>? faults = null;
		foreach (var handler in handlers)
		{
			try
			{
				await handler().ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Fault-independence: isolate each handler fault; rethrown aggregated below.
			catch (Exception ex)
			{
				(faults ??= []).Add(ex);
			}
#pragma warning restore CA1031
		}

		if (faults is not null)
		{
			throw new AggregateException("One or more handlers failed.", faults);
		}
	}

	public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken) =>
		throw new NotSupportedException();

	public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken) =>
		throw new NotSupportedException();
}

/// <summary>
/// Direct-<see cref="IMessageBus"/> VIOLATING fixture: fails fast on the first handler throw, abandoning the
/// remaining siblings. Implements the interface from scratch (no first-party base).
/// </summary>
internal sealed class FailFastEventBus(IReadOnlyList<Func<Task>> handlers) : IMessageBus
{
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		// The first throw propagates immediately — siblings after it never run.
		foreach (var handler in handlers)
		{
			await handler().ConfigureAwait(false);
		}
	}

	public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken) =>
		throw new NotSupportedException();

	public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken) =>
		throw new NotSupportedException();
}
