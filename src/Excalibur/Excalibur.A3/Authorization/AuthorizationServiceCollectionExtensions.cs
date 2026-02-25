// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authorization;
using Excalibur.Dispatch.Abstractions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection" /> to register dispatch authorization services.
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
	/// <summary>
	/// Adds dispatch authorization services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDispatchAuthorization(this IServiceCollection services)
	{
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, GrantsAuthorizationHandler>());
		services.TryAddSingleton<IDispatchAuthorizationService, DispatchAuthorizationService>();
		services.TryAddSingleton<AttributeAuthorizationCache>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, AuthorizationMiddleware>());

		return services;
	}
}
