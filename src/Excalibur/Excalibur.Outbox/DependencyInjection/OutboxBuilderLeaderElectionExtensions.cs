// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// Configures the outbox background service to skip processing when the
	/// provided gate returns <see langword="false"/>.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="isLeaderCheck">
	/// A factory delegate that, given an <see cref="IServiceProvider"/>, returns
	/// <see langword="true"/> when this instance should process outbox messages.
	/// Typically wired to <c>ILeaderElection.IsLeader</c>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When registered, the <see cref="Excalibur.Outbox.Outbox.OutboxBackgroundService"/>
	/// checks <see cref="IProcessingGate.ShouldProcess"/> before each polling cycle.
	/// If the gate returns <see langword="false"/>, the cycle is skipped.
	/// </para>
	/// <para>
	/// For automatic wiring with <c>ILeaderElection</c>, install
	/// <c>Excalibur.LeaderElection</c> and use the parameterless
	/// <c>WithLeaderElection()</c> overload.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseInMemory()
	///           .WithLeaderElection(sp =>
	///           {
	///               var le = sp.GetRequiredService&lt;ILeaderElection&gt;();
	///               return le.IsLeader;
	///           })
	///           .EnableBackgroundProcessing();
	/// });
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
}
