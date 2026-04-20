// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Hosting.Builders;

using Quartz;

using IJobConfigurator = Excalibur.Jobs.Quartz.IJobConfigurator;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for job-host configuration.
/// </summary>
/// <remarks>
/// <para>
/// These extensions are the canonical public path for opting a composition root into
/// Excalibur's Quartz-backed job hosting. They mirror the (now-internal) canonical
/// <c>AddExcaliburJobHost</c> overload set one-to-one and forward via thin delegation.
/// </para>
/// <para>
/// For worker-service scenarios that compose via <see cref="Microsoft.Extensions.Hosting.IHostApplicationBuilder"/>
/// rather than an <see cref="IExcaliburBuilder"/>, use the
/// <c>IHostApplicationBuilder.AddExcaliburJobHost(...)</c> carve-out instead
/// (see <c>Microsoft.Extensions.Hosting.ExcaliburJobHostBuilderExtensions</c>).
/// </para>
/// </remarks>
public static class JobsExcaliburBuilderExtensions
{
	/// <summary>
	/// Adds Excalibur Job Host services to the composition root.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="assemblies">An array of assemblies to scan for services and jobs.</param>
	/// <returns>The same <see cref="IExcaliburBuilder"/> for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur
	///     .AddJobs(typeof(Program).Assembly));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	public static IExcaliburBuilder AddJobs(this IExcaliburBuilder builder, params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddExcaliburJobHost(assemblies);
		return builder;
	}

	/// <summary>
	/// Adds Excalibur Job Host services with custom Quartz configuration.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configureQuartz">Optional action to configure Quartz services.</param>
	/// <param name="assemblies">An array of assemblies to scan for services and jobs.</param>
	/// <returns>The same <see cref="IExcaliburBuilder"/> for fluent chaining.</returns>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	public static IExcaliburBuilder AddJobs(this IExcaliburBuilder builder,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddExcaliburJobHost(configureQuartz, assemblies);
		return builder;
	}

	/// <summary>
	/// Adds Excalibur Job Host services with job configuration.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configureJobs">Action to configure specific jobs via <see cref="IJobConfigurator"/>.</param>
	/// <param name="assemblies">An array of assemblies to scan for services and jobs.</param>
	/// <returns>The same <see cref="IExcaliburBuilder"/> for fluent chaining.</returns>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	public static IExcaliburBuilder AddJobs(this IExcaliburBuilder builder,
		Action<IJobConfigurator> configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureJobs);

		_ = builder.Services.AddExcaliburJobHost(configureJobs, assemblies);
		return builder;
	}

	/// <summary>
	/// Adds Excalibur Job Host services with both Quartz and job configuration.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configureQuartz">Optional action to configure Quartz services.</param>
	/// <param name="configureJobs">Optional action to configure specific jobs via <see cref="IJobConfigurator"/>.</param>
	/// <param name="assemblies">An array of assemblies to scan for services and jobs.</param>
	/// <returns>The same <see cref="IExcaliburBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// This is the recommended unified entry point for configuring job hosting.
	/// </remarks>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	public static IExcaliburBuilder AddJobs(this IExcaliburBuilder builder,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		Action<IJobConfigurator>? configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddExcaliburJobHost(configureQuartz, configureJobs, assemblies);
		return builder;
	}
}
