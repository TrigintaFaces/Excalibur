// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Defines the interface for routing context implementations.
/// </summary>
/// <remarks> The routing context provides additional information and state that may be needed during routing operations. </remarks>
public interface IRoutingContext
{
	/// <summary>
	/// Gets the timestamp when the routing operation started.
	/// </summary>
	DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets the cancellation token for the routing operation.
	/// </summary>
	CancellationToken CancellationToken { get; }

	/// <summary>
	/// Gets the source endpoint that sent the message.
	/// </summary>
	string? SourceEndpoint { get; }

	/// <summary>
	/// Gets custom properties associated with the routing context.
	/// </summary>
	/// <remarks> Properties can be used to pass additional context or state between different parts of the routing system. </remarks>
	IDictionary<string, object> Properties { get; }

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> The current <see cref="Headers"/> value. </value>
	/// <remarks> Headers contain metadata about the message being routed. </remarks>
	IReadOnlyDictionary<string, object> Headers { get; }

	/// <summary>
	/// Gets the message type being routed.
	/// </summary>
	/// <value> The current <see cref="MessageType"/> value. </value>
	string MessageType { get; }

	/// <summary>
	/// Gets the errors that occurred during routing operations.
	/// </summary>
	IReadOnlyList<string> Errors { get; }

	/// <summary>
	/// Adds an error to the routing context.
	/// </summary>
	/// <param name="errorMessage"> The error message to add. </param>
	/// <remarks> This method allows routing components to record errors without throwing exceptions that would stop the routing process. </remarks>
	void AddError(string errorMessage);
}
