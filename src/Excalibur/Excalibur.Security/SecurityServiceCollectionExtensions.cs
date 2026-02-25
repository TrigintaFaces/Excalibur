// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Copyright (c) TrigintaFaces. All rights reserved.

using System.Diagnostics.CodeAnalysis;

using Excalibur.Security;
using Excalibur.Security.Abstractions;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up security services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Argon2id password hasher to the service collection with default options.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPasswordHasher(this IServiceCollection services)
	{
		return services.AddPasswordHasher(_ => { });
	}

	/// <summary>
	/// Adds the Argon2id password hasher to the service collection with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configure">An action to configure the Argon2 options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
	public static IServiceCollection AddPasswordHasher(
		this IServiceCollection services,
		Action<Argon2Options> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		_ = services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

		return services;
	}

	/// <summary>
	/// Adds the Argon2id password hasher to the service collection with configuration from a configuration section.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configuration">The configuration to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
	[RequiresUnreferencedCode("Binding configuration to Argon2Options may require unreferenced members.")]
	public static IServiceCollection AddPasswordHasher(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<Argon2Options>(configuration.GetSection(Argon2Options.SectionName));
		_ = services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

		return services;
	}
}
