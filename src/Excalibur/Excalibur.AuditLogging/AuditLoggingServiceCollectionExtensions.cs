// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging;
using Excalibur.AuditLogging.Alerting;
using Excalibur.AuditLogging.Annotation;
using Excalibur.AuditLogging.Encryption;
using Excalibur.AuditLogging.Retention;
using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring audit logging services.
/// </summary>
public static class AuditLoggingServiceCollectionExtensions
{
	/// <summary>
	/// The service key the audit-annotation store is registered under, so the access-checking decorator can
	/// resolve whichever store a host configured without naming its type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A provider package registers its annotation store under this key — and under nothing else. It must not
	/// bind <see cref="IAuditAnnotationStore"/> directly: that binding belongs to this package and always
	/// yields the access-checking decorator, so a provider that took it would hand consumers an unchecked
	/// store. Registering here instead means the provider's store is what the decorator wraps, in either call
	/// order, because the key is resolved after all registration has completed.
	/// </para>
	/// <para>
	/// Deliberately <see langword="static" /> <see langword="readonly" /> rather than a <c>const</c>: a public
	/// constant is inlined into every consuming assembly at compile time, so a provider compiled against an
	/// older value would register under a key this package no longer reads — and would do so silently, since
	/// the decorator would simply fall back to its own default store.
	/// </para>
	/// </remarks>
	public static readonly string InnerAuditAnnotationStoreKey = "excalibur.auditlogging.annotations.inner";

	/// <summary>
	/// Adds the default audit logging services with in-memory storage.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// This registers:
	/// - <see cref="IAuditLogger" /> as <see cref="DefaultAuditLogger" />
	/// - <see cref="IAuditStore" /> as <see cref="InMemoryAuditStore" /> (singleton)
	/// </para>
	/// <para> For production, replace <see cref="IAuditStore" /> with a persistent implementation. </para>
	/// </remarks>
	public static IServiceCollection AddAuditLogging(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Shared keyed-MAC + hash-chain integrity strategy + default signing-key provider.
		// Every audit store depends on IAuditIntegrityStrategy to tag/verify records.
		_ = services.AddAuditIntegrity();

		// Register in-memory store as singleton (maintains state across requests).
		// NOTE: no durability attestation is emitted here, and that is deliberate — this store does not
		// survive a restart. A host that needs the trail to outlive the process must register a durable
		// store explicitly; nothing currently refuses this default at startup, so the obligation is the
		// host's own.
		_ = services.AddDefaultTenantContext();
		// Registered through the capability seam rather than a bare TryAddSingleton. InMemoryAuditStore takes
		// ITenantContext, and every read it builds binds the ambient tenant term (the query filter is a
		// scope, not a caller-supplied filter), so the seam derives the ambient-scoping mechanism from the
		// constructor and emits ITenantScopingCapability<IAuditStore> as part of the same act. Without that
		// marker a host wiring this store alongside the row discriminator is refused at startup, because
		// IAuditStore carries [TenantOwned] and nothing attested the store honours it.
		//
		// The estate-wide scope recorded for this provider in ARCHITECTURE.md is chain VERIFICATION only,
		// enumerated per partition. It is not the store's tenancy mechanism, and the partitioned marker
		// would be the wrong attestation here: that one states the tenant is re-established from the row
		// and never inferred from ambient state, which is the opposite of what this store does.
		_ = services.AddTenantAwareStore<IAuditStore, InMemoryAuditStore>();
		services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<InMemoryAuditStore>());

		// Register audit logger as scoped (allows for request-scoped context)
		services.TryAddScoped<IAuditLogger, DefaultAuditLogger>();


