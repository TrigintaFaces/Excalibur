// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application;
using Excalibur.Domain;

using Excalibur.Hosting.Builders;
using Excalibur.Hosting.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Excalibur services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ExcaliburHostingServiceCollectionExtensions
{
	/// <summary>
	/// Adds all Excalibur subsystem services using a unified builder.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <param name="configure">The builder configuration action.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance for further configuration.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the unified entry point for configuring all Excalibur subsystems.
	/// Registers Dispatch primitives with sensible defaults so that <c>IDispatcher</c>
	/// and the core pipeline are available without a separate <c>AddDispatch()</c> call.
	/// </para>
	/// <para>
	/// <b>Simple usage (Dispatch defaults are sufficient):</b>
	/// <code>
	/// services.AddExcalibur(excalibur =>
	/// {
	///     excalibur
	///         .AddEventSourcing(es => es.UseEventStore&lt;SqlServerEventStore&gt;())
	///         .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString)))
	///         .AddCdc(cdc => cdc.TrackTable&lt;Order&gt;())
	///         .AddSagas(opts => opts.EnableTimeouts = true)
	///         .AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromSeconds(30));
	/// });
	/// </code>
	/// </para>
	/// <para>
	/// <b>Advanced usage (custom Dispatch configuration):</b>
	/// When you need to configure transports, pipelines, or middleware, call
	/// <c>AddDispatch</c> with a builder action. Both orderings are safe because
	/// all Dispatch registrations use <c>TryAdd</c> internally.
	/// <code>
	/// // 1. Configure Dispatch with transports and middleware
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
	///     dispatch.AddObservability(obs => obs.Enabled = true);
	///     dispatch.ConfigurePipeline("default", p => p.UseValidation());
	/// });
	///
	/// // 2. Configure Excalibur subsystems
	/// services.AddExcalibur(excalibur =>
	/// {
	///     excalibur
	///         .AddEventSourcing(es => es.UseEventStore&lt;SqlServerEventStore&gt;())
	///         .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString)));
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExcalibur(
		this IServiceCollection services,
		Action<IExcaliburBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure Dispatch primitives are registered (idempotent via TryAdd*)
		_ = services.AddDispatch();

		// Register ExcaliburOptions with ValidateOnStart
		_ = services.AddOptions<ExcaliburOptions>()
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ExcaliburOptions>, ExcaliburOptionsValidator>());

		var builder = new ExcaliburBuilder(services);
		configure(builder);

		// Register the Excalibur core cross-cutting primitives (context family +
		// IActivityContext) AFTER configure(builder) so that explicit consumer
		// registrations inside the builder callback win against these TryAdd
		// defaults. Rationale: TryAdd is a no-op when a registration exists — it
		// is not a Replace. Registering after configure guarantees:
		//   * consumer did nothing → TryAdd lands the default;
		//   * consumer registered a custom impl in configure → their descriptor is
		//     already present, TryAdd is the no-op, and their impl wins.
		// [S793 bd-sdhocq P0 / COMPASS msg 1449 §1]
		_ = services.AddExcaliburContextServices();
		services.TryAddScoped<IActivityContext, ActivityContext>();

		return services;
	}

	// AddExcaliburBaseServices(...) was deleted in S804 (bd-sdhocq A8) per ADR-325 §2.
	// The canonical composition path is services.AddExcalibur(x => x.ScanAssemblies(...))
	// with explicit .UseTenant(...) / .UseLocalClientAddress() opt-ins.
}
