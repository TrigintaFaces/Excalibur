// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Represents the delivery status for an individual route.
/// </summary>
internal enum RouteDeliveryStatus
{
	/// <summary>
	/// The route has been selected but not yet dispatched.
	/// </summary>
	NotDispatched,

	/// <summary>
	/// Delivery to this route succeeded.
	/// </summary>
	Succeeded,

	/// <summary>
	/// Delivery to this route failed.
	/// </summary>
	Failed
}
