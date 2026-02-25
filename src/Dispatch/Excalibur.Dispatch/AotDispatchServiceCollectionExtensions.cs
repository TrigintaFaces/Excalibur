// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#if USE_SOURCE_GENERATION || PUBLISH_AOT
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AOT-friendly dispatch services.
/// </summary>
public static class AotDispatchServiceCollectionExtensions
{
/// <summary>
/// Configures the dispatch pipeline to use AOT-friendly handler invocation.
/// </summary>
/// <param name="services"> The service collection. </param>
/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAotHandlerInvocation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Replace the standard handler invoker with the AOT version
		_ = services.RemoveAll<IHandlerInvoker>();
		services.TryAddSingleton<IHandlerInvoker, HandlerInvokerAot>();

		return services;
	}

/// <summary>
/// Registers a handler with its AOT-friendly invoker.
/// </summary>
/// <typeparam name="THandler"> The handler type. </typeparam>
/// <typeparam name="TMessage"> The message type. </typeparam>
/// <param name="services"> The service collection. </param>
/// <param name="invoker"> The invoker delegate. </param>
/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddHandlerWithInvoker<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler, TMessage>(
		this IServiceCollection services,
		Func<THandler, TMessage, CancellationToken, Task> invoker)
		where THandler : class
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(invoker);

		// Register the handler
		services.TryAddTransient<THandler>();

		// Register the invoker
		HandlerInvokerRegistry.RegisterInvoker(invoker);

		return services;
	}

/// <summary>
/// Registers a handler with its AOT-friendly invoker that returns a result.
/// </summary>
/// <typeparam name="THandler"> The handler type. </typeparam>
/// <typeparam name="TMessage"> The message type. </typeparam>
/// <typeparam name="TResult"> The result type. </typeparam>
/// <param name="services"> The service collection. </param>
/// <param name="invoker"> The invoker delegate. </param>
/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddHandlerWithInvoker<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler, TMessage, TResult>(
		this IServiceCollection services,
		Func<THandler, TMessage, CancellationToken, Task<TResult>> invoker)
		where THandler : class
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(invoker);

		// Register the handler
		services.TryAddTransient<THandler>();

		// Register the invoker
		HandlerInvokerRegistry.RegisterInvoker(invoker);

		return services;
	}
}

#endif
