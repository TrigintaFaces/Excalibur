// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers closed handler types while preserving context-binding metadata for Native AOT.</summary>
/// <remarks>
/// Dispatch resolves the concrete handler registration. As with ordinary implementation-type DI descriptors,
/// interface and concrete resolutions are independent. An existing concrete registration is preserved.
/// Native invocation separately requires source-generated invokers or closed registrations through
/// <see cref="HandlerInvokerRegistry"/>. These helpers do not discover assemblies or generate invokers at runtime.
/// </remarks>
public static class TypedHandlerServiceCollectionExtensions
{
	/// <summary>Registers a handler for an action with no response.</summary>
	/// <typeparam name="TAction">The action type.</typeparam>
	/// <typeparam name="THandler">The concrete handler type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="lifetime">The handler lifetime, unless its concrete registration already exists.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddActionHandler<TAction,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] THandler>(
		this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
		where TAction : IDispatchAction
		where THandler : class, IActionHandler<TAction> => AddTypedHandler<IActionHandler<TAction>, THandler>(services, lifetime);

	/// <summary>Registers a handler for an action with a response.</summary>
	/// <typeparam name="TAction">The action type.</typeparam>
	/// <typeparam name="TResponse">The response type.</typeparam>
	/// <typeparam name="THandler">The concrete handler type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="lifetime">The handler lifetime, unless its concrete registration already exists.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddActionHandler<TAction, TResponse,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] THandler>(
		this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
		where TAction : IDispatchAction<TResponse>
		where THandler : class, IActionHandler<TAction, TResponse> => AddTypedHandler<IActionHandler<TAction, TResponse>, THandler>(services, lifetime);

	/// <summary>Registers a handler for an event.</summary>
	/// <typeparam name="TEvent">The event type.</typeparam>
	/// <typeparam name="THandler">The concrete handler type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="lifetime">The handler lifetime, unless its concrete registration already exists.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddEventHandler<TEvent,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] THandler>(
		this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
		where TEvent : IDispatchEvent
		where THandler : class, IEventHandler<TEvent> => AddTypedHandler<IEventHandler<TEvent>, THandler>(services, lifetime);

	/// <summary>Registers a handler for a document.</summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <typeparam name="THandler">The concrete handler type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="lifetime">The handler lifetime, unless its concrete registration already exists.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddDocumentHandler<TDocument,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] THandler>(
		this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
		where TDocument : IDispatchDocument
		where THandler : class, IDocumentHandler<TDocument> => AddTypedHandler<IDocumentHandler<TDocument>, THandler>(services, lifetime);

	private static IServiceCollection AddTypedHandler<TService,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] THandler>(
		IServiceCollection services, ServiceLifetime lifetime)
		where TService : class
		where THandler : class, TService
	{
		ArgumentNullException.ThrowIfNull(services);
		if (!Enum.IsDefined(lifetime))
		{
			throw new ArgumentOutOfRangeException(nameof(lifetime));
		}

		services.TryAdd(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));
		services.Add(new PreservedHandlerServiceDescriptor(typeof(TService), typeof(THandler), lifetime));
		return services;
	}
}
