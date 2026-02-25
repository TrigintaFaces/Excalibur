// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Unified router that handles both transport selection and endpoint routing.
/// </summary>
/// <remarks>
/// <para>
/// The dispatch router combines <see cref="ITransportSelector"/> and <see cref="IEndpointRouter"/>
/// into a single interface that returns a complete <see cref="RoutingDecision"/> containing
/// both the selected transport and target endpoints.
/// </para>
/// <para>
/// This is the primary routing interface used by <c>RoutingMiddleware</c> to make
/// routing decisions for messages in the dispatch pipeline.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The IDispatchRouter is typically used internally by the middleware.
/// // Configure routing via the fluent builder:
/// services.AddDispatch(dispatch =>
/// {
///     dispatch.UseRouting(routing =>
///     {
///         routing.Transport
///             .Route&lt;OrderCreated&gt;().To("rabbitmq")
///             .Default("local");
///
///         routing.Endpoints
///             .Route&lt;OrderCreated&gt;()
///                 .To("billing-service", "inventory-service");
///     });
/// });
/// </code>
/// </example>
public interface IDispatchRouter
{
	/// <summary>
	/// Determines both transport and endpoint routing for a message.
	/// </summary>
	/// <param name="message">The message to route.</param>
	/// <param name="context">The message context containing metadata and state.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="RoutingDecision"/> containing the selected transport,
	/// target endpoints, and routing metadata.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="context"/> is null.
	/// </exception>
	ValueTask<RoutingDecision> RouteAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if the router can route to a specific destination.
	/// </summary>
	/// <param name="message">The message to check.</param>
	/// <param name="destination">The destination name (transport or endpoint).</param>
	/// <returns>
	/// <see langword="true"/> if the destination is configured as either a transport
	/// or an endpoint for the message; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="destination"/> is null or empty.
	/// </exception>
	bool CanRouteTo(IDispatchMessage message, string destination);

	/// <summary>
	/// Gets all available routes for a message (for diagnostics).
	/// </summary>
	/// <param name="message">The message to get routes for.</param>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// Information about all available routes including transport and endpoint routes.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="context"/> is null.
	/// </exception>
	IEnumerable<RouteInfo> GetAvailableRoutes(IDispatchMessage message, IMessageContext context);
}
