// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Provides context information for a message being processed through the dispatch pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The message context flows through the entire message processing pipeline. It provides:
/// </para>
/// <list type="bullet">
/// <item>Core identity fields (MessageId, CorrelationId, CausationId)</item>
/// <item>The message payload and handler result</item>
/// <item>A scoped service provider for dependency resolution</item>
/// <item>An Items dictionary for middleware data sharing</item>
/// <item>A Features dictionary for typed feature access (processing, validation, routing, etc.)</item>
/// </list>
/// <para>
/// Cross-cutting concerns (identity, routing, processing state, validation, timeout, rate limiting,
/// transactions) are accessed via typed feature interfaces in the <see cref="Features"/> dictionary.
/// Use <c>context.GetFeature&lt;IMessageProcessingFeature&gt;()</c> or the convenience extension methods
/// in <see cref="Features.MessageContextFeatureExtensions"/>.
/// </para>
/// <para>
/// Follows the pattern of <c>Microsoft.AspNetCore.Http.HttpContext</c>: 7 core properties plus
/// a typed feature collection for extensibility.
/// </para>
/// </remarks>
public interface IMessageContext
{
	/// <summary>
	/// Gets or sets the unique identifier for this message instance.
	/// </summary>
	/// <value>The unique identifier assigned to the message.</value>
	string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for tracking related messages.
	/// </summary>
	/// <value>The correlation identifier or <see langword="null"/>.</value>
	string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the causation identifier linking this message to its cause.
	/// </summary>
	/// <value>The causation identifier or <see langword="null"/>.</value>
	string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the message being processed.
	/// </summary>
	/// <value>The message instance or <see langword="null"/>.</value>
	IDispatchMessage? Message { get; set; }

	/// <summary>
	/// Gets or sets the result of processing the message.
	/// </summary>
	/// <value>The handler result or <see langword="null"/>.</value>
	object? Result { get; set; }

	/// <summary>
	/// Gets or sets the scoped service provider for this message processing context.
	/// </summary>
	/// <value>The scoped service provider for this context.</value>
	IServiceProvider RequestServices { get; set; }

	/// <summary>
	/// Gets this context's request-local scratch collection.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>Request-local.</strong> Nothing in this framework serializes, persists, transmits or emits
	/// these entries. To carry a value beyond this process, mark it with
	/// <see cref="MessageContextPropagationExtensions.SetPropagatedItem" /> instead. The two collections are
	/// separate for the reason <see cref="System.Diagnostics.Activity" /> keeps
	/// <see cref="System.Diagnostics.Activity.Tags" /> and
	/// <see cref="System.Diagnostics.Activity.Baggage" /> apart: whether a write crosses a trust boundary
	/// must be decided by the call, never by the spelling of a key.
	/// </para>
	/// <para>
	/// <strong>Contract an implementation must keep.</strong> Returns the one collection owned by this
	/// context. Never <see langword="null" />. <em>The same instance on every call</em>, for the life of
	/// the context. Mutable, and a write is observable to every subsequent read. Returning a fresh
	/// collection per call satisfies the signature and breaks this framework silently — several components
	/// write an entry through one call and read it back through another.
	/// </para>
	/// </remarks>
	/// <value>The request-local items collection owned by this message context.</value>
	IDictionary<string, object> Items { get; }

	/// <summary>
	/// Gets the typed feature collection for accessing cross-cutting concerns.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Features provide typed access to processing state, identity, routing, validation,
	/// timeout, rate limiting, and transaction contexts. Use extension methods from
	/// <see cref="Features.MessageContextFeatureExtensions"/> for convenient access:
	/// </para>
	/// <code>
	/// var processing = context.GetOrCreateProcessingFeature();
	/// processing.ProcessingAttempts++;
	/// </code>
	/// <para>
	/// <strong>Contract an implementation must keep — the same one <see cref="Items" /> states, and now
	/// load-bearing here too.</strong> Returns the one collection owned by this context. Never
	/// <see langword="null" />. <em>The same instance on every call</em>, for the life of the context.
	/// Mutable, and a write is observable to every subsequent read. Returning a fresh collection per
	/// call satisfies the signature and breaks this framework silently: the propagated set
	/// (<see cref="MessageContextPropagationExtensions.SetPropagatedItem" />) is stored here, so a
	/// context that forgets a feature write loses every marked value with nothing raised and nothing
	/// logged.
	/// </para>
	/// </remarks>
	/// <value>The features dictionary keyed by feature interface type.</value>
	IDictionary<Type, object> Features { get; }
}
