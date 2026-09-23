// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

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
	/// <param name="responseType">
	/// The response type the handler produces when <paramref name="expectsResponse" /> is <see langword="true" />.
	/// Optional; a caller that knows the response type up front (a DI descriptor's generic argument, a source
	/// generator's symbol) should supply it so consumers can read it directly instead of recovering it by reflection.
	/// </param>
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
	void Register(Type messageType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType, bool expectsResponse, Type? responseType = null);

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
	bool TryGetHandler(Type messageType, out IHandlerRegistryEntry entry);

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
	IReadOnlyList<IHandlerRegistryEntry> GetAll();

	/// <summary>
	/// Attempts to retrieve EVERY handler registered for the specified message type, reporting separately
	/// whether the registry KNOWS the type at all.
	/// </summary>
	/// <param name="messageType"> The type of message to find handlers for. </param>
	/// <param name="entries"> Every registration for <paramref name="messageType" />; empty when none. </param>
	/// <returns>
	/// <see langword="true"/> when the registry can speak for this message type — including when the honest
	/// answer is that it has no handlers; <see langword="false"/> when the type is unknown to it.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>The return value and the list answer DIFFERENT questions, and collapsing them is a defect this
	/// contract exists to prevent.</b> There are two ways to end up with no entries:
	/// </para>
	/// <list type="bullet">
	/// <item><b><see langword="true"/> with an empty list</b> — the registry knows this message type and the
	/// answer is genuinely "no handlers". An event with no subscribers is the ordinary case, not an error.</item>
	/// <item><b><see langword="false"/></b> — the registry has never heard of this message type. It is not
	/// saying "none"; it is saying it cannot answer, because a composition may dispatch through a path this
	/// registry never saw.</item>
	/// </list>
	/// <para>
	/// One of those is safe to treat as "nothing applies" and the other is not, so a caller that reads only
	/// <c>entries.Count == 0</c> cannot behave correctly. A cross-cutting concern deciding what applies to a
	/// message must branch on the RETURN VALUE and treat <see langword="false"/> as UNDETERMINED.
	/// </para>
	/// <para>
	/// <see cref="TryGetHandler" /> answers with at most ONE handler, which is the right shape for an action
	/// and loses information for an event. The default implementation here filters <see cref="GetAll" />, so
	/// an existing implementation keeps working without change; an implementation holding an index by message
	/// type should override it. An overriding implementation MUST NOT hand out a collection that aliases its
	/// own mutable state — see the note on the framework's own registry.
	/// </para>
	/// </remarks>
	bool TryGetHandlers(Type messageType, out IReadOnlyList<IHandlerRegistryEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var matches = new List<IHandlerRegistryEntry>();
		var known = false;

		foreach (var entry in GetAll())
		{
			if (entry.MessageType == messageType)
			{
				known = true;
				matches.Add(entry);
			}
		}

		entries = matches;

		// The default can only infer knowledge from a registration it can see, so an empty result is reported
		// as UNKNOWN rather than as "none". That is the safe direction: a caller told "undetermined" asks
		// another way, where a caller told "none" may act on it.
		return known;
	}
}
