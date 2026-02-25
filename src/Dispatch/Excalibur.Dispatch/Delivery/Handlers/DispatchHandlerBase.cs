// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Provides the base implementation for strongly-typed message handlers in the Dispatch messaging system. This abstract class encapsulates
/// common handler infrastructure and provides convenient access to message processing context, validation results, authorization status,
/// and routing information.
/// </summary>
/// <typeparam name="TMessage"> The type of message this handler processes, must implement <see cref="IDispatchAction{TResult}" />. </typeparam>
/// <typeparam name="TResult"> The type of result returned by processing the message. </typeparam>
/// <remarks>
/// This base class follows the Command Query Responsibility Segregation (CQRS) pattern, providing infrastructure for both command handlers
/// (that modify state) and query handlers (that return data). Derived classes inherit pre-processing validation, authorization, and routing
/// checks that can be used to implement early termination or conditional logic based on message processing pipeline results.
/// </remarks>
public abstract class DispatchHandlerBase<TMessage, TResult>
	where TMessage : IDispatchAction<TResult>
{
	/// <summary>
	/// Gets or sets the message processing context containing validation results, authorization status, routing information, and other
	/// metadata accumulated during message pipeline processing.
	/// </summary>
	/// <value>
	/// The <see cref="MessageContext" /> instance that provides access to pipeline processing results and contextual information for the
	/// current message being handled.
	/// </value>
	/// <remarks>
	/// This property is initialized by the message processing pipeline infrastructure and provides handlers with access to pre-processing
	/// results that can inform handler behavior. The context is typically populated before the handler's main processing logic executes.
	/// </remarks>
	/// <value>The current <see cref="Context"/> value.</value>
	public required MessageContext Context { get; set; }

	/// <summary>
	/// Gets a value indicating whether the message passed validation checks during pipeline processing. This property provides a convenient
	/// way to check validation status without directly accessing the context's validation result.
	/// </summary>
	/// <value> <c> true </c> if the message passed all validation rules; otherwise, <c> false </c>. </value>
	/// <remarks>
	/// Handlers can use this property to implement early termination logic or provide different behavior based on validation results.
	/// Invalid messages may still reach handlers depending on pipeline configuration, allowing for custom error handling or logging.
	/// </remarks>
	/// <value>The current <see cref="IsValid"/> value.</value>
	protected bool IsValid => Context.ValidationResult.IsValid;

	/// <summary>
	/// Gets a value indicating whether the current user or context is authorized to process this message. This property provides convenient
	/// access to authorization results from the message pipeline.
	/// </summary>
	/// <value> <c> true </c> if the message processing is authorized; otherwise, <c> false </c>. </value>
	/// <remarks>
	/// Authorization checks are typically performed early in the message processing pipeline based on user identity, roles, claims, or
	/// message-specific authorization rules. Handlers can use this property to implement conditional logic or audit unauthorized access attempts.
	/// </remarks>
	/// <value>The current <see cref="IsAuthorized"/> value.</value>
	protected bool IsAuthorized => Context.AuthorizationResult.IsAuthorized;

	/// <summary>
	/// Gets a value indicating whether the message was successfully routed through the pipeline. This property indicates whether routing
	/// middleware was able to properly process the message and determine the appropriate handler or processing path.
	/// </summary>
	/// <value> <c> true </c> if message routing was successful; otherwise, <c> false </c>. </value>
	/// <remarks>
	/// Routing success typically indicates that the message type is recognized and that appropriate handler registration exists. Failed
	/// routing might indicate misconfiguration, missing handlers, or unsupported message types that may require special handling or error responses.
	/// </remarks>
	/// <value>The current <see cref="IsRouted"/> value.</value>
	protected bool IsRouted => Context.RoutingDecision?.IsSuccess ?? true;
}
