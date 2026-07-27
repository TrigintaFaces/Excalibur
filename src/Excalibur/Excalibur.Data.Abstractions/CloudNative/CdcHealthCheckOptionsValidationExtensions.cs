// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helper for the reflection-free (AOT-safe) <see cref="CdcHealthCheckOptions"/> startup validator.
/// </summary>
/// <remarks>
/// The validator itself is internal to the assembly that owns <see cref="CdcHealthCheckOptions"/>; this helper is the
/// supported public entry point so any provider package that binds the options can register the same validator.
/// Registration is idempotent (<c>TryAddEnumerable</c>), so calling it from multiple provider packages is safe.
/// </remarks>
public static class CdcHealthCheckOptionsValidationExtensions
{
	/// <summary>Registers the startup validator for <see cref="CdcHealthCheckOptions"/>.</summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
	public static IServiceCollection AddCdcHealthCheckOptionsValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CdcHealthCheckOptions>, CdcHealthCheckOptionsValidator>());

		return services;
	}
}
