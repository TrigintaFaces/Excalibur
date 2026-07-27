// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the inbox schema-validation hosted service, which verifies every registered
/// <see cref="IInboxSchemaValidator"/> against its deployment mode at host startup.
/// </summary>
public static class InboxSchemaValidationServiceCollectionExtensions
{
	/// <summary>
	/// Adds the hosted service that verifies each registered inbox store's physical schema at host startup
	/// (fail-before-first-message). Idempotent: safe to call from every inbox provider registration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInboxSchemaValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, InboxSchemaValidationHostedService>());

		return services;
	}
}
