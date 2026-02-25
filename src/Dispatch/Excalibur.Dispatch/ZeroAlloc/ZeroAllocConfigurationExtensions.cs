// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Pooling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.ZeroAlloc;

/// <summary>
/// Extension methods for configuring zero-allocation features.
/// </summary>
public static class ZeroAllocConfigurationExtensions
{
	/// <summary>
	/// Enables all zero-allocation optimizations for high-throughput scenarios.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	[RequiresUnreferencedCode("Registers serializers and handlers via reflection which may be trimmed.")]
	public static IDispatchBuilder UseZeroAllocation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register optimized pipeline (DispatchPipeline uses struct-based state machine pattern)
		builder.Services.TryAddSingleton<IDispatchPipeline, DispatchPipeline>();

		// Add zero-alloc serializer
		_ = builder.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);

		// Add message context pool
		builder.Services.TryAddSingleton<IMessageContextPool>(static sp =>
			new MessageContextPool(sp));

		// Replace default factory with pooled factory for zero-allocation context creation
		_ = builder.Services.Replace(ServiceDescriptor.Singleton<Delivery.IMessageContextFactory>(static sp =>
			new PooledMessageContextFactory(sp.GetRequiredService<IMessageContextPool>())));

		// Use optimized handler invoker (the standard HandlerInvoker is now the optimized implementation)
		_ = builder.Services.Replace(ServiceDescriptor.Singleton<IHandlerInvoker, HandlerInvoker>());

		// Add buffer pool if not already registered
		builder.Services.TryAddSingleton<MessageBufferPool>();

		return builder;
	}

	/// <summary>
	/// Adds the zero-allocation JSON serializer.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	[RequiresUnreferencedCode("Registers serializers via reflection which may be trimmed.")]
	public static IDispatchBuilder AddZeroAllocSerializer(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);

		return builder;
	}

	/// <summary>
	/// Configures zero-allocation options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureZeroAllocation(
		this IServiceCollection services,
		Action<ZeroAllocOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		return services;
	}

	/// <summary>
	/// Adds zero-allocation middleware to the pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddZeroAllocMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Add any zero-alloc specific middleware here
		return builder;
	}
}
