// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Handler activator that resolves handlers through the service provider without runtime code generation.
/// </summary>
/// <remarks>
/// Activation resolves the handler from the container and falls back to constructor injection when it is not registered.
/// The message context is applied through the <see cref="IMessageContextAware" /> interface rather than by reflecting over
/// properties, so no member of the handler is discovered dynamically and the activator is safe under trimming and
/// ahead-of-time compilation. It is reached through <see cref="AotHandlerActivator" />.
/// </remarks>
internal sealed class ServiceProviderHandlerActivator : IHandlerActivator
{
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

		return handler;
	}
}
