// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Spanner;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the Google Cloud Spanner data provider foundation shared by the
/// <c>Excalibur.*.Spanner</c> persistence stores.
/// </summary>
public static class SpannerServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Spanner connection provider and its validated <see cref="SpannerOptions"/>. Call this once
	/// per application; the individual <c>AddSpanner*</c> store registrations depend on it.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configures the Spanner connection options.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	public static IServiceCollection AddSpannerDataProvider(
		this IServiceCollection services,
		Action<SpannerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		services.AddOptions<SpannerOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SpannerOptions>, SpannerOptionsValidator>());

		services.TryAddSingleton<ISpannerConnectionProvider, SpannerConnectionProvider>();

		return services;
	}
}
