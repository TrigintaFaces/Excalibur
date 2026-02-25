// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Fluent builder for configuring fallback routing behavior.
/// </summary>
/// <remarks>
/// <para>
/// Fallback routes are used when no other routing rules match a message.
/// This provides a safety net to ensure messages are not lost and can
/// be routed to dead-letter queues or default handlers.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// routing.Fallback
///     .To("dead-letter-queue")
///     .WithReason("No matching routing rules");
/// </code>
/// </example>
public interface IFallbackRoutingBuilder
{
	/// <summary>
	/// Specifies the fallback endpoint for unrouted messages.
	/// </summary>
	/// <param name="endpoint">The fallback endpoint name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="endpoint"/> is null or whitespace.
	/// </exception>
	IFallbackRoutingBuilder To(string endpoint);

	/// <summary>
	/// Specifies a reason to include in routing diagnostics when fallback is used.
	/// </summary>
	/// <param name="reason">The diagnostic reason.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="reason"/> is null or whitespace.
	/// </exception>
	IFallbackRoutingBuilder WithReason(string reason);

	/// <summary>
	/// Gets the configured fallback endpoint.
	/// </summary>
	/// <value>The fallback endpoint name, or <see langword="null"/> if not configured.</value>
	string? Endpoint { get; }

	/// <summary>
	/// Gets the configured fallback reason.
	/// </summary>
	/// <value>The diagnostic reason, or <see langword="null"/> if not configured.</value>
	string? Reason { get; }
}
