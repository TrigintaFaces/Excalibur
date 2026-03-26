// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines how a projection processes events.
/// </summary>
public enum ProjectionMode
{
	/// <summary>
	/// The projection runs asynchronously via the <c>GlobalStreamProjectionHost</c>,
	/// catching up on events in the background. This is the default mode.
	/// </summary>
	Async = 0,

	/// <summary>
	/// The projection runs inline during <c>SaveAsync</c>, providing immediate
	/// read-after-write consistency. Events are applied and persisted before
	/// <c>SaveAsync</c> returns to the caller.
	/// </summary>
	Inline = 1,

	/// <summary>
	/// The projection is built on-demand by replaying events from the event store
	/// without persisting the result. Suitable for ad-hoc queries and reporting.
	/// </summary>
	Ephemeral = 2
}
