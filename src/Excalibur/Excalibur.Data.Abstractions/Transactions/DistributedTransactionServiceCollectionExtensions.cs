// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Transactions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring distributed transaction services.
/// </summary>
public static class DistributedTransactionServiceCollectionExtensions
{
	/// <summary>
	/// Adds distributed transaction coordination services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure distributed transaction options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDistributedTransactions(
		this IServiceCollection services,
		Action<DistributedTransactionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DistributedTransactionOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddSingleton<IDistributedTransactionCoordinator, InMemoryDistributedTransactionCoordinator>();

		return services;
	}

	/// <summary>
	/// Adds distributed transaction coordination services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDistributedTransactions(this IServiceCollection services)
	{
		return services.AddDistributedTransactions(_ => { });
	}
}
