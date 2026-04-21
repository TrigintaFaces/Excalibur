// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.OrphanedAccess;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering orphaned access detection services on <see cref="IGovernanceBuilder"/>.
/// </summary>
public static class OrphanedAccessGovernanceBuilderExtensions
{
	/// <summary>
	/// Adds orphaned access detection services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configure">Optional delegate to configure <see cref="OrphanedAccessOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Consumers MUST register an <see cref="IUserStatusProvider"/> implementation before
	/// calling this method. No default implementation is provided.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddOrphanedAccessDetection(opts =>
	///         {
	///             opts.ScanIntervalHours = 12;
	///             opts.AutoRevokeDeparted = true;
	///         }));
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddOrphanedAccessDetection(
		this IGovernanceBuilder builder,
		Action<OrphanedAccessOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register options with validation
		var optionsBuilder = builder.Services.AddOptions<OrphanedAccessOptions>();
		if (configure is not null)
		{
			optionsBuilder.Configure(configure);
		}

		optionsBuilder.ValidateOnStart();

		return builder.AddOrphanedAccessDetectionCore();
	}

	/// <summary>
	/// Adds orphaned access detection services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="OrphanedAccessOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	public static IGovernanceBuilder AddOrphanedAccessDetection(
		this IGovernanceBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<OrphanedAccessOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		return builder.AddOrphanedAccessDetectionCore();
	}

	private static IGovernanceBuilder AddOrphanedAccessDetectionCore(this IGovernanceBuilder builder)
	{
		// Default detector (overridable)
		builder.Services.TryAddSingleton<IOrphanedAccessDetector, DefaultOrphanedAccessDetector>();

		// Background scanning service
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, OrphanedAccessScanService>());

		return builder;
	}
}
