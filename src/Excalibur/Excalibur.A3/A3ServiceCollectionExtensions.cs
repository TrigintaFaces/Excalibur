// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

		// Authorization grants are cached under keys that identify a user but not an application, so an
		// application-scoped cache is a prerequisite rather than an enhancement. The components already
		// depend on it by service key, which makes an unscoped composition fail to resolve; this turns that
		// failure into a start-up error naming the call to add.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, AuthorizationCachePrerequisiteValidator>(
				_ => new AuthorizationCachePrerequisiteValidator(services)));
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AuthorizationCachePrerequisiteValidator>(
				_ => new AuthorizationCachePrerequisiteValidator(services)));

		// Activities and IAuthorizationPolicy have no AddExcaliburA3Core() counterpart, so TryAdd is the
		// whole contract: the first call wins and a second is a no-op.
		services.TryAddSingleton<Activities>();

		// ActivityGroups, UserGrants and IAuthorizationPolicyProvider DO have one. AddExcaliburA3Core()
		// registers each with TryAdd precisely so this composition can displace it, which is why TryAdd
		// here would be wrong twice over: it would hand the seam back to the lighter Core registration --
		// a silent demotion that any arm counting descriptors would still score as idempotent -- while
		// leaving the duplicate this method is fixing. Replace keeps the override and collapses the seam to
		// one descriptor, so the second call changes nothing. None of the three is resolved as an
		// IEnumerable anywhere, so one descriptor is the contract rather than an accident of ordering.
		_ = services.Replace(ServiceDescriptor.Transient<ActivityGroups, ActivityGroups>());
		_ = services.Replace(ServiceDescriptor.Transient<UserGrants, UserGrants>());
		_ = services.Replace(ServiceDescriptor.Scoped<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>());

		// The cache bound is the longest a revoked grant can still authorize, so an unusable value is refused
		// at startup rather than discovered on the first cached read.
		_ = services.AddOptions<AuthorizationCacheOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuthorizationCacheOptions>, AuthorizationCacheOptionsValidator>());

		// A grant sync is a full refresh. On a store that cannot replace a set of grants in one step it can
		// only delete and then insert, which leaves a window in which a user is partly revoked. The default
		// refuses such a store at start-up rather than accepting that window silently; a host that wants it
		// says so, and is warned once.
		_ = services.AddOptions<ActivityGroupSyncOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ActivityGroupSyncOptions>, ActivityGroupSyncOptionsValidator>());

		// One instance behind both registrations, so the BestEffort warning is written once for one
		// composition however many ways start-up validation is reached.
		services.TryAddSingleton(provider => new ActivityGroupGrantSyncPrerequisiteValidator(
			services,
			provider.GetRequiredService<IOptions<ActivityGroupSyncOptions>>(),
			provider.GetRequiredService<ILogger<ActivityGroupGrantSyncPrerequisiteValidator>>()));
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ActivityGroupGrantSyncPrerequisiteValidator>(
				static provider => provider.GetRequiredService<ActivityGroupGrantSyncPrerequisiteValidator>()));
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ActivityGroupGrantSyncPrerequisiteValidator>(
				static provider => provider.GetRequiredService<ActivityGroupGrantSyncPrerequisiteValidator>()));

		services.TryAddScoped<IAuthorizationPolicy>(static container =>
			ResolvePolicySynchronously(container.GetRequiredService<IAuthorizationPolicyProvider>()));

		// AddHttpClient<TClient, TImplementation>() registers the typed client with AddTransient and the
		// HttpClientFactory ships no TryAdd counterpart, so the idempotence guard has to be on the contract
		// rather than on the descriptor.
		if (!AlreadyRegistered<IActivityGroupService>(services))
		{
			_ = services.AddHttpClient<IActivityGroupService, ActivityGroupService>(static (provider, client) =>
			{
				// Read the bound options rather than the process-wide static: the endpoint is per-host
				// configuration, and this lambda already runs against a built provider.
				client.BaseAddress = new Uri(
					provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value.AuthorizationServiceEndpoint);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			});
		}

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

		// Assembly marker for the reflection scan below. AuthorizationPolicy moved to Excalibur.A3.Core
		// (it needed to be reachable from the lightweight AddExcaliburA3Core() composition),
		// so typeof(AuthorizationPolicy).Assembly would now resolve to A3.Core and silently stop scanning
		// this (full) assembly's own handlers. Anchor on a type that is declared here and stays here.
		_ = services.AddDispatchHandlers(typeof(A3ServiceCollectionExtensions).Assembly);

		// AUDIT IS NOT REGISTERED HERE, DELIBERATELY. It used to be, and that made an opt-in feature a
		// prerequisite of authorization: AuditMiddleware takes an IAuditMessagePublisher, which this
		// framework registers nowhere and the consumer must supply, so every A3 consumer had to provide
		// an audit destination or fail to build a container. The middleware then returns straight through
		// for anything that is not IAmAuditable, so most of them were supplying a destination for a
		// pipeline step they never trigger.
		//
		// It also carried a trimming requirement past its own declaration. AuditMiddleware.InvokeAsync and
		// ActivityAudit's Request getter are [RequiresUnreferencedCode]/[RequiresDynamicCode] -- audit
		// serializes the request reflectively -- and four suppressions on those members justify themselves
		// by saying the requirement reaches the consumer at AddAudit, "which registers this type". That was
		// false while this method also registered it, and it was false through an internal extension that
		// carries no annotation of its own, so the analyzer had nothing to attach the requirement to.
		//
		// Consumers who want auditing call the documented opt-in, which is annotated:
		//     services.AddExcalibur(x => x.AddAudit());
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
		if (AlreadyRegistered<IAuthenticationTokenProvider>(services))
		{
			// Same guard as the activity-group client above: AddHttpClient has no TryAdd counterpart, and a
			// second AddExcaliburA3() call must not append a duplicate typed client.
			return services;
		}

		try
		{
			_ = services.AddHttpClient<IAuthenticationTokenProvider, AuthenticationTokenProvider>(static (provider, client) =>
			{
				// Read the bound options rather than the process-wide static: the endpoint is per-host
				// configuration, and this lambda already runs against a built provider.
				client.BaseAddress = new Uri(
					provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value.AuthenticationServiceEndpoint);
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
	/// Reports whether <typeparamref name="TService"/> already has a descriptor, matching what
	/// <c>TryAdd</c> tests. Used where the registration helper is one Microsoft ships no
	/// <c>TryAdd</c> counterpart for.
	/// </summary>
	private static bool AlreadyRegistered<TService>(IServiceCollection services) =>
		services.Any(static descriptor => descriptor.ServiceType == typeof(TService));

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
