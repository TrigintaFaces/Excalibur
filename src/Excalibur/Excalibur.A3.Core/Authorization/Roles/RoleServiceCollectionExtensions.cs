// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Roles;
using Excalibur.A3.Authorization.Roles.Stores.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering role management on <see cref="IA3Builder"/>.
/// </summary>
public static class RoleServiceCollectionExtensions
{
	/// <summary>
	/// Adds role management services to the A3 builder.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Optional delegate to configure <see cref="RoleOptions"/>.</param>
	/// <returns>The <see cref="IA3Builder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="IRoleStore"/> with an in-memory fallback store and decorates
	/// <see cref="IAuthorizationEvaluator"/> with a role-aware evaluator that resolves
	/// <c>GrantType.Role</c> grants to effective permissions at authorization time.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddRoles(opts =>
	///     {
	///         opts.MaxHierarchyDepth = 3;
	///         opts.EnforceUniqueNames = true;
	///         opts.PermissionCacheDurationSeconds = 600;
	///     });
	/// </code>
	/// </remarks>
	public static IA3Builder AddRoles(
		this IA3Builder builder,
		Action<RoleOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register options with validation
		var optionsBuilder = builder.Services.AddOptions<RoleOptions>();
		if (configure is not null)
		{
			optionsBuilder.Configure(configure);
		}

		optionsBuilder.ValidateDataAnnotations()
			.ValidateOnStart();

		// Fallback in-memory store (overridable)
		builder.Services.TryAddSingleton<IRoleStore, InMemoryRoleStore>();

		// Shared role permission resolver (internal, used by decorator and SoD evaluator)
		builder.Services.TryAddSingleton<RolePermissionResolver>();

		// Decorate IAuthorizationEvaluator with role-aware resolution.
		// Uses manual factory pattern (no Scrutor dependency).
		DecorateAuthorizationEvaluator(builder.Services);

		return builder;
	}

	private static void DecorateAuthorizationEvaluator(IServiceCollection services)
	{
		// Find the existing IAuthorizationEvaluator registration
		var existingDescriptor = services.FirstOrDefault(
			d => d.ServiceType == typeof(IAuthorizationEvaluator));

		if (existingDescriptor is null)
		{
			return;
		}

		services.Remove(existingDescriptor);

		services.Add(ServiceDescriptor.Describe(
			typeof(IAuthorizationEvaluator),
			sp =>
			{
				var inner = CreateInnerEvaluator(sp, existingDescriptor);
				var grantStore = sp.GetRequiredService<IGrantStore>();
				var resolver = sp.GetRequiredService<RolePermissionResolver>();
				return new RoleAwareAuthorizationEvaluator(inner, grantStore, resolver);
			},
			existingDescriptor.Lifetime));
	}

	private static IAuthorizationEvaluator CreateInnerEvaluator(
		IServiceProvider sp,
		ServiceDescriptor descriptor)
	{
		if (descriptor.ImplementationInstance is IAuthorizationEvaluator instance)
		{
			return instance;
		}

		if (descriptor.ImplementationFactory is not null)
		{
			return (IAuthorizationEvaluator)descriptor.ImplementationFactory(sp);
		}

		if (descriptor.ImplementationType is not null)
		{
			return (IAuthorizationEvaluator)ActivatorUtilities.CreateInstance(
				sp, descriptor.ImplementationType);
		}

		throw new InvalidOperationException(
			"Cannot resolve inner IAuthorizationEvaluator from the existing service descriptor.");
	}
}
