// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for bridging <see cref="IExcaliburBuilder"/> to <see cref="IDispatchBuilder"/>
/// for unified dispatch pipeline configuration within the Excalibur builder.
/// </summary>
public static class ExcaliburBuilderDispatchExtensions
{
	/// <summary>
	/// Adds the Dispatch messaging pipeline within the Excalibur builder.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">An optional action to configure the dispatch pipeline.</param>
	/// <returns>The Excalibur builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Bridges the Excalibur unified builder to the Dispatch pipeline builder, keeping the
	/// <c>Add*</c> registration verb consistent with peer subsystem registration methods
	/// (<c>AddEventSourcing</c>, <c>AddOutbox</c>, <c>AddCdc</c>, <c>AddSagas</c>,
	/// <c>AddIdentityMap</c>, <c>AddLeaderElection</c>): registration uses <c>Add*</c>, while
	/// pipeline/middleware ordering uses <c>Use*</c>:
	/// </para>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur
	///     .AddDispatch(dispatch => dispatch
	///         .AddHandlersFromAssembly(typeof(Program).Assembly)
	///         .WithDefaults())
	///     .AddEventSourcing(es => es.UseSqlServer(opts => opts.ConnectionString = connectionString))
	///     .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString))));
	/// </code>
	/// <para>
	/// If <paramref name="configure"/> is null, Dispatch is registered with default settings.
	/// The standalone <c>services.AddDispatch()</c> remains available as a shortcut for simple
	/// MediatR-replacement scenarios that do not compose the Excalibur application framework.
	/// </para>
	/// </remarks>
	public static IExcaliburBuilder AddDispatch(
		this IExcaliburBuilder builder,
		Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.AddDispatch(configure);

		return builder;
	}
}
