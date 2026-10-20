// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Handler activator that resolves handlers through the service provider without runtime code generation.
/// </summary>
/// <remarks>
/// Activation resolves the handler from the container and falls back to constructor injection when it is not registered.
/// Context binding prefers <see cref="IMessageContextAware" />, then a registered setter, then cached metadata for a
/// writable context property preserved by the annotated handler type. No expression compilation or runtime code
/// generation is required. It is reached through <see cref="AotHandlerActivator" />.
/// </remarks>
internal sealed class ServiceProviderHandlerActivator : IHandlerActivator
{
	private static readonly ConcurrentDictionary<Type, ContextPropertyPlan> ContextPropertyPlans = new();

	/// <summary>
	/// Activates a handler instance using the service provider.
	/// </summary>
	/// <param name="handlerType"> The type of handler to activate. </param>
	/// <param name="context"> The message context to inject into the handler. </param>
	/// <param name="provider"> The service provider for dependency resolution. </param>
	/// <returns> The activated handler instance. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the handler cannot be activated. </exception>
	public object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		var handler = provider.GetService(handlerType);
		if (handler == null)
		{
			try
			{
				handler = ActivatorUtilities.CreateInstance(provider, handlerType);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException(
					$"Failed to activate handler of type '{handlerType.FullName}'. " +
					"Ensure the handler is registered in the service container or has a public constructor.", ex);
			}
		}

		// Inject context if the handler supports it
		if (handler is IMessageContextAware contextAware)
		{
			contextAware.SetContext(context);
		}
		else
		{
			ApplyRegisteredOrPropertyContext(handlerType, handler, context);
		}

		return handler;
	}

	private static void ApplyRegisteredOrPropertyContext(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType,
		object handler,
		IMessageContext context)
	{
		// Registration remains observable after a reflective or empty plan has been cached.
		if (HandlerActivatorRegistry.TryGetContextSetter(handlerType, out var setter) && setter is not null)
		{
			setter(handler, context);
			return;
		}

		if (!ContextPropertyPlans.TryGetValue(handlerType, out var plan))
		{
			// Discover from the annotated requested type, never an unannotated DI proxy's GetType().
			plan = ContextPropertyPlans.GetOrAdd(handlerType, new ContextPropertyPlan(FindContextProperty(handlerType)));
		}

		plan.Apply(handler, context);
	}

	private static PropertyInfo? FindContextProperty(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		foreach (var property in handlerType.GetProperties())
		{
			// Match HandlerActivator's selection, including a private setter on a public property.
			if (property.SetMethod is { IsStatic: false } &&
				property.GetIndexParameters().Length == 0 &&
				property.PropertyType == typeof(IMessageContext))
			{
				return property;
			}
		}

		return null;
	}

	private sealed class ContextPropertyPlan(PropertyInfo? property)
	{
		public void Apply(object handler, IMessageContext context)
		{
			if (property is null)
			{
				return;
			}

			try
			{
				property.SetValue(handler, context);
			}
			catch (TargetInvocationException exception) when (exception.InnerException is not null)
			{
				// Match direct interface/registered/compiled setters rather than exposing reflection's wrapper.
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			}
		}
	}
}
