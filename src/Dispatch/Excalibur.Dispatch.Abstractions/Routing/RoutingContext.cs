// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Provides context for routing decisions.
/// </summary>
/// <remarks>
/// This class provides the complete routing context including message metadata,
/// routing hints, and additional properties for load balancing and route evaluation.
/// </remarks>
public sealed class RoutingContext
{
	/// <summary>
	/// Gets or sets the current timestamp for routing evaluation.
	/// </summary>
	/// <value>The current timestamp for routing evaluation.</value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the cancellation token for the routing operation.
	/// </summary>
	/// <value>The cancellation token for the routing operation.</value>
	public CancellationToken CancellationToken { get; set; }

	/// <summary>
	/// Gets or sets the source of the message.
	/// </summary>
	/// <value>The source of the message.</value>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the source endpoint that sent the message.
	/// </summary>
	/// <value>The source endpoint that sent the message.</value>
	/// <remarks>Alias for <see cref="Source"/> for backward compatibility.</remarks>
	public string? SourceEndpoint
	{
		get => Source;
		set => Source = value;
	}

	/// <summary>
	/// Gets or sets the message type being routed.
	/// </summary>
	/// <value>The message type being routed.</value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	/// <value>The correlation ID.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets additional properties for routing decisions.
	/// </summary>
	/// <value>Additional properties for routing decisions.</value>
	public Dictionary<string, object> Properties { get; } = [];

}
