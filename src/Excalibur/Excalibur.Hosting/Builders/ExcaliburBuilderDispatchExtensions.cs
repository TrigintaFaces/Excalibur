// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for bridging <see cref="IExcaliburBuilder"/> to <see cref="IDispatchBuilder"/>
/// for unified dispatch pipeline configuration within the Excalibur builder.
/// </summary>
public static class ExcaliburBuilderDispatchExtensions
{
	/// <summary>
	/// Configures the Dispatch messaging pipeline within the Excalibur builder.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">An optional action to configure the dispatch pipeline.</param>
	/// <returns>The Excalibur builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This bridges the Excalibur unified builder to the Dispatch pipeline builder,
	/// enabling a single composition root:
	/// </para>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur
	///     .UseDispatch(dispatch => dispatch
	///         .AddHandlersFromAssembly(typeof(Program).Assembly)
	///         .WithDefaults())
	///     .AddEventSourcing(es => es.UseSqlServer(connectionString))
	///     .AddOutbox(outbox => outbox.UseSqlServer(connectionString)));
	/// </code>
	/// <para>
	/// If <paramref name="configure"/> is null, Dispatch is registered with default settings.
	/// The existing <c>AddDispatch()</c> remains available as a standalone shortcut for
	/// simple MediatR-replacement scenarios.
	/// </para>
	/// </remarks>
	public static IExcaliburBuilder UseDispatch(
		this IExcaliburBuilder builder,
		Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.AddDispatch(configure);

		return builder;
	}
}
