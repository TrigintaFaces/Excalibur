// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

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
	/// Gets a dictionary for storing transport-specific metadata and extensibility data during message processing.
	/// </summary>
	/// <value>The transport-specific and extensibility items dictionary scoped to this message context.</value>
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
	/// </remarks>
	/// <value>The features dictionary keyed by feature interface type.</value>
	IDictionary<Type, object> Features { get; }
}
