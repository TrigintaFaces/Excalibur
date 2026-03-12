// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Auth;

/// <summary>
/// Extension methods for adding tenant identity middleware to the dispatch pipeline.
/// </summary>
public static class TenantIdentityPipelineExtensions
{
	/// <summary>
	/// Adds tenant identity middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The tenant identity middleware resolves the current tenant from the message context
	/// and makes it available to downstream handlers via the tenant identity feature.
	/// </para>
	/// <para>
	/// Tenant resolution services must be registered separately in the DI container.
	/// This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseTenantIdentity()      // Resolve tenant after authentication
	///        .UseAuthorization()
	///        .UseValidation();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseTenantIdentity(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<TenantIdentityMiddleware>();
	}
}
