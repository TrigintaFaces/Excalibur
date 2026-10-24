// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Defines how a projection processes events.
/// </summary>
public enum ProjectionMode
{
	/// <summary>
	/// The projection runs asynchronously via the <c>AsyncProjectionProcessingHost</c>,
	/// catching up on events in the background. This is the default mode.
	/// </summary>
	Async = 0,

	/// <summary>
	/// The projection is built on-demand by replaying events from the event store
	/// without persisting the result. Suitable for ad-hoc queries and reporting.
	/// </summary>
	Ephemeral = 1,

	/// <summary>
	/// The save drives the ordered durable projection processor through its committed position.
	/// Background recovery is registered automatically; no separate worker application is required.
	/// </summary>
	Inline = 2
}
