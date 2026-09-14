// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Represents a registered handler entry in the message handler registry, containing metadata about the relationship between a message type
/// and its corresponding handler implementation. This record maintains the essential information needed for message routing and handler activation.
/// </summary>
/// <remarks>
/// Handler registry entries are used by the message dispatching infrastructure to route messages to appropriate handlers based on message
/// type. The entry includes information about whether the handler expects a response, enabling proper handling of both commands and queries
/// in the CQRS pattern implementation.
/// </remarks>
/// <param name="messageType"> The type of message that this handler can process. </param>
/// <param name="handlerType"> The type of the handler implementation that processes the message. </param>
/// <param name="expectsResponse"> Indicates whether this handler returns a response after processing. </param>
/// <param name="responseType">
/// The response type the handler produces when <paramref name="expectsResponse" /> is <see langword="true" />.
/// Optional so every existing 3-argument call site, including the source generator's emitted registration, stays
/// source-compatible; a caller that omits it gets a <see langword="null" /> <see cref="ResponseType" /> and the
/// consumer falls back to the reflective path.
/// </param>
internal sealed class HandlerRegistryEntry(
	Type messageType,
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
	bool expectsResponse,
	Type? responseType = null) : IHandlerRegistryEntry
{
	/// <summary>
	/// Gets the type of message that this handler can process. This type is used for message routing and handler resolution during dispatch operations.
	/// </summary>
	/// <value> A <see cref="Type" /> representing the message type that the registered handler can process. </value>
	/// <remarks>
	/// The message type serves as the primary key for handler lookup during message processing. It must match the generic type parameter of
	/// the handler implementation to ensure type safety during handler invocation.
	/// </remarks>
	public Type MessageType { get; } = messageType;

	/// <summary>
	/// Gets the type of the handler implementation that processes messages of the associated message type. This type is used for handler
	/// activation and dependency injection during message processing.
	/// </summary>
	/// <value> A <see cref="Type" /> representing the handler implementation that will process the message. </value>
	/// <remarks>
	/// The handler type must implement the appropriate handler interface (IDispatchHandler, IDispatchAction, etc.) and be registered in the
	/// dependency injection container for successful activation. This type information enables the framework to instantiate the correct
	/// handler at runtime. DynamicallyAccessedMembers annotation preserves PublicProperties for reflection-based context injection.
	/// </remarks>
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
	public Type HandlerType { get; } = handlerType;

	/// <summary>
	/// Gets a value indicating whether this handler returns a response after processing the message. This property distinguishes between
	/// command handlers (no response) and query handlers (with response).
	/// </summary>
	/// <value> <c> true </c> if the handler returns a response; <c> false </c> if it processes messages without returning data. </value>
	/// <remarks>
	/// This property enables the message processing pipeline to handle command and query patterns differently, ensuring that responses are
	/// properly awaited and returned for query operations while allowing fire-and-forget behavior for command operations. This supports the
	/// CQRS architectural pattern implementation within the messaging framework.
	/// </remarks>
	public bool ExpectsResponse { get; } = expectsResponse;

	/// <summary>
	/// Gets the response type produced by the handler when <see cref="ExpectsResponse" /> is <see langword="true" />,
	/// or <see langword="null" /> when the handler does not return a response, or was registered through a caller
	/// that did not supply it.
	/// </summary>
	/// <remarks>
	/// Carrying the response type here lets direct-action dispatch plan construction read it directly instead of
	/// recovering it by reflecting over <see cref="MessageType" />'s implemented interfaces on every dispatch.
	/// </remarks>
	public Type? ResponseType { get; } = responseType;
}
