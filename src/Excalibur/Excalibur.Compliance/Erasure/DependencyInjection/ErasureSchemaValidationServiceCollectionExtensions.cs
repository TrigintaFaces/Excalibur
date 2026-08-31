// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the erasure schema-validation hosted service, which verifies every registered
/// <see cref="IErasureSchemaValidator"/> at host startup.
/// </summary>
public static class ErasureSchemaValidationServiceCollectionExtensions
{
	/// <summary>
	/// Adds the hosted service that verifies each registered erasure store's backing schema at host startup
	/// (fail-before-first-request). Idempotent: safe to call from every erasure provider registration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Provisioning needs a round trip to the database, so it cannot be an <c>IValidateOptions</c> check —
	/// those run synchronously and are meant to inspect the configured values, not the deployment they
	/// point at. Startup is still the right moment, which is what a hosted service provides: the host
	/// refuses to start rather than surfacing a deployment fault on a caller's first erasure request.
	/// </remarks>
	public static IServiceCollection AddErasureSchemaValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ErasureSchemaValidationHostedService>());

		return services;
	}
}
