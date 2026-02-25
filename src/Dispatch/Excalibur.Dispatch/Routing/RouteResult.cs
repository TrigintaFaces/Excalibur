// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Represents the result of a single routed destination.
/// </summary>
internal sealed class RouteResult : IRouteResult
{
	private const string LocalBusName = "Local";

	/// <summary>
	/// Initializes a new instance of the <see cref="RouteResult" /> class.
	/// </summary>
	/// <param name="messageBusName"> The name of the message bus targeted by this route. </param>
	/// <param name="routeMetadata"> Optional routing metadata. </param>
	public RouteResult(string? messageBusName = null, IRouteMetadata? routeMetadata = null)
	{
		MessageBusName = string.IsNullOrWhiteSpace(messageBusName) ? LocalBusName : messageBusName;
		RouteMetadata = routeMetadata;
	}

	/// <inheritdoc/>
	public string MessageBusName { get; }

	/// <inheritdoc/>
	public bool IsLocal => string.Equals(MessageBusName, LocalBusName, StringComparison.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public IRouteMetadata? RouteMetadata { get; set; }

	/// <inheritdoc/>
	public RouteDeliveryStatus DeliveryStatus { get; set; } = RouteDeliveryStatus.NotDispatched;

	/// <inheritdoc/>
	public RouteFailure? Failure { get; set; }
}
