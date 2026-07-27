// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Processing;
using Excalibur.Outbox;
using Excalibur.Outbox.Processing;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding leader election gating to <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderLeaderElectionExtensions
{
	/// <summary>
	/// Configures the outbox background service to skip processing on this instance whenever it does not
	/// hold leadership, using the atomic, fencing-token-carrying leadership snapshot rather than an
	/// advisory bool.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers an <see cref="ILeaderProcessingGate"/> (also resolvable as <see cref="IProcessingGate"/>)
	/// backed by the DI-resolved <see cref="ILeaderElection"/>. The gate fails closed: the current
	/// instance is gated on <c>ILeaderElection.CurrentLeadership is not null</c>, never on the advisory
	/// <c>ILeaderElection.IsLeader</c> bool, so a null leadership snapshot always skips the protected
	/// processing cycle.
	/// </para>
	/// <para>
	/// This extension requires an <see cref="ILeaderElection"/> to already be registered in DI (via a
	/// leader-election provider package). Use the
	/// <see cref="OutboxBuilderProcessingGateExtensions.WithLeaderElection(IOutboxBuilder, Func{IServiceProvider, bool})"/>
	/// overload in <c>Excalibur.Outbox</c> for a custom, non-leader-election gate — a hand-supplied
	/// predicate is advisory only and carries no fencing token.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseInMemory()
	///           .WithLeaderElection()
	///           .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </example>
	public static IOutboxBuilder WithLeaderElection(this IOutboxBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		RegisterOutboxLeaderGate(builder.Services);

		return builder;
	}

	/// <summary>
	/// Registers the leader-election-backed outbox processing gate (also resolvable as
	/// <see cref="IProcessingGate"/> and <see cref="ILeaderProcessingGate"/>) so the outbox drain fences on
	/// the atomic, fencing-token-carrying leadership snapshot. Idempotent via <c>TryAdd</c>, so an explicit
	/// <see cref="WithLeaderElection(IOutboxBuilder)"/> and the automatic default-on wiring compose without
	/// double registration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <remarks>
	/// Fencing is on by default for a framework-protected outbox: registering a leader election is the
	/// multi-instance signal, so this gate is wired automatically when a leader election is added
	/// (see <c>LeaderElectionExcaliburBuilderExtensions.AddLeaderElection</c>). The consumer opts the outbox
	/// out — for a genuinely single-active-writer topology — with the outbox builder's <c>AsSingleWriter()</c>.
	/// </remarks>
	internal static void RegisterOutboxLeaderGate(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<LeaderElectionProcessingGate>(sp =>
			new LeaderElectionProcessingGate(sp.GetRequiredService<ILeaderElection>()));
		services.TryAddSingleton<IProcessingGate>(sp => sp.GetRequiredService<LeaderElectionProcessingGate>());
		services.TryAddSingleton<ILeaderProcessingGate>(sp => sp.GetRequiredService<LeaderElectionProcessingGate>());
	}
}
