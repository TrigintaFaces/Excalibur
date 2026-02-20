// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents a context transition across a service boundary.
/// </summary>
public sealed class ServiceBoundaryTransition
{
	/// <summary>
	/// Gets or sets the service name.
	/// </summary>
	public required string ServiceName { get; set; }

	/// <summary>
	/// Gets or sets when the transition occurred.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the trace parent at this boundary.
	/// </summary>
	public string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether context was preserved across the boundary.
	/// </summary>
	public bool ContextPreserved { get; set; }
}
