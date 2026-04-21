// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering provisioning services on <see cref="IGovernanceBuilder"/>.
/// </summary>
public static class ProvisioningGovernanceBuilderExtensions
{
	/// <summary>
	/// Adds provisioning workflow services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configureProvisioning">Optional delegate to configure <see cref="ProvisioningOptions"/>.</param>
	/// <param name="configureJit">Optional delegate to configure <see cref="JitAccessOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddProvisioning(
	///             provisioning => provisioning.RequireRiskAssessment = true,
	///             jit => jit.DefaultJitDuration = TimeSpan.FromHours(8)));
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddProvisioning(
		this IGovernanceBuilder builder,
		Action<ProvisioningOptions>? configureProvisioning = null,
		Action<JitAccessOptions>? configureJit = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Provisioning options with validation
		var provisioningOptionsBuilder = builder.Services.AddOptions<ProvisioningOptions>();
		if (configureProvisioning is not null)
		{
			provisioningOptionsBuilder.Configure(configureProvisioning);
		}

		provisioningOptionsBuilder.ValidateOnStart();

		// JIT access options with validation
		var jitOptionsBuilder = builder.Services.AddOptions<JitAccessOptions>();
		if (configureJit is not null)
		{
			jitOptionsBuilder.Configure(configureJit);
		}

		jitOptionsBuilder.ValidateOnStart();

		return builder.AddProvisioningCore();
	}

	/// <summary>
	/// Adds provisioning workflow services using <see cref="IConfiguration"/> sections.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="provisioningConfiguration">The configuration section to bind to <see cref="ProvisioningOptions"/>.</param>
	/// <param name="jitConfiguration">The configuration section to bind to <see cref="JitAccessOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated binding.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	public static IGovernanceBuilder AddProvisioning(
		this IGovernanceBuilder builder,
		IConfiguration provisioningConfiguration,
		IConfiguration jitConfiguration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(provisioningConfiguration);
		ArgumentNullException.ThrowIfNull(jitConfiguration);

		_ = builder.Services.AddOptions<ProvisioningOptions>()
			.Bind(provisioningConfiguration)
			.ValidateOnStart();

		_ = builder.Services.AddOptions<JitAccessOptions>()
			.Bind(jitConfiguration)
			.ValidateOnStart();

		return builder.AddProvisioningCore();
	}

	private static IGovernanceBuilder AddProvisioningCore(this IGovernanceBuilder builder)
	{
		// Cross-property validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<
				Microsoft.Extensions.Options.IValidateOptions<JitAccessOptions>,
				JitAccessOptionsValidator>());

		// Fallback in-memory store (overridable)
		builder.Services.TryAddSingleton<IProvisioningStore, InMemoryProvisioningStore>();

		// Default workflow (overridable)
		builder.Services.TryAddSingleton<IProvisioningWorkflowConfiguration, DefaultSingleApproverWorkflow>();

		// Default risk assessor (overridable)
		builder.Services.TryAddSingleton<IGrantRiskAssessor, DefaultGrantRiskAssessor>();

		// Provisioning completion service (wires approval -> grant creation)
		builder.Services.TryAddSingleton<ProvisioningCompletionService>();

		// JIT expiry background service (registered conditionally by PostConfigure check)
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, JitAccessExpiryService>());

		return builder;
	}
}
