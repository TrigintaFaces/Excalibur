// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
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
	[RequiresDynamicCode(
		"Registers the expression-compiling handler invoker, which generates code at run time.")]
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

		// Use the optimized handler invoker, honoring the same runtime branch as ConfigureHandlerInvoker:
		// the expression-compiling invoker cannot run where dynamic code is unsupported, so on that host
		// the source-generated invoker stands. Enabling this throughput opt-in must not change the
		// consumer's ahead-of-time story.
		_ = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported
			? builder.Services.Replace(ServiceDescriptor.Singleton<IHandlerInvoker, HandlerInvoker>())
			: builder.Services.Replace(ServiceDescriptor.Singleton<IHandlerInvoker, HandlerInvokerAot>());


		return builder;
	}
}
