// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Handler activator for Native AOT and trimmed applications. Resolves handlers from the dependency-injection
/// container without runtime code generation.
/// </summary>
/// <remarks>
/// <para>
/// This is the activator to use when the application is published with Native AOT. It performs no expression
/// compilation. Context binding prefers <see cref="IMessageContextAware" />, then a setter registered through
/// <see cref="HandlerActivatorRegistry" />, then cached metadata for a writable context property preserved by
/// the annotated handler type. <c>AddDispatch</c> selects this activator when dynamic code is unavailable.
/// </para>
/// <para>
/// To select this activation strategy explicitly, including in a managed application, register it before
/// adding Dispatch. Dispatch registers its runtime-selected default only if none is present:
/// </para>
/// <code>
/// services.AddSingleton&lt;IHandlerActivator, AotHandlerActivator&gt;();
/// services.AddDispatch();
/// </code>
/// <para>
/// Handlers activated this way must either be registered in the container or expose a public constructor whose
/// parameters the container can supply. Interface and registered setters avoid property discovery. The fallback
/// matches <see cref="HandlerActivator" />: the first writable, non-indexed instance property whose type is exactly
/// <see cref="IMessageContext" /> is selected from the requested type's public properties. This includes inherited
/// public properties and private setters on public properties. Metadata is preserved by the activation contract;
/// no runtime code generation is required. Registered setters remain observable after a fallback plan is cached.
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
