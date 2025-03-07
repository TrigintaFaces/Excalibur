using System.Net.Http.Headers;

using DOPA.DependencyInjection;

using Excalibur.A3.Audit;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Application;
using Excalibur.Core;
using Excalibur.Core.Exceptions;
using Excalibur.DataAccess;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3;

/// <summary>
///     Provides extension methods to configure application services for A3 Excalibur applications.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Configures the A3 Excalibur services.
	/// </summary>
	/// <param name="services"> The service collection to add A3 services. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection AddExcaliburA3Services(this IServiceCollection services, SupportedDatabase databaseType)
	{
		_ = services.AddA3MediatRServices();
		_ = services.AddTransient<IAccessToken, AccessToken>();

		_ = services.AddAuthentication();
		_ = services.AddAuthorization(databaseType);
		_ = services.AddActivities(typeof(AuthorizationPolicy).Assembly);
		_ = services.AddScoped<IAccessToken, AccessToken>();

		return services;
	}

	/// <summary>
	///     Configures MediatR services for A3 applications.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection AddA3MediatRServices(this IServiceCollection services)
	{
		_ = services
			.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AuthorizationPolicy).Assembly))
			.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>))
			.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));

		return services;
	}

	/// <summary>
	///     Configures authentication services.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The updated service collection. </returns>
	/// <exception cref="InvalidConfigurationException">
	///     Thrown when the <see cref="ApplicationContext.AuthenticationServiceEndpoint" /> has an invalid format.
	/// </exception>
	private static IServiceCollection AddAuthentication(this IServiceCollection services)
	{
		try
		{
			_ = services.AddHttpClient<IAuthenticationTokenProvider, AuthenticationTokenProvider>(client =>
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
	///     Configures authorization services using OPA policies.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The updated service collection. </returns>
	private static IServiceCollection AddAuthorization(this IServiceCollection services, SupportedDatabase databaseType)
	{
		var assembly = typeof(AuthorizationPolicy).Assembly;
		var wasmResourcePath = $"{assembly.GetName().Name}.A3.Authorization.authorization.wasm";
		using var stream = assembly.GetManifestResourceStream(wasmResourcePath);

		_ = services
			.AddOpaPolicy<AuthorizationPolicy>(stream)
			.AddAuthorizationRequestProviders(databaseType)
			.AddSingleton<Activities>()
			.AddTransient<ActivityGroups>()
			.AddTransient<UserGrants>()
			.AddScoped<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>()
			.AddScoped(container => container
				.GetRequiredService<IAuthorizationPolicyProvider>()
				.GetPolicyAsync()
				.GetAwaiter()
				.GetResult())
			.AddHttpClient<IActivityGroupService, ActivityGroupService>(client =>
			{
				client.BaseAddress = new Uri(ApplicationContext.AuthorizationServiceEndpoint);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			});

		return services;
	}

	private static IServiceCollection AddAuthorizationRequestProviders(this IServiceCollection services, SupportedDatabase databaseType)
	{
		_ = services.AddActivityGroupRequestProvider(databaseType);
		_ = services.AddGrantRequestProvider(databaseType);

		return services;
	}

	private static IServiceCollection AddActivityGroupRequestProvider(this IServiceCollection services, SupportedDatabase databaseType)
	{
		var requestProviderType = databaseType switch
		{
			SupportedDatabase.Postgres => Type.GetType(
				"Excalibur.A3.Postgres.RequestProviders.Authorization.ActivityGroups.PostgresActivityGroupRequestProvider, Excalibur.A3.Postgres"),
			SupportedDatabase.SqlServer => Type.GetType(
				"Excalibur.A3.SqlServer.RequestProviders.Authorization.ActivityGroups.SqlServerActivityGroupRequestProvider, Excalibur.A3.SqlServer"),
			SupportedDatabase.Unknown or _ => throw new NotSupportedException($"Database type '{databaseType}' is not supported.")
		};

		if (requestProviderType == null)
		{
			throw new InvalidOperationException(
				$"The request provider for '{databaseType}' is not available. Ensure the appropriate NuGet package is installed.");
		}

		_ = services.AddSingleton(typeof(IActivityGroupRequestProvider), requestProviderType);
		return services;
	}

	private static IServiceCollection AddGrantRequestProvider(this IServiceCollection services, SupportedDatabase databaseType)
	{
		var requestProviderType = databaseType switch
		{
			SupportedDatabase.Postgres => Type.GetType(
				"Excalibur.A3.Postgres.RequestProviders.Authorization.Grants.PostgresGrantRequestProvider, Excalibur.A3.Postgres"),
			SupportedDatabase.SqlServer => Type.GetType(
				"Excalibur.A3.SqlServer.RequestProviders.Authorization.Grants.SqlServerGrantRequestProvider, Excalibur.A3.SqlServer"),
			SupportedDatabase.Unknown or _ => throw new NotSupportedException($"Database type '{databaseType}' is not supported.")
		};

		if (requestProviderType == null)
		{
			throw new InvalidOperationException(
				$"The request provider for '{databaseType}' is not available. Ensure the appropriate NuGet package is installed.");
		}

		_ = services.AddSingleton(typeof(IGrantRequestProvider), requestProviderType);
		return services;
	}
}
