// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.Outbox;
using Excalibur.Saga;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting.Builders;

/// <summary>
/// Unified builder for configuring all Excalibur subsystems.
/// </summary>
/// <remarks>
/// <para>
/// This builder provides a single fluent API for configuring Excalibur infrastructure:
/// <list type="bullet">
/// <item>Event sourcing (aggregates, event stores, snapshots, upcasting)</item>
/// <item>Outbox pattern (reliable messaging, background processing, cleanup)</item>
/// <item>Change Data Capture processing (table tracking, recovery)</item>
/// <item>Saga/process manager support</item>
/// <item>Leader election for distributed coordination</item>
/// </list>
/// </para>
/// <para>
/// Dispatch transport, pipeline, and middleware configuration is handled separately
/// via <see cref="IDispatchBuilder"/> in <c>AddDispatch()</c>. This builder focuses
/// exclusively on Excalibur subsystem configuration.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcalibur(excalibur =>
/// {
///     excalibur
///         .AddEventSourcing(es => es.UseEventStore&lt;SqlServerEventStore&gt;())
///         .AddOutbox(outbox => outbox.UseSqlServer(connectionString))
///         .AddCdc(cdc => cdc.TrackTable&lt;Order&gt;())
///         .AddSagas(opts => opts.EnableTimeouts = true)
///         .AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromSeconds(30));
/// });
/// </code>
/// </para>
/// </remarks>
public interface IExcaliburBuilder
{
	/// <summary>
	/// Gets the service collection for advanced registration scenarios.
	/// </summary>
	IServiceCollection Services { get; }

	/// <summary>
	/// Configures event sourcing (aggregates, event stores, snapshots, upcasting).
	/// </summary>
	/// <param name="configure">The event sourcing configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IExcaliburBuilder AddEventSourcing(Action<IEventSourcingBuilder> configure);

	/// <summary>
	/// Configures the outbox pattern (reliable messaging, background processing, cleanup).
	/// </summary>
	/// <param name="configure">The outbox configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IExcaliburBuilder AddOutbox(Action<IOutboxBuilder> configure);

	/// <summary>
	/// Configures Change Data Capture processing (table tracking, recovery).
	/// </summary>
	/// <param name="configure">The CDC configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IExcaliburBuilder AddCdc(Action<ICdcBuilder> configure);

	/// <summary>
	/// Configures saga/process manager support.
	/// </summary>
	/// <param name="configure">Optional action to configure saga options. Pass <see langword="null"/> to register with defaults.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IExcaliburBuilder AddSagas(Action<SagaOptions>? configure = null);

	/// <summary>
	/// Configures leader election for distributed coordination.
	/// </summary>
	/// <param name="configure">Optional action to configure leader election options. Pass <see langword="null"/> to register with defaults.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IExcaliburBuilder AddLeaderElection(Action<LeaderElectionOptions>? configure = null);
}
