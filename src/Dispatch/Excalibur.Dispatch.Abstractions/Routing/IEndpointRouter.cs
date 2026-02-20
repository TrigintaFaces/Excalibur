// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Service responsible for routing messages to target endpoints (services).
/// </summary>
/// <remarks>
/// <para>
/// The endpoint router determines which services should receive a message.
/// This enables content-based routing, multicast delivery, and conditional routing
/// based on message content, headers, or other criteria.
/// </para>
/// <para>
/// This interface replaces the former <c>IMessageRouter</c> with clearer naming that
/// distinguishes endpoint routing from transport selection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddDispatch(dispatch =>
/// {
///     dispatch.UseRouting(routing =>
///     {
///         routing.Endpoints
///             .Route&lt;OrderCreated&gt;()
///                 .To("billing-service", "inventory-service")
///                 .When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");
///     });
/// });
/// </code>
/// </example>
public interface IEndpointRouter
{
	/// <summary>
	/// Routes a message to its target endpoints based on configured routing rules.
	/// </summary>
	/// <param name="message">The message to route.</param>
	/// <param name="context">The message context containing metadata and state.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	/// A list of endpoint names that should receive the message.
	/// Returns an empty list if no matching rules are found and no fallback is configured.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="context"/> is null.
	/// </exception>
	ValueTask<IReadOnlyList<string>> RouteToEndpointsAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a message can be routed to the specified endpoint.
	/// </summary>
	/// <param name="message">The message to check.</param>
	/// <param name="endpoint">The endpoint name to verify.</param>
	/// <returns>
	/// <see langword="true"/> if any routing rule targets the specified endpoint;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="endpoint"/> is null or empty.
	/// </exception>
	bool CanRouteToEndpoint(IDispatchMessage message, string endpoint);

	/// <summary>
	/// Gets all configured endpoint routes for diagnostic purposes.
	/// </summary>
	/// <param name="message">The message to get routes for.</param>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// Information about all configured endpoint routes, including rule names,
	/// destinations, and priorities.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="context"/> is null.
	/// </exception>
	IEnumerable<RouteInfo> GetEndpointRoutes(IDispatchMessage message, IMessageContext context);
}
