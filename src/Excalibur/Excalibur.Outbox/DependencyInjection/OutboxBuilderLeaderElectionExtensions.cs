// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox;
using Excalibur.Outbox.Processing;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding a custom, delegate-based processing gate to <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// The leader-election-backed <c>WithLeaderElection()</c> overload — gated on the atomic,
/// fencing-token-carrying leadership snapshot — lives in the <c>Excalibur.LeaderElection</c> package.
/// <c>Excalibur.Outbox</c> takes no dependency on leader election; this class provides only the advisory,
/// delegate-based gate for consumers who want custom gating without leader election.
/// </remarks>
public static class OutboxBuilderProcessingGateExtensions
{
	/// <summary>
	/// Configures the outbox background service to skip processing when the
	/// provided gate returns <see langword="false"/>.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="isLeaderCheck">
	/// A factory delegate that, given an <see cref="IServiceProvider"/>, returns
	/// <see langword="true"/> when this instance should process outbox messages.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When registered, the <see cref="Excalibur.Outbox.Outbox.OutboxBackgroundService"/>
	/// checks <see cref="IProcessingGate.ShouldProcess"/> before each polling cycle.
	/// If the gate returns <see langword="false"/>, the cycle is skipped.
	/// </para>
	/// <para>
	/// This overload is <b>advisory only</b> — the supplied predicate carries no fencing token, so it must
	/// not be wired to a bare leadership bool for split-brain-safe gating. Prefer the leader-election
	/// package's parameterless <c>WithLeaderElection()</c> overload, which gates on the atomic leadership
	/// snapshot and fails closed. Use this overload only for a genuinely custom gate that is not
	/// leader-election-backed.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseInMemory()
	///           .WithLeaderElection(sp => MyCustomGateCheck(sp))
	///           .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </example>
	public static IOutboxBuilder WithLeaderElection(
		this IOutboxBuilder builder,
		Func<IServiceProvider, bool> isLeaderCheck)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(isLeaderCheck);

		builder.Services.TryAddSingleton<IProcessingGate>(sp =>
			new DelegateProcessingGate(() => isLeaderCheck(sp)));

		return builder;
	}

	/// <summary>
	/// Asserts that exactly one process drains this outbox, opting the drain out of leadership fencing even
	/// when a leader election is registered for other resources.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Fencing is <b>on by default</b> for a framework-protected outbox: registering a leader election is the
	/// multi-instance signal, so the drain is fenced. On stores that record the fencing high-water durably
	/// (PostgreSQL, Oracle, MongoDB) a superseded leader cannot claim or complete messages it no longer owns.
	/// The SQL Server store derives its high-water from the outbox rows instead of a dedicated fence record, so
	/// a cleanup that purges sent rows resets it and the advance overwrites rather than taking the maximum —
	/// its leadership fence is best-effort, though the per-message lease still prevents two processors claiming
	/// the same message. This method is the explicit, positive topology assertion — "I am the single active
	/// writer, I own the exactly-once guarantee" — for a deployment where a leader election is registered for
	/// <i>other</i> resources (leases, scheduled jobs) but exactly one process drains this outbox.
	/// </para>
	/// <para>
	/// The opt-out is <b>observable</b>: the outbox logs a startup warning that it is running unfenced by
	/// single-active-writer opt-out. Enabling this in a genuinely multi-writer deployment reopens the
	/// split-brain window fencing exists to close, so it must reflect a real single-active-writer topology.
	/// </para>
	/// </remarks>
	public static IOutboxBuilder AsSingleWriter(this IOutboxBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.Configure<OutboxDeliveryOptions>(o => o.SingleActiveWriter = true);

		return builder;
	}
}
