// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the distributed-tracing observability metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/>. Carries the W3C trace context fields. Holds at most
/// ten properties to satisfy the Microsoft-first focused-value-type design guideline.
/// </remarks>
public readonly record struct MessageObservability
{
	/// <summary>
	/// Gets the W3C trace parent identifier for distributed tracing.
	/// </summary>
	/// <value> The W3C trace parent or <see langword="null"/>. </value>
	public string? TraceParent { get; init; }

	/// <summary>
	/// Gets the W3C trace state information for distributed tracing.
	/// </summary>
	/// <value> The W3C trace state or <see langword="null"/>. </value>
	public string? TraceState { get; init; }

	/// <summary>
	/// Gets the W3C baggage information for cross-service communication.
	/// </summary>
	/// <value> The W3C baggage or <see langword="null"/>. </value>
	public string? Baggage { get; init; }
}