		return services;
	}

	/// <summary>
	/// Adds the default audit logging services with a custom audit store.
	/// </summary>
	/// <typeparam name="TAuditStore"> The audit store implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAuditLogging<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAuditStore>(
		this IServiceCollection services)
		where TAuditStore : class, IAuditStore
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register custom store as singleton
		services.TryAddSingleton<IAuditStore, TAuditStore>();

		// Register audit logger as scoped
		services.TryAddScoped<IAuditLogger, DefaultAuditLogger>();

		return services;
	}

	/// <summary>
	/// Adds the default audit logging services with a custom audit store, optional alerting, and optional retention.
	/// </summary>
	/// <typeparam name="TAuditStore"> The audit store implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureAlerts"> Optional action to configure audit alert options. When null, alerting is not registered. </param>
	/// <param name="configureRetention"> Optional action to configure audit retention options. When null, retention is not registered. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAuditLogging<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAuditStore>(
		this IServiceCollection services,
		Action<AuditAlertOptions>? configureAlerts,
		Action<AuditRetentionOptions>? configureRetention = null)
		where TAuditStore : class, IAuditStore
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddAuditLogging<TAuditStore>();

		if (configureAlerts is not null)
		{
			_ = services.AddAuditAlerting(configureAlerts);
		}

		if (configureRetention is not null)
		{
			_ = services.AddAuditRetention(configureRetention);
		}

		return services;
	}

	/// <summary>
	/// Adds the default audit logging services with a factory-provided audit store.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="storeFactory"> The factory to create the audit store. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAuditLogging(
		this IServiceCollection services,
		Func<IServiceProvider, IAuditStore> storeFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(storeFactory);

		// Register factory-provided store as singleton
		services.TryAddSingleton(storeFactory);

		// Register audit logger as scoped
		services.TryAddScoped<IAuditLogger, DefaultAuditLogger>();

		return services;
	}

	/// <summary>
	/// Replaces the audit store registration with a custom implementation.
	/// </summary>
	/// <typeparam name="TAuditStore"> The audit store implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseAuditStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAuditStore>(
		this IServiceCollection services)
		where TAuditStore : class, IAuditStore
	{
		ArgumentNullException.ThrowIfNull(services);

		// Remove existing registrations
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		if (descriptor is not null)
		{
			_ = services.Remove(descriptor);
		}

		// Register custom store
		_ = services.AddSingleton<IAuditStore, TAuditStore>();

		return services;
	}

	/// <summary>
	/// Adds the RBAC audit store decorator for role-based access control.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// This method decorates the existing <see cref="IAuditStore" /> registration with
	/// <see cref="RbacAuditStore" /> to enforce role-based access control.
	/// </para>
	/// <para> Consumers must also register an <see cref="IAuditRoleProvider" /> implementation to provide the current user's role. </para>
	/// <para> Call order matters: Call this after registering the base audit store. </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAuditLogging()
	///     .AddRbacAuditStore();
	/// services.AddScoped&lt;IAuditRoleProvider, MyRoleProvider&gt;();
	/// </code>
	/// </example>
	public static IServiceCollection AddRbacAuditStore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Store the existing IAuditStore registration
		var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore))
								 ?? throw new InvalidOperationException(
									 Resources.AuditLoggingServiceCollectionExtensions_NoAuditStoreRegistrationFound);

		// Remove the existing registration
		_ = services.Remove(existingDescriptor);

		// read implementation members through the keyed-safe accessors once into locals
		// (raw reads throw on keyed descriptors on .NET 8+; locals also preserve null-flow analysis).
		var implementationType = existingDescriptor.GetImplementationType();
		var implementationInstance = existingDescriptor.GetImplementationInstance();
		var implementationFactory = existingDescriptor.GetImplementationFactory();

		// Re-register the original store with a different key (for the decorator to use)
		if (implementationType is not null)
		{
			services.Add(new ServiceDescriptor(
				implementationType,
				implementationType,
				existingDescriptor.Lifetime));
		}
		else if (implementationInstance is not null)
		{
			_ = services.AddSingleton(
				implementationInstance.GetType(),
				implementationInstance);
		}
		else if (implementationFactory is not null)
		{
			// For factory registrations, we need to wrap the factory
			services.Add(new ServiceDescriptor(
				typeof(IAuditStore),
				sp => new RbacAuditStore(
					(IAuditStore)implementationFactory(sp),
					sp.GetRequiredService<IServiceScopeFactory>(),
					sp.GetRequiredService<Logging.ILogger<RbacAuditStore>>()),
				existingDescriptor.Lifetime));

			return services;
		}

		// Register the decorator
		services.Add(new ServiceDescriptor(
			typeof(IAuditStore),
			sp =>
			{
				// Try to resolve the original store type
				var innerStore = implementationType is not null
					? (IAuditStore)sp.GetRequiredService(implementationType)
					: implementationInstance is not null
						? (IAuditStore)sp.GetRequiredService(implementationInstance.GetType())
						: throw new InvalidOperationException(
							Resources.AuditLoggingServiceCollectionExtensions_InnerAuditStoreResolutionFailed);

				// The role, actor and meta-audit logger are NOT resolved here. This descriptor inherits the
				// wrapped store's lifetime, which is a singleton, so resolving them at this point binds them
				// from the root: the decorator would answer with one caller's role and identity for the life
				// of the container, and a host with scope validation on would refuse to start. The store
				// opens a scope per operation instead.
				return new RbacAuditStore(
					innerStore,
					sp.GetRequiredService<IServiceScopeFactory>(),
					sp.GetRequiredService<Logging.ILogger<RbacAuditStore>>());
			},
			existingDescriptor.Lifetime));

		return services;
	}

	/// <summary>
	/// Adds an <see cref="IAuditRoleProvider" /> implementation.
	/// </summary>
	/// <typeparam name="TRoleProvider"> The role provider implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAuditRoleProvider<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TRoleProvider>(
		this IServiceCollection services)
		where TRoleProvider : class, IAuditRoleProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddScoped<IAuditRoleProvider, TRoleProvider>();

		return services;
	}

	/// <summary>
	/// Adds real-time audit alerting services.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An action to configure the audit alert options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers the <see cref="IAuditAlertService" /> with rate limiting
	/// and configurable alert rules. Use <see cref="IAuditAlertService.RegisterRuleAsync" />
	/// to add rules after service construction.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAuditAlerting(
		this IServiceCollection services,
		Action<AuditAlertOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AuditAlertOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditAlertOptions>>(new AuditAlertOptionsValidator()));

		services.TryAddSingleton<IAuditAlertService, DefaultAuditAlertService>();

		return services;
	}

	/// <summary>
	/// Adds real-time audit alerting services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="AuditAlertOptions"/>. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAuditAlerting(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AuditAlertOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditAlertOptions>>(new AuditAlertOptionsValidator()));

		services.TryAddSingleton<IAuditAlertService, DefaultAuditAlertService>();

		return services;
	}

	/// <summary>
	/// Adds automated audit retention services with a background cleanup service.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An action to configure the audit retention options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers the <see cref="IAuditRetentionService" /> and a
	/// <see cref="AuditRetentionBackgroundService" /> that periodically
	/// enforces the configured retention policy. Requires an <see cref="IAuditStore" />
	/// to be registered.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAuditRetention(
		this IServiceCollection services,
		Action<AuditRetentionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AuditRetentionOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditRetentionOptions>>(new AuditRetentionOptionsValidator()));

		services.TryAddSingleton<IAuditRetentionService, DefaultAuditRetentionService>();
		_ = services.AddHostedService<AuditRetentionBackgroundService>();

		// Configuring retention is a production-audit signal: you do not enforce a retention policy over a
		// trail you are willing to lose on restart. Install the startup gate so a volatile audit store FAILS
		// CLOSED here unless the host opted in (AllowVolatileAuditStore = true) or registered a durable store.
		// The bare AddAuditLogging() default stays gate-free (dev/MediatR-replacement) — retention is the
		// distinct "I require a durable trail" composition, per the audit-funnel finding.
		_ = services.AddAuditDurabilityGate();

		return services;
	}

	/// <summary>
	/// Adds automated audit retention services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="AuditRetentionOptions"/>. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAuditRetention(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AuditRetentionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditRetentionOptions>>(new AuditRetentionOptionsValidator()));

		services.TryAddSingleton<IAuditRetentionService, DefaultAuditRetentionService>();
		_ = services.AddHostedService<AuditRetentionBackgroundService>();

		// Configuring retention is a production-audit signal: you do not enforce a retention policy over a
		// trail you are willing to lose on restart. Install the startup gate so a volatile audit store FAILS
		// CLOSED here unless the host opted in (AllowVolatileAuditStore = true) or registered a durable store.
		// The bare AddAuditLogging() default stays gate-free (dev/MediatR-replacement) — retention is the
		// distinct "I require a durable trail" composition, per the audit-funnel finding.
		_ = services.AddAuditDurabilityGate();

		return services;
	}

	/// <summary>
	/// Decorates the existing <see cref="IAuditStore"/> with field-level encryption at rest.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure which fields are encrypted.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method wraps the currently registered <see cref="IAuditStore"/> with an
	/// <see cref="EncryptingAuditEventStore"/> that encrypts configurable fields (ActorId, IpAddress,
	/// Reason, UserAgent) before delegating to the inner store.
	/// </para>
	/// <para>
	/// Requires an <see cref="IEncryptionProvider"/> to be registered. Call this after
	/// registering the base audit store (e.g., <see cref="AddAuditLogging(IServiceCollection)"/>).
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAuditLogging()
	///     .UseAuditLogEncryption(options =>
	///     {
	///         options.EncryptActorId = true;
	///         options.EncryptIpAddress = true;
	///     });
	/// </code>
	/// </example>
	public static IServiceCollection UseAuditLogEncryption(
		this IServiceCollection services,
		Action<AuditEncryptionOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<AuditEncryptionOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditEncryptionOptions>>(new AuditEncryptionOptionsValidator()));
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		RegisterAuditLogEncryptionDecorator(services);

		return services;
	}

	/// <summary>
	/// Decorates the existing <see cref="IAuditStore"/> with field-level encryption at rest
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AuditEncryptionOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection UseAuditLogEncryption(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AuditEncryptionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditEncryptionOptions>>(new AuditEncryptionOptionsValidator()));

		RegisterAuditLogEncryptionDecorator(services);

		return services;
	}

	/// <summary>
	/// Adds audit annotation services with in-memory storage.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers:
	/// <list type="bullet">
	/// <item><see cref="IAuditAnnotationStore"/> as <see cref="InMemoryAuditAnnotationStore"/> (singleton)</item>
	/// <item><see cref="AuditAnnotationOptions"/> with <c>ValidateOnStart</c></item>
	/// </list>
	/// </para>
	/// <para>
	/// Requires an <see cref="IAuditActorProvider"/> registration for actor identity.
	/// For production, replace <see cref="IAuditAnnotationStore"/> with a persistent implementation.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAuditAnnotations(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<AuditAnnotationOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditAnnotationOptions>>(new AuditAnnotationOptionsValidator()));

		services.TryAddSingleton(TimeProvider.System);

		// The inner store is registered under a well-known KEY, and the decorator resolves it by that key at
		// RESOLUTION time rather than binding a concrete type at REGISTRATION time. That distinction is the
		// whole fix: an earlier revision wrapped InMemoryAuditAnnotationStore by name, so a provider package
		// could not participate at all — the decorator either wrapped the in-memory store or, if the provider
		// registered IAuditAnnotationStore first, was not applied. SQL-backed annotations WITH access checks
		// were not expressible in any call order, which is worse than the ordering hazard it looked like.
		//
		// Resolution happens after every registration has run, so call order cannot affect the outcome. A
		// provider registers its store under this key with AddKeyedSingleton and wins over this default in
		// BOTH orders (verified against the container, not assumed: a later keyed registration supersedes an
		// earlier TryAdd of the same key, and TryAdd declines when the key is already taken).
		_ = services.AddDefaultTenantContext();
		services.TryAddKeyedSingleton<IAuditAnnotationStore, InMemoryAuditAnnotationStore>(InnerAuditAnnotationStoreKey);

		// Resolving IAuditAnnotationStore always yields the access-checking decorator, so "I forgot to add the
		// decorator" is not a reachable state — the protection is not something a consumer has to remember. A
		// consumer who genuinely wants the unchecked store asks for it by its key, which makes that decision
		// visible in their code and in review rather than being the silent default.
		//
		// GetRequiredKeyedService is the fail-loud arm and it is free: a host that registered no inner store
		// fails at first resolution with a container error naming the missing service, rather than silently
		// resolving to nothing.
		//
		// The role, actor and meta-audit logger are NOT resolved here. This is a singleton, so resolving
		// them at this point binds them from the root: the decorator would answer with one caller's role
		// and identity for the life of the container -- and since the actor identity decides which
		// annotations a read returns, that hands the first caller's private notes to everyone after them.
		// A host with scope validation on would refuse to start. The store opens a scope per operation.
		services.TryAddSingleton<IAuditAnnotationStore>(sp => new RbacAuditAnnotationStore(
			sp.GetRequiredKeyedService<IAuditAnnotationStore>(InnerAuditAnnotationStoreKey),
			sp.GetRequiredService<IServiceScopeFactory>(),
			sp.GetRequiredService<Logging.ILogger<RbacAuditAnnotationStore>>()));

		// The role provider cannot be defaulted, so its absence fails the host at startup rather than at the
		// first denied read. Registered unconditionally: gating this on anything would reintroduce the
		// ordering dependence it exists to remove.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AuditAnnotationRoleProviderValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, AuditAnnotationRoleProviderValidator>());

		return services;
	}

	/// <summary>
	/// Adds audit annotation services with a custom annotation store.
	/// </summary>
	/// <typeparam name="TStore">The annotation store implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAuditAnnotations<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStore>(
		this IServiceCollection services)
		where TStore : class, IAuditAnnotationStore
	{
		ArgumentNullException.ThrowIfNull(services);

		// Delegates to the parameterless overload so the access-checking decorator is registered here too.
		// This overload previously bound IAuditAnnotationStore straight to TStore, which was a SECOND DOOR
		// to the defect the keyed seam above closes: a consumer calling AddAuditAnnotations<MyStore>() got
		// their store with no role or authorship checks on it, in a package whose parameterless overload
		// promises the opposite. A caller cannot be expected to know that one overload is checked and its
		// sibling is not.
		_ = services.AddAuditAnnotations();

		// TStore is registered under the inner-store KEY, exactly as a provider package does, so it becomes
		// what the decorator wraps rather than what replaces it. AddKeyedSingleton rather than TryAdd: the
		// call above registers the in-memory default under this key, and a TryAdd here would silently lose
		// to it — leaving the consumer's own store unused while everything appeared wired.
		services.AddKeyedSingleton<IAuditAnnotationStore, TStore>(InnerAuditAnnotationStoreKey);

		return services;
	}

	/// <summary>
	/// Adds audit annotation services with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure annotation options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddAuditAnnotations(
		this IServiceCollection services,
		Action<AuditAnnotationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddAuditAnnotations();
		_ = services.Configure(configure);

		return services;
	}

	/// <summary>
	/// Adds scoped audit context services for conditional audit assertions in handlers.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers:
	/// <list type="bullet">
	/// <item><see cref="IAuditContext"/> as <see cref="DefaultAuditContext"/> (scoped)</item>
	/// <item><see cref="AuditContextOptions"/> with <c>ValidateOnStart</c></item>
	/// <item>the dispatch middleware that initializes that context per message</item>
	/// </list>
	/// </para>
	/// <para>
	/// Requires <see cref="IAuditLogger"/> registration (from <see cref="AddAuditLogging(IServiceCollection)"/>).
	/// The registered middleware fills the context with the message's correlation id, tenant and actor
	/// before the handler runs, so a handler that injects <see cref="IAuditContext"/> records entries
	/// attributed to the caller rather than to an uninitialized scope.
	/// </para>
	/// <para>
	/// The context is per-request state, so it is initialized only for a message dispatched with a request
	/// scope. A message dispatched without one is passed through and the gap is logged: the audit context
	/// its handler receives belongs to a scope created for the handler, and reaching for the root provider
	/// instead would share one instance across every message in the process.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAuditContext(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<AuditContextOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditContextOptions>>(new AuditContextOptionsValidator()));

		services.TryAddSingleton(TimeProvider.System);
		services.TryAddScoped<DefaultAuditContext>();
		services.TryAddScoped<IAuditContext>(sp => sp.GetRequiredService<DefaultAuditContext>());

		// The middleware that fills the context. Without it every entry a handler records through
		// IAuditContext carries no correlation id, no tenant and an "unknown" actor, while this method
		// documents the opposite. It holds no per-request state — the context and the actor provider are
		// resolved from the message's own scope on every invocation — so a singleton descriptor is the
		// truthful lifetime for its dependency subtree. TryAddEnumerable avoids double-registration when
		// this extension is called more than once.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, AuditContextMiddleware>());

		return services;
	}

	/// <summary>
	/// Adds scoped audit context services with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure audit context options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddAuditContext(
		this IServiceCollection services,
		Action<AuditContextOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddAuditContext();
		_ = services.Configure(configure);

		return services;
	}

	private static void RegisterAuditLogEncryptionDecorator(IServiceCollection services)
	{
		// Find and replace the existing IAuditStore registration with the encrypting decorator
		var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore))
								 ?? throw new InvalidOperationException(
									 "No IAuditStore registration found. Call AddAuditLogging() first.");

		_ = services.Remove(existingDescriptor);

		// Re-add the inner store under a keyed or typed registration.: read implementation
		// members through the keyed-safe accessors once into locals (preserves null-flow analysis).
		var implementationType = existingDescriptor.GetImplementationType();
		var implementationInstance = existingDescriptor.GetImplementationInstance();
		if (implementationType is not null)
		{
			services.Add(new ServiceDescriptor(
				implementationType,
				implementationType,
				existingDescriptor.Lifetime));

			_ = services.AddSingleton<IAuditStore>(sp => new EncryptingAuditEventStore(
				(IAuditStore)sp.GetRequiredService(implementationType),
				sp.GetRequiredService<IEncryptionProvider>(),
				sp.GetRequiredService<IOptions<AuditEncryptionOptions>>()));
		}
		else if (implementationInstance is not null)
		{
			_ = services.AddSingleton<IAuditStore>(sp => new EncryptingAuditEventStore(
				(IAuditStore)implementationInstance,
				sp.GetRequiredService<IEncryptionProvider>(),
				sp.GetRequiredService<IOptions<AuditEncryptionOptions>>()));
		}
		else if (existingDescriptor.GetImplementationFactory() is not null)
		{
			var factory = existingDescriptor.GetImplementationFactory();
			_ = services.AddSingleton<IAuditStore>(sp => new EncryptingAuditEventStore(
				(IAuditStore)factory(sp),
				sp.GetRequiredService<IEncryptionProvider>(),
				sp.GetRequiredService<IOptions<AuditEncryptionOptions>>()));
		}
	}
}
