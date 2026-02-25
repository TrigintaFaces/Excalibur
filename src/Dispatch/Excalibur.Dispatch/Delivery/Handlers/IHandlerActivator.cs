// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Defines the contract for handler activation services that create and configure handler instances during message processing. The
/// activator is responsible for instantiating handlers with proper dependency injection and context binding.
/// </summary>
/// <remarks>
/// Handler activators bridge the gap between the handler registry (which knows which handlers exist) and the actual handler instances
/// needed for message processing. They typically integrate with dependency injection containers to provide constructor dependencies and may
/// apply additional configuration or decoration to handler instances based on message context or other factors.
/// </remarks>
public interface IHandlerActivator
{
	/// <summary>
	/// Creates and configures an instance of the specified handler type with dependencies resolved from the service provider and context
	/// information applied as appropriate.
	/// </summary>
	/// <param name="handlerType"> The type of handler to activate, typically obtained from handler registry. </param>
	/// <param name="context"> The message processing context containing metadata and state information. </param>
	/// <param name="provider"> The service provider for resolving handler dependencies through dependency injection. </param>
	/// <returns>
	/// A fully configured handler instance ready for message processing, with all dependencies injected and context information applied
	/// where applicable.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the handler type cannot be instantiated, typically due to missing dependencies or invalid constructor signatures.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Implementations may use various activation strategies including:
	/// - Direct dependency injection container resolution
	/// - Factory pattern activation with custom logic
	/// - Decorator pattern application for cross-cutting concerns
	/// - Context-based configuration or property injection
	/// </para>
	/// <para>
	/// The activator should ensure that handler instances are properly configured for the current message processing context and that all
	/// required dependencies are satisfied before returning.
	/// </para>
	/// </remarks>
	/// <remarks>
	/// Uses DynamicallyAccessedMembers to preserve PublicProperties for reflection-based context injection.
	/// </remarks>
	[RequiresUnreferencedCode("Handler activation may require reflection to instantiate handler types")]
	object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
		IMessageContext context,
		IServiceProvider provider);
}
