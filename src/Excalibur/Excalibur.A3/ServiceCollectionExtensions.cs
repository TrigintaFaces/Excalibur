// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;
using Excalibur.Domain.Exceptions;

using Microsoft.Extensions.DependencyInjection.Extensions;

using IAuthorizationPolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods to configure application services for A3 Excalibur applications.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Configures the A3 Excalibur services.
	/// </summary>
	/// <param name="services"> The service collection to add A3 services. </param>
	/// <param name="databaseType"> The type of database to configure for A3 services. </param>
	/// <returns> The updated service collection. </returns>
	/// <remarks>
	/// Automatically registers <see cref="ITenantId"/> with <see cref="TenantDefaults.DefaultTenantId"/>
	/// if not already registered. For multi-tenant applications, register your tenant resolver
	/// before calling this method.
	/// </remarks>
	[RequiresUnreferencedCode("Calls methods that use Type.GetType to dynamically load database-specific request providers.")]
	public static IServiceCollection AddExcaliburA3Services(this IServiceCollection services, SupportedDatabase databaseType)
	{
		_ = services.TryAddTenantId();
		_ = services.AddA3DispatchServices();
		_ = services.AddAuthentication();
		_ = services.AddAuthorization(databaseType);
		_ = services.AddActivities(typeof(AuthorizationPolicy).Assembly);
		services.TryAddScoped<IAccessToken, AccessToken>();

		return services;
	}

	/// <summary>
	/// Configures Dispatch services for A3 applications.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection AddA3DispatchServices(this IServiceCollection services)
	{
		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers(typeof(AuthorizationPolicy).Assembly);

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, AuditMiddleware>());
		_ = services.AddDispatchAuthorization();

		return services;
	}

	/// <summary>
	/// Configures authentication services.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The updated service collection. </returns>
	/// <exception cref="InvalidConfigurationException">
	/// Thrown when the <see cref="ApplicationContext.AuthenticationServiceEndpoint" /> has an invalid format.
	/// </exception>
	private static IServiceCollection AddAuthentication(this IServiceCollection services)
	{
		try
		{
			_ = services.AddHttpClient<IAuthenticationTokenProvider, AuthenticationTokenProvider>(static client =>
			{
				client.BaseAddress = new Uri(ApplicationContext.AuthenticationServiceEndpoint);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			});
		}
		catch (UriFormatException ex)
		{
			throw new InvalidConfigurationException(nameof(ApplicationContext.AuthenticationServiceEndpoint), innerException: ex);
		}

		return services;
	}

	/// <summary>
	/// Configures authorization services with grant-based policy evaluation.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="databaseType"> The type of database to configure for authorization services. </param>
	/// <returns> The updated service collection. </returns>
	[RequiresUnreferencedCode("Calls methods that use Type.GetType to dynamically load database-specific request providers.")]
	private static IServiceCollection AddAuthorization(this IServiceCollection services, SupportedDatabase databaseType)
	{
		_ = services
			.AddAuthorizationRequestProviders(databaseType)
			.AddSingleton<Activities>()
			.AddTransient<ActivityGroups>()
			.AddTransient<UserGrants>()
			.AddScoped<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>()
			.AddScoped(static container => container
				.GetRequiredService<IAuthorizationPolicyProvider>()
				.GetPolicyAsync()
				.GetAwaiter()
				.GetResult())
			.AddHttpClient<IActivityGroupService, ActivityGroupService>(static client =>
			{
				client.BaseAddress = new Uri(ApplicationContext.AuthorizationServiceEndpoint);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			});

		return services;
	}

	[RequiresUnreferencedCode("Calls methods that use Type.GetType to dynamically load database-specific request providers.")]
	private static IServiceCollection AddAuthorizationRequestProviders(this IServiceCollection services, SupportedDatabase databaseType)
	{
		_ = services.AddActivityGroupRequestProvider(databaseType);
		_ = services.AddGrantRequestProvider(databaseType);

		return services;
	}

	[RequiresUnreferencedCode(
		"Uses Type.GetType to dynamically load database-specific request providers. The types should be preserved if using AOT.")]
	private static IServiceCollection AddActivityGroupRequestProvider(this IServiceCollection services, SupportedDatabase databaseType)
	{
		var requestProviderType = databaseType switch
		{
			SupportedDatabase.Postgres => Type.GetType(
				"Excalibur.Dispatch.Databases.Postgres.RequestProviders.Authorization.ActivityGroups.PostgresActivityGroupRequestProvider, Excalibur.Dispatch.Databases.Postgres"),
			SupportedDatabase.SqlServer => Type.GetType(
				"Excalibur.Dispatch.Databases.SqlServer.RequestProviders.Authorization.ActivityGroups.SqlServerActivityGroupRequestProvider, Excalibur.Dispatch.Databases.SqlServer"),
			SupportedDatabase.Unknown or _ => throw new NotSupportedException(
				$"Database type '{databaseType}' is not supported."),
		}
								  ?? throw new InvalidOperationException(
									  $"The request provider for '{databaseType}' is not available. Ensure the appropriate NuGet package is installed.");

		services.TryAddSingleton(typeof(IActivityGroupRequestProvider), requestProviderType);
		return services;
	}

	[RequiresUnreferencedCode(
		"Uses Type.GetType to dynamically load database-specific request providers. The types should be preserved if using AOT.")]
	private static IServiceCollection AddGrantRequestProvider(this IServiceCollection services, SupportedDatabase databaseType)
	{
		var requestProviderType = databaseType switch
		{
			SupportedDatabase.Postgres => Type.GetType(
				"Excalibur.Dispatch.Databases.Postgres.RequestProviders.Authorization.Grants.PostgresGrantRequestProvider, Excalibur.Dispatch.Databases.Postgres"),
			SupportedDatabase.SqlServer => Type.GetType(
				"Excalibur.Dispatch.Databases.SqlServer.RequestProviders.Authorization.Grants.SqlServerGrantRequestProvider, Excalibur.Dispatch.Databases.SqlServer"),
			SupportedDatabase.Unknown or _ => throw new NotSupportedException(
				$"Database type '{databaseType}' is not supported."),
		}
								  ?? throw new InvalidOperationException(
									  $"The request provider for '{databaseType}' is not available. Ensure the appropriate NuGet package is installed.");

		services.TryAddSingleton(typeof(IGrantRequestProvider), requestProviderType);
		return services;
	}
}
