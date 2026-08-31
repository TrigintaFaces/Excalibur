// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Handler activator for Native AOT and trimmed applications. Resolves handlers from the dependency-injection
/// container without runtime code generation.
/// </summary>
/// <remarks>
/// <para>
/// This is the activator to use when the application is published with Native AOT. It performs no expression
/// compilation and reflects over no handler member: the handler comes from the service provider, and the message
/// context is applied through <see cref="IMessageContextAware" />. The default activator registered by
/// <c>AddDispatch</c> compiles expressions instead, and refuses to run when dynamic code is unavailable.
/// </para>
/// <para>
/// Register it before adding Dispatch, because Dispatch registers its default activator only if none is present:
/// </para>
/// <code>
/// services.AddSingleton&lt;IHandlerActivator, AotHandlerActivator&gt;();
/// services.AddDispatch();
/// </code>
/// <para>
/// Handlers activated this way must either be registered in the container or expose a public constructor whose
/// parameters the container can supply, and must implement <see cref="IMessageContextAware" /> to receive the
/// message context. Property-injected context is not available on this path, because discovering the property
/// would require the reflection that Native AOT rules out.
/// </para>
/// </remarks>
public sealed class AotHandlerActivator : IHandlerActivator
{
	private static readonly ServiceProviderHandlerActivator Inner = new();

	/// <summary>
	/// Activates a handler instance using the service provider.
	/// </summary>
	/// <param name="handlerType"> The type of handler to activate. </param>
	/// <param name="context"> The message context to inject into the handler. </param>
	/// <param name="provider"> The service provider for dependency resolution. </param>
	/// <returns> The activated handler instance with context injected if applicable. </returns>
	public object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		return Inner.ActivateHandler(handlerType, context, provider);
	}
}
