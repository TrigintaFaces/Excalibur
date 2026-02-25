// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Extension methods for configuring caching on an <see cref="IDispatchBuilder" />.
/// </summary>
public static class CachingDispatchBuilderExtensions
{
	/// <summary>
	/// Adds caching middleware to the builder's service collection.
	/// </summary>
	/// <param name="builder"> The <see cref="IDispatchBuilder" /> to configure. </param>
	/// <returns> The configured <see cref="IDispatchBuilder" />. </returns>
	public static IDispatchBuilder AddDispatchCaching(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchCaching();

		return builder;
	}

	/// <summary>
	/// Adds Dispatch caching (memory, distributed, hybrid) via the builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Optional action to configure cache options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddCaching();
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder AddCaching(
		this IDispatchBuilder builder,
		Action<CacheOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure != null)
		{
			_ = builder.Services.AddDispatchCaching(configure);
		}
		else
		{
			_ = builder.Services.AddDispatchCaching();
		}

		return builder;
	}

	/// <summary>
	/// Configures caching with a custom options delegate.
	/// </summary>
	/// <param name="builder"> The <see cref="IDispatchBuilder" /> to configure. </param>
	/// <param name="configure"> Callback used to set <see cref="CacheOptions" />. </param>
	/// <returns> The configured <see cref="IDispatchBuilder" />. </returns>
	public static IDispatchBuilder WithCachingOptions(
		this IDispatchBuilder builder,
		Action<CacheOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new CacheOptions();
		configure(options);

		if (options.GlobalPolicy is not null)
		{
			_ = builder.Services.AddSingleton(options.GlobalPolicy);
		}

		if (options.CacheKeyBuilder is not null)
		{
			_ = builder.Services.AddSingleton(options.CacheKeyBuilder);
		}

		_ = builder.Services.AddOptions<CacheOptions>()
			.Configure(opts =>
			{
				opts.Enabled = options.Enabled;
				opts.CacheMode = options.CacheMode;
				opts.Behavior.DefaultExpiration = options.Behavior.DefaultExpiration;
				opts.DefaultTags = options.DefaultTags;
				opts.Behavior.CacheTimeout = options.Behavior.CacheTimeout;
				opts.GlobalPolicy = options.GlobalPolicy;
				opts.CacheKeyBuilder = options.CacheKeyBuilder;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder;
	}

	/// <summary>
	/// Binds <see cref="CacheOptions" /> from configuration.
	/// </summary>
	/// <param name="builder"> The <see cref="IDispatchBuilder" /> to configure. </param>
	/// <param name="configuration"> The configuration section to bind. </param>
	/// <returns> The configured <see cref="IDispatchBuilder" />. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for CacheOptions requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder WithCachingOptions(this IDispatchBuilder builder, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOptions<CacheOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return builder;
	}

	/// <summary>
	/// Adds a global result cache policy.
	/// </summary>
	/// <param name="builder"> The <see cref="IDispatchBuilder" /> to configure. </param>
	/// <param name="policy"> Delegate that decides whether a result should be cached. </param>
	/// <returns> The configured <see cref="IDispatchBuilder" />. </returns>
	public static IDispatchBuilder WithResultCachePolicy(this IDispatchBuilder builder, Func<IDispatchMessage, object?, bool> policy)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSingleton<IResultCachePolicy>(new DefaultResultCachePolicy(policy));
		return builder;
	}

	/// <summary>
	/// Adds a typed result cache policy for <typeparamref name="TMessage" />.
	/// </summary>
	/// <typeparam name="TMessage"> The message type. </typeparam>
	/// <param name="builder"> The <see cref="IDispatchBuilder" /> to configure. </param>
	/// <param name="shouldCache"> Delegate that decides whether a result should be cached. </param>
	/// <returns> The configured <see cref="IDispatchBuilder" />. </returns>
	public static IDispatchBuilder WithResultCachePolicy<TMessage>(
		this IDispatchBuilder builder,
		Func<TMessage, object?, bool> shouldCache)
		where TMessage : class, IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(shouldCache);

		_ = builder.Services.AddSingleton<IResultCachePolicy<TMessage>>(new TypedResultCachePolicy<TMessage>(shouldCache));
		return builder;
	}

	/// <summary>
	/// Registers a custom result cache policy implementation.
	/// </summary>
	/// <typeparam name="TMessage"> The message type. </typeparam>
	/// <typeparam name="TPolicy"> The policy implementation. </typeparam>
	/// <param name="builder"> The <see cref="IDispatchBuilder" /> to configure. </param>
	/// <returns> The configured <see cref="IDispatchBuilder" />. </returns>
	public static IDispatchBuilder WithResultCachePolicy<TMessage,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TPolicy>(this IDispatchBuilder builder)
		where TMessage : class, IDispatchMessage
		where TPolicy : class, IResultCachePolicy<TMessage>
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSingleton<IResultCachePolicy<TMessage>, TPolicy>();
		return builder;
	}

	// Note: Projection tag resolver registration methods (WithResolvers, WithResolversFromAssembly) have been
	// moved to Excalibur.Caching.Projections as part of Sprint 330 T1.2 (AD-330).
	// Use ExcaliburDispatchBuilderExtensions.WithProjectionResolvers() and WithProjectionResolversFromAssembly() instead.

	private sealed class TypedResultCachePolicy<TMessage>(Func<TMessage, object?, bool> shouldCache) : IResultCachePolicy<TMessage>
		where TMessage : class, IDispatchMessage
	{
		public bool ShouldCache(TMessage message, object? result) => shouldCache(message, result);
	}
}
