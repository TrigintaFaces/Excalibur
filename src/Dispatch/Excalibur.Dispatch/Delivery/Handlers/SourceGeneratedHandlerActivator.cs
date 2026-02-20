// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Source-generated handler activator implementation for AOT scenarios.
/// </summary>
/// <remarks>
/// This class is intended to be replaced by source generation. In the meantime, it provides a fallback implementation that uses the service
/// provider for handler activation.
/// </remarks>
internal sealed class SourceGeneratedHandlerActivatorFallback : IHandlerActivator
{
	/// <summary>
	/// Activates a handler instance using the service provider.
	/// </summary>
	/// <param name="handlerType"> The type of handler to activate. </param>
	/// <param name="context"> The message context to inject into the handler. </param>
	/// <param name="provider"> The service provider for dependency Excalibur.Tests.Integration. </param>
	/// <returns> The activated handler instance. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the handler cannot be activated. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2067:'instanceType' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to 'ActivatorUtilities.CreateInstance'",
		Justification =
			"Handler types are registered at startup and preserved through DI registration. This fallback activation is only used when handlers are not resolved from the container. In AOT builds, handlers should be registered via source generation.")]
	public object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		// Note: This service provider-based implementation serves as a fallback for runtime scenarios. In AOT deployments, this class will
		// be replaced by source-generated activation code.
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
