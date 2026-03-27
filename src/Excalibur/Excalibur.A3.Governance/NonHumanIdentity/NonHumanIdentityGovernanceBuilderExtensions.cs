// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.NonHumanIdentity;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering non-human identity services on <see cref="IGovernanceBuilder"/>.
/// </summary>
public static class NonHumanIdentityGovernanceBuilderExtensions
{
	/// <summary>
	/// Adds non-human identity classification services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers a default <see cref="IPrincipalTypeProvider"/> that classifies all principals
	/// as <see cref="PrincipalType.Human"/>. Override by registering a custom implementation
	/// before calling this method.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddNonHumanIdentity());
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddNonHumanIdentity(this IGovernanceBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IPrincipalTypeProvider, DefaultPrincipalTypeProvider>();

		return builder;
	}

	/// <summary>
	/// Adds API key management services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configure">Optional delegate to configure <see cref="ApiKeyOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers an in-memory API key manager with SHA-256 hashed key storage.
	/// Override by registering a custom <see cref="IApiKeyManager"/> before calling this method.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddNonHumanIdentity()
	///         .AddApiKeyManagement(opts =>
	///         {
	///             opts.MaxKeysPerPrincipal = 5;
	///             opts.DefaultExpirationDays = 30;
	///         }));
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddApiKeyManagement(
		this IGovernanceBuilder builder,
		Action<ApiKeyOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Options with validation
		var optionsBuilder = builder.Services.AddOptions<ApiKeyOptions>();
		if (configure is not null)
		{
			optionsBuilder.Configure(configure);
		}

		optionsBuilder.ValidateDataAnnotations()
			.ValidateOnStart();

		// Cross-property validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<
				Microsoft.Extensions.Options.IValidateOptions<ApiKeyOptions>,
				ApiKeyOptionsValidator>());

		// Default in-memory manager (overridable)
		builder.Services.TryAddSingleton<IApiKeyManager, InMemoryApiKeyManager>();

		// Ensure principal type provider is registered
		builder.Services.TryAddSingleton<IPrincipalTypeProvider, DefaultPrincipalTypeProvider>();

		return builder;
	}
}
