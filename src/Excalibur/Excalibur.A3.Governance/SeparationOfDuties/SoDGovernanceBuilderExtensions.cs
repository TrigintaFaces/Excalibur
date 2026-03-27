// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.SeparationOfDuties;
using Excalibur.A3.Governance.Stores.InMemory;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Separation of Duties services on <see cref="IGovernanceBuilder"/>.
/// </summary>
public static class SoDGovernanceBuilderExtensions
{
	/// <summary>
	/// Adds Separation of Duties services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configure">Optional delegate to configure <see cref="SoDOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="ISoDPolicyStore"/> with an in-memory fallback store.
	/// Override with a persistent store implementation via DI replacement.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddSeparationOfDuties(opts =>
	///         {
	///             opts.MinimumEnforcementSeverity = SoDSeverity.Critical;
	///             opts.DetectiveScanInterval = TimeSpan.FromHours(12);
	///         }));
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddSeparationOfDuties(
		this IGovernanceBuilder builder,
		Action<SoDOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register options with validation
		var optionsBuilder = builder.Services.AddOptions<SoDOptions>();
		if (configure is not null)
		{
			optionsBuilder.Configure(configure);
		}

		optionsBuilder.ValidateDataAnnotations()
			.ValidateOnStart();

		// Fallback in-memory store (overridable)
		builder.Services.TryAddSingleton<ISoDPolicyStore, InMemorySoDPolicyStore>();

		// Default evaluator (overridable)
		builder.Services.TryAddSingleton<ISoDEvaluator, DefaultSoDEvaluator>();

		// Preventive middleware (opt-in via EnablePreventiveEnforcement)
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, SoDPreventiveMiddleware>());

		// Detective scanning background service (opt-in via EnableDetectiveScanning)
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, SoDDetectiveScanService>());

		return builder;
	}
}
