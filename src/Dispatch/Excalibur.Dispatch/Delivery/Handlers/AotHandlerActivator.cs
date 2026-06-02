// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// AOT-compatible handler activator that resolves handlers from the DI container without expression compilation.
/// </summary>
/// <remarks>
/// <para>
/// This implementation avoids runtime expression compilation, making it suitable for Native AOT scenarios.
/// Handlers are resolved from the service provider and context is injected via <see cref="IMessageContextAware"/>.
/// </para>
/// <para>
/// For further optimization, the <c>Excalibur.Dispatch.SourceGenerators</c> package generates a
/// <c>SourceGeneratedHandlerActivator</c> with compile-time switch statements for known handler types.
/// Register it as <see cref="IHandlerActivator"/> in the DI container to replace this default implementation.
/// </para>
/// </remarks>
public sealed class AotHandlerActivator : IHandlerActivator
{
	private static readonly SourceGeneratedHandlerActivatorFallback Inner = new();

	/// <summary>
	/// Activates a handler instance using the service provider.
	/// </summary>
	/// <param name="handlerType"> The type of handler to activate. </param>
	/// <param name="context"> The message context to inject into the handler. </param>
	/// <param name="provider"> The service provider for dependency resolution. </param>
	/// <returns> The activated handler instance with context injected if applicable. </returns>
	[RequiresUnreferencedCode("Handler activation may require reflection to instantiate handler types")]
	public object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		return Inner.ActivateHandler(handlerType, context, provider);
	}
}
