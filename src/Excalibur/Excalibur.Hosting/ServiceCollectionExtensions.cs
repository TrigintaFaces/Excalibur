// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Hosting.Builders;
using Excalibur.Hosting.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Excalibur services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
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
	///         .AddOutbox(outbox => outbox.UseSqlServer(connectionString))
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
	///         .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
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

		// Register ExcaliburOptions so IOptions<ExcaliburOptions> is resolvable from DI
		_ = services.Configure<ExcaliburOptions>(_ => { });

		var builder = new ExcaliburBuilder(services);
		configure(builder);

		return services;
	}

	/// <summary>
	/// Adds Excalibur health checks and UI components to the service collection.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="withHealthChecks"> An optional action to configure additional health checks using an <see cref="IHealthChecksBuilder" />. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburHealthChecks(
		this IServiceCollection services,
		Action<IHealthChecksBuilder>? withHealthChecks = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var healthChecks = services.AddHealthChecks();

		withHealthChecks?.Invoke(healthChecks);

		_ = services.AddHealthChecksUI(static options =>
		{
			_ = options.SetEvaluationTimeInSeconds(10);
			_ = options.MaximumHistoryEntriesPerEndpoint(60);
			_ = options.SetApiMaxActiveRequests(1);
			_ = options.AddHealthCheckEndpoint("feedback api", "/.well-known/ready");
		});

		return services;
	}

	/// <summary>
	/// Registers Excalibur application and data layer services along with common context values.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> Assemblies containing handlers and validators. </param>
	/// <param name="useLocalClientAddress"> Whether to register the local machine address as <see cref="IClientAddress" />. </param>
	/// <param name="tenantId"> The tenant identifier. Defaults to <see cref="TenantDefaults.DefaultTenantId"/>. </param>
	/// <returns> The updated service collection. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburBaseServices(
		this IServiceCollection services,
		Assembly[] assemblies,
		bool useLocalClientAddress = false,
		string tenantId = TenantDefaults.DefaultTenantId)
	{
		ArgumentNullException.ThrowIfNull(services);

		// ADR-078: Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		_ = services.AddDispatch(assemblies);

		_ = services.AddExcaliburDataServices();
		_ = services.AddExcaliburApplicationServices(assemblies);
		_ = services.AddExcaliburContextServices(tenantId, localAddress: useLocalClientAddress);

		return services;
	}
}
