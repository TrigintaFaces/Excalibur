// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging;
using Excalibur.Dispatch.AuditLogging.Alerting;
using Excalibur.Dispatch.AuditLogging.Encryption;
using Excalibur.Dispatch.AuditLogging.Retention;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring audit logging services.
/// </summary>
public static class AuditLoggingServiceCollectionExtensions
{
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

		// Register in-memory store as singleton (maintains state across requests)
		services.TryAddSingleton<InMemoryAuditStore>();
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

		// Re-register the original store with a different key (for the decorator to use)
		if (existingDescriptor.ImplementationType is not null)
		{
			services.Add(new ServiceDescriptor(
				existingDescriptor.ImplementationType,
				existingDescriptor.ImplementationType,
				existingDescriptor.Lifetime));
		}
		else if (existingDescriptor.ImplementationInstance is not null)
		{
			_ = services.AddSingleton(
				existingDescriptor.ImplementationInstance.GetType(),
				existingDescriptor.ImplementationInstance);
		}
		else if (existingDescriptor.ImplementationFactory is not null)
		{
			// For factory registrations, we need to wrap the factory
			services.Add(new ServiceDescriptor(
				typeof(IAuditStore),
				sp => new RbacAuditStore(
					(IAuditStore)existingDescriptor.ImplementationFactory(sp),
					sp.GetRequiredService<IAuditRoleProvider>(),
					sp.GetRequiredService<Logging.ILogger<RbacAuditStore>>(),
					sp.GetService<IAuditActorProvider>(),
					sp.GetService<IAuditLogger>()),
				existingDescriptor.Lifetime));

			return services;
		}

		// Register the decorator
		services.Add(new ServiceDescriptor(
			typeof(IAuditStore),
			sp =>
			{
				// Try to resolve the original store type
				var innerStore = existingDescriptor.ImplementationType is not null
					? (IAuditStore)sp.GetRequiredService(existingDescriptor.ImplementationType)
					: existingDescriptor.ImplementationInstance is not null
						? (IAuditStore)sp.GetRequiredService(existingDescriptor.ImplementationInstance.GetType())
						: throw new InvalidOperationException(
							Resources.AuditLoggingServiceCollectionExtensions_InnerAuditStoreResolutionFailed);

				return new RbacAuditStore(
					innerStore,
					sp.GetRequiredService<IAuditRoleProvider>(),
					sp.GetRequiredService<Logging.ILogger<RbacAuditStore>>(),
					sp.GetService<IAuditActorProvider>(),
					sp.GetService<IAuditLogger>());
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
			.ValidateDataAnnotations()
			.ValidateOnStart();

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
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<IAuditRetentionService, DefaultAuditRetentionService>();
		_ = services.AddHostedService<AuditRetentionBackgroundService>();

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
	/// registering the base audit store (e.g., <see cref="AddAuditLogging()"/>).
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

		_ = services.AddOptions<AuditEncryptionOptions>();
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		// Find and replace the existing IAuditStore registration with the encrypting decorator
		var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore))
								 ?? throw new InvalidOperationException(
									 "No IAuditStore registration found. Call AddAuditLogging() first.");

		_ = services.Remove(existingDescriptor);

		// Re-add the inner store under a keyed or typed registration
		if (existingDescriptor.ImplementationType is not null)
		{
			services.Add(new ServiceDescriptor(
				existingDescriptor.ImplementationType,
				existingDescriptor.ImplementationType,
				existingDescriptor.Lifetime));

			_ = services.AddSingleton<IAuditStore>(sp => new EncryptingAuditEventStore(
				(IAuditStore)sp.GetRequiredService(existingDescriptor.ImplementationType),
				sp.GetRequiredService<IEncryptionProvider>(),
				sp.GetRequiredService<IOptions<AuditEncryptionOptions>>()));
		}
		else if (existingDescriptor.ImplementationInstance is not null)
		{
			_ = services.AddSingleton<IAuditStore>(sp => new EncryptingAuditEventStore(
				(IAuditStore)existingDescriptor.ImplementationInstance,
				sp.GetRequiredService<IEncryptionProvider>(),
				sp.GetRequiredService<IOptions<AuditEncryptionOptions>>()));
		}
		else if (existingDescriptor.ImplementationFactory is not null)
		{
			var factory = existingDescriptor.ImplementationFactory;
			_ = services.AddSingleton<IAuditStore>(sp => new EncryptingAuditEventStore(
				(IAuditStore)factory(sp),
				sp.GetRequiredService<IEncryptionProvider>(),
				sp.GetRequiredService<IOptions<AuditEncryptionOptions>>()));
		}

		return services;
	}
}
