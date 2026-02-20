// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an authentication event for security auditing purposes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthenticationEvent" /> class.
/// </remarks>
/// <param name="eventId"> The unique identifier for this authentication event. </param>
/// <param name="timestamp"> The timestamp when the authentication event occurred. </param>
/// <param name="userId"> The identifier of the user attempting authentication. </param>
/// <param name="authenticationMethod"> The method used for authentication. </param>
/// <param name="result"> The result of the authentication attempt. </param>
public sealed class AuthenticationEvent(
	string eventId,
	DateTimeOffset timestamp,
	string userId,
	string authenticationMethod,
	AuthenticationResult result)
{
	/// <summary>
	/// Gets the unique identifier for this authentication event.
	/// </summary>
	/// <value>
	/// The unique identifier for this authentication event.
	/// </value>
	public string EventId { get; } = eventId ?? throw new ArgumentNullException(nameof(eventId));

	/// <summary>
	/// Gets the timestamp when the authentication event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the authentication event occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; } = timestamp;

	/// <summary>
	/// Gets the identifier of the user attempting authentication.
	/// </summary>
	/// <value>
	/// The identifier of the user attempting authentication.
	/// </value>
	public string UserId { get; } = userId ?? throw new ArgumentNullException(nameof(userId));

	/// <summary>
	/// Gets the method used for authentication.
	/// </summary>
	/// <value>
	/// The method used for authentication.
	/// </value>
	public string AuthenticationMethod { get; } = authenticationMethod ?? throw new ArgumentNullException(nameof(authenticationMethod));

	/// <summary>
	/// Gets the result of the authentication attempt.
	/// </summary>
	/// <value>
	/// The result of the authentication attempt.
	/// </value>
	public AuthenticationResult Result { get; } = result;

	/// <summary>
	/// Gets the IP address from which the authentication attempt originated.
	/// </summary>
	/// <value>
	/// The IP address from which the authentication attempt originated.
	/// </value>
	public string? IpAddress { get; init; }

	/// <summary>
	/// Gets the user agent string from the authentication request.
	/// </summary>
	/// <value>
	/// The user agent string from the authentication request.
	/// </value>
	public string? UserAgent { get; init; }

	/// <summary>
	/// Gets the session identifier associated with the authentication.
	/// </summary>
	/// <value>
	/// The session identifier associated with the authentication.
	/// </value>
	public string? SessionId { get; init; }

	/// <summary>
	/// Gets additional context information about the authentication event.
	/// </summary>
	/// <value>
	/// Additional context information about the authentication event.
	/// </value>
	public IReadOnlyDictionary<string, object>? Context { get; init; }

	/// <summary>
	/// Gets the reason for authentication failure, if applicable.
	/// </summary>
	/// <value>
	/// The reason for authentication failure, if applicable.
	/// </value>
	public string? FailureReason { get; init; }

	/// <summary>
	/// Gets the location from which the authentication attempt originated.
	/// </summary>
	/// <value>
	/// The location from which the authentication attempt originated.
	/// </value>
	public string? Location { get; init; }

	/// <summary>
	/// Gets the unique identifier for this authentication event (alias for EventId).
	/// </summary>
	/// <value>
	/// The unique identifier for this authentication event.
	/// </value>
	public string Id => EventId;

	/// <summary>
	/// Gets a value indicating whether the authentication was successful.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the authentication was successful; otherwise, <see langword="false"/>.
	/// </value>
	public bool Success => Result == AuthenticationResult.Success;
}
