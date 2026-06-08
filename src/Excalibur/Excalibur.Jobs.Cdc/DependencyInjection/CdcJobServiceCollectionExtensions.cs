// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;
using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Jobs.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for registering the services required by the SQL Server
/// CDC (Change Data Capture) Quartz job, <see cref="CdcJob"/>.
/// </summary>
public static class CdcJobServiceCollectionExtensions
{
	/// <summary>
	/// Registers everything <see cref="CdcJob"/> needs to run: binds <see cref="CdcJobOptions"/>
	/// from the <c>Jobs:CdcJob</c> configuration section and registers the change-event processor
	/// factory together with its SQL Server data-access policy factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">
	/// The application configuration containing the <c>Jobs:CdcJob</c> section.
	/// </param>
	/// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Call this once during startup, then schedule the job with
	/// <see cref="CdcJob.ConfigureJob(Quartz.IServiceCollectionQuartzConfigurator, IConfiguration)"/>:
	/// </para>
	/// <code>
	/// builder.Services.AddSqlServerCdcJob(builder.Configuration);
	/// // ...inside AddJobs(configureQuartz: q => ...):
	/// CdcJob.ConfigureJob(q, builder.Configuration);
	/// </code>
	/// <para>
	/// Infrastructure services use <c>TryAdd</c> semantics, so a consumer (or
	/// <c>AddExcaliburSqlServices</c>) that already registered an
	/// <see cref="IDataAccessPolicyFactory"/> retains precedence. The tracked tables configured
	/// under each database are the single source of truth — capture instances are derived from
	/// them — so only <c>DatabaseConfigs[].Tables</c> needs to be supplied in configuration.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
	/// </exception>
	public static IServiceCollection AddSqlServerCdcJob(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

#pragma warning disable IL2026, IL3050 // Configuration binding uses reflection by design; mirrors CdcJob.ConfigureJob.
		_ = services.AddOptions<CdcJobOptions>()
			.Bind(configuration.GetSection(CdcJob.JobConfigSectionName))
			.ValidateOnStart();
#pragma warning restore IL2026, IL3050

		// SQL Server data-access policy factory (Polly-wrapped SQL calls). TryAdd so that
		// AddExcaliburSqlServices() or a consumer override keeps precedence.
		services.TryAddSingleton<IDataAccessPolicyFactory, SqlDataAccessPolicyFactory>();

		// The processor factory CdcJob depends on. Without this registration CdcJob cannot be
		// activated by Quartz's MicrosoftDependencyInjectionJobFactory.
		services.TryAddSingleton<IDataChangeEventProcessorFactory, DataChangeEventProcessorFactory>();

		return services;
	}
}
