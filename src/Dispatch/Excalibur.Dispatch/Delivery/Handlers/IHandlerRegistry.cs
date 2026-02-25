// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Defines the contract for handler registry services that maintain the mapping between message types and their corresponding handler
/// implementations. The registry serves as the central repository for handler metadata used during message routing and processing operations.
/// </summary>
/// <remarks>
/// The handler registry is a core component of the message dispatching infrastructure, enabling dynamic handler resolution based on message
/// types. It supports both compile-time registration (through reflection-based assembly scanning) and runtime registration for flexible
/// handler management in various application scenarios including modular architectures and plugin systems.
/// </remarks>
public interface IHandlerRegistry
{
	/// <summary>
	/// Registers a handler type for processing messages of the specified type, including metadata about whether the handler produces a
	/// response result.
	/// </summary>
	/// <param name="messageType"> The type of message that the handler can process. </param>
	/// <param name="handlerType"> The type of the handler implementation that will process the message. </param>
	/// <param name="expectsResponse"> Indicates whether the handler returns a response after processing. </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="messageType" /> or <paramref name="handlerType" /> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the handler type doesn't implement appropriate handler interfaces for the message type.
	/// </exception>
	/// <remarks>
	/// Registration establishes the relationship between message types and their handlers, enabling the message processing pipeline to
	/// route messages to appropriate handlers. The registry may overwrite existing registrations for the same message type, allowing
	/// handler replacement or modification during application lifecycle. Handler types should implement appropriate interfaces
	/// (IActionHandler, IEventHandler, etc.) to be compatible with the processing pipeline.
	/// </remarks>
	void Register(Type messageType, Type handlerType, bool expectsResponse);

	/// <summary>
	/// Attempts to retrieve the handler registration information for the specified message type. This method provides efficient handler
	/// lookup during message processing operations.
	/// </summary>
	/// <param name="messageType"> The type of message to find a handler for. </param>
	/// <param name="entry">
	/// When the method returns, contains the handler registry entry if found; otherwise, the default value for the type.
	/// </param>
	/// <returns> <c> true </c> if a handler is registered for the message type; otherwise, <c> false </c>. </returns>
	/// <remarks>
	/// This method provides fast handler resolution with minimal allocation overhead, making it suitable for high-throughput message
	/// processing scenarios. The returned entry contains all metadata needed for handler activation and invocation, including response
	/// expectations and handler type information.
	/// </remarks>
	bool TryGetHandler(Type messageType, out HandlerRegistryEntry entry);

	/// <summary>
	/// Retrieves all handler registrations currently maintained by the registry. This method is primarily used for diagnostics,
	/// configuration validation, and tooling scenarios.
	/// </summary>
	/// <returns>
	/// A read-only collection of all handler registry entries, providing access to complete handler mapping information for the current
	/// application configuration.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The returned collection represents a snapshot of current registrations and may be used for various purposes including:
	/// - Application startup validation and diagnostics
	/// - Development tooling and handler discovery
	/// - Runtime introspection and monitoring
	/// - Configuration documentation generation
	/// </para>
	/// <para>Changes to the registry after calling this method are not reflected in the returned collection.</para>
	/// </remarks>
	IReadOnlyList<HandlerRegistryEntry> GetAll();
}
