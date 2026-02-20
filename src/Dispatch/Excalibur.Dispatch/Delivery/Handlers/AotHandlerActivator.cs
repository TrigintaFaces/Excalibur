// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// AOT-compatible handler activator that uses source-generated code instead of expression compilation.
/// </summary>
/// <remarks>
/// This implementation avoids reflection and expression compilation, making it suitable for Native AOT scenarios. It delegates to a
/// source-generated activator that contains compile-time generated switch statements for known handler types.
/// </remarks>
public sealed class AotHandlerActivator : IHandlerActivator
{
	private static readonly IHandlerActivator GeneratedActivator = new SourceGeneratedHandlerActivator();
	private static readonly IHandlerActivator FallbackActivator = new SourceGeneratedHandlerActivatorFallback();

	/// <summary>
	/// Activates a handler instance using AOT-compatible source-generated code.
	/// </summary>
	/// <param name="handlerType"> The type of handler to activate. </param>
	/// <param name="context"> The message context to inject into the handler. </param>
	/// <param name="provider"> The service provider for dependency Excalibur.Tests.Integration. </param>
	/// <returns> The activated handler instance with context injected if applicable. </returns>
	/// <remarks>
	/// This method uses source-generated code to avoid reflection and expression compilation. The source generator scans all handler types
	/// at compile time and generates optimized activation code for each known handler type.
	/// </remarks>
	[RequiresUnreferencedCode("Handler activation may require reflection to instantiate handler types")]
	public object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		IMessageContext context,
		IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(provider);

		try
		{
			return GeneratedActivator.ActivateHandler(handlerType, context, provider);
		}
		catch (InvalidOperationException)
		{
			return FallbackActivator.ActivateHandler(handlerType, context, provider);
		}
	}
}
