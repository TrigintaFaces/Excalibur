// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB authorization services.
/// </summary>
public static class DynamoDbAuthorizationExtensions
{
	/// <summary>
	/// Adds DynamoDB-based authorization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbAuthorization(
		this IServiceCollection services,
		Action<DynamoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<IGrantRequestProvider, DynamoDbGrantService>();
		services.TryAddSingleton<IActivityGroupGrantService, DynamoDbActivityGroupGrantService>();

		return services;
	}

	/// <summary>
	/// Adds DynamoDB-based authorization services to the service collection with LocalStack configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serviceUrl">The DynamoDB service URL (e.g., "http://localhost:4566" for LocalStack).</param>
	/// <param name="grantsTableName">The grants table name. Defaults to "authorization_grants".</param>
	/// <param name="activityGroupsTableName">The activity groups table name. Defaults to "authorization_activity_groups".</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbAuthorization(
		this IServiceCollection services,
		string serviceUrl,
		string grantsTableName = "authorization_grants",
		string activityGroupsTableName = "authorization_activity_groups")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);

		return services.AddDynamoDbAuthorization(options =>
		{
			options.ServiceUrl = serviceUrl;
			options.GrantsTableName = grantsTableName;
			options.ActivityGroupsTableName = activityGroupsTableName;
		});
	}

	/// <summary>
	/// Adds only the DynamoDB grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbGrantService(
		this IServiceCollection services,
		Action<DynamoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<IGrantRequestProvider, DynamoDbGrantService>();

		return services;
	}

	/// <summary>
	/// Adds only the DynamoDB activity group grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbActivityGroupGrantService(
		this IServiceCollection services,
		Action<DynamoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<IActivityGroupGrantService, DynamoDbActivityGroupGrantService>();

		return services;
	}
}
