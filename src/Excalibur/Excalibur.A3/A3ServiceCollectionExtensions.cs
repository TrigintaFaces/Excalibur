// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;

using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Domain;
using Excalibur.Domain.Exceptions;

using Microsoft.Extensions.DependencyInjection.Extensions;

using IAuthorizationPolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods to configure full-stack A3 services.
/// </summary>
public static class A3ServiceCollectionExtensions
{
	/// <summary>
	/// Adds full A3 authorization services including CQRS pipeline, authentication,
	/// and dispatch middleware. Returns a builder for store configuration.
	/// </summary>
	/// <param name="services">The service collection to add A3 services to.</param>
	/// <returns>An <see cref="IA3Builder"/> for configuring store providers.</returns>
	/// <remarks>
	/// <para>
	/// This is the recommended entry point for full-stack A3 authorization.
	/// Use the returned builder to register store providers:
	/// </para>
	/// <code>
	/// services.AddExcaliburA3()
	///     .UseSqlServer();
	/// </code>
	/// <para>
	/// For a lightweight registration (no CQRS, no Dispatch pipeline, no
	/// external services), use the <c>Excalibur.A3.Core</c> package with
	/// <see cref="A3CoreServiceCollectionExtensions.AddExcaliburA3Core"/> instead.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IA3Builder AddExcaliburA3(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register core stores (in-memory fallbacks) via A3.Core
		var builder = services.AddExcaliburA3Core();

		_ = services.AddTenantContext();
		_ = services.AddA3DispatchServices();
		_ = AddAuthentication(services);
		_ = services.AddA3AuthorizationCore();
		services.TryAddScoped<IAccessToken, AccessToken>();

		// Full-stack A3 is the production authorization composition. Volatile grants lost on restart make a
		// user whose grants vanished indistinguishable from one who never had any — authorization silently
		// denies everyone. Install the startup gate so an in-memory grant store FAILS CLOSED here unless the
		// host opted in (AllowVolatileGrantStore = true) or registered a durable store. AddExcaliburA3Core()
		// stays gate-free — the lightweight in-memory dev/test path.
		_ = services.AddGrantDurabilityGate();

		return builder;
	}

	/// <summary>
	/// Registers authorization core services without database-specific providers.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	private static IServiceCollection AddA3AuthorizationCore(this IServiceCollection services)
	{
		services.TryAddScoped<IGrantRepository, GrantRepository>();

		_ = services
			.AddSingleton<Activities>()
			.AddTransient<ActivityGroups>()
			.AddTransient<UserGrants>()
			.AddScoped<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>()
			.AddScoped<IAuthorizationPolicy>(static container =>
				ResolvePolicySynchronously(container.GetRequiredService<IAuthorizationPolicyProvider>()))
			.AddHttpClient<IActivityGroupService, ActivityGroupService>(static client =>
			{
				client.BaseAddress = new Uri(ApplicationContext.AuthorizationServiceEndpoint);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			});

		return services;
	}

	/// <summary>
	/// Configures Dispatch services for A3 applications.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The updated service collection. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddA3DispatchServices(this IServiceCollection services)
	{
		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers(typeof(AuthorizationPolicy).Assembly);

		// AddExcaliburAudit registers AuditMiddleware together with the IActivityContext it resolves.
		// Registering the middleware again here is what re-opened the captive dependency it fixed: a
		// second descriptor at a different lifetime, de-duplicated by implementation type, so which
		// lifetime survived was decided by call order rather than by either registration.
		_ = services.AddExcaliburAudit();
		_ = services.AddExcaliburAuthorization();

		return services;
	}

	/// <summary>
	/// Configures authentication services.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
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

	private static IAuthorizationPolicy ResolvePolicySynchronously(IAuthorizationPolicyProvider policyProvider)
	{
		using var completed = new ManualResetEventSlim(false);
		IAuthorizationPolicy? resolvedPolicy = null;
		Exception? error = null;

		_ = ResolveAsync();
		if (!completed.Wait(TimeSpan.FromSeconds(30)))
		{
			throw new TimeoutException("Timed out while resolving authorization policy.");
		}

		if (error is not null)
		{
			ExceptionDispatchInfo.Capture(error).Throw();
		}

		return resolvedPolicy ?? throw new InvalidOperationException(
			"Authorization policy resolution returned null.");

		async Task ResolveAsync()
		{
			try
			{
				resolvedPolicy = await policyProvider.GetPolicyAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			finally
			{
				completed.Set();
			}
		}
	}
}
