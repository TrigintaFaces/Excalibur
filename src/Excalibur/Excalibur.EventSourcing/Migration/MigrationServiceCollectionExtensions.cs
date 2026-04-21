// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing.Migration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring event sourcing migration services.
/// </summary>
public static class MigrationServiceCollectionExtensions
{
	/// <summary>
	/// Adds event sourcing migration services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to configure migration runner options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This registers:
	/// <list type="bullet">
	/// <item><see cref="IEventBatchMigrator"/> for executing individual migration plans</item>
	/// <item><see cref="IMigrationRunner"/> for coordinating migration execution</item>
	/// <item><see cref="MigrationOptions"/> and <see cref="MigrationRunnerOptions"/> via IOptions</item>
	/// </list>
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddEventSourcingMigration(opts =>
	/// {
	///     opts.ParallelStreams = 4;
	///     opts.DryRun = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddEventSourcingMigration(
		this IServiceCollection services,
		Action<MigrationRunnerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		var optionsBuilder = services.AddOptions<MigrationRunnerOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder
			.ValidateOnStart();

		_ = services.AddOptions<MigrationOptions>()
			.ValidateOnStart();

		// Register services
		services.TryAddSingleton<IEventBatchMigrator, EventBatchMigrator>();
		services.TryAddSingleton<IMigrationRunner, MigrationRunner>();

		return services;
	}

	/// <summary>
	/// Adds event sourcing migration services to the service collection
	/// using an <see cref="IConfiguration"/> section for migration runner options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind migration runner options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddEventSourcingMigration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<MigrationRunnerOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		_ = services.AddOptions<MigrationOptions>()
			.ValidateOnStart();

		// Register services
		services.TryAddSingleton<IEventBatchMigrator, EventBatchMigrator>();
		services.TryAddSingleton<IMigrationRunner, MigrationRunner>();

		return services;
	}
}
