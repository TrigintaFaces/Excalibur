// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Represents the result of a single route target, including delivery outcome details.
/// </summary>
internal interface IRouteResult
{
	/// <summary>
	/// Gets the name of the message bus targeted by this route.
	/// </summary>
	string MessageBusName { get; }

	/// <summary>
	/// Gets a value indicating whether this route targets the local bus.
	/// </summary>
	bool IsLocal { get; }

	/// <summary>
	/// Gets or sets additional metadata about the routing decision for this route.
	/// </summary>
	IRouteMetadata? RouteMetadata { get; set; }

	/// <summary>
	/// Gets or sets the delivery status for this route.
	/// </summary>
	RouteDeliveryStatus DeliveryStatus { get; set; }

	/// <summary>
	/// Gets or sets delivery failure details, if any.
	/// </summary>
	RouteFailure? Failure { get; set; }

	/// <summary>
	/// Gets a value indicating whether delivery succeeded for this route.
	/// </summary>
	bool DeliverySucceeded => DeliveryStatus == RouteDeliveryStatus.Succeeded;

	/// <summary>
	/// Gets a value indicating whether delivery failed for this route.
	/// </summary>
	bool DeliveryFailed => DeliveryStatus == RouteDeliveryStatus.Failed;
}
