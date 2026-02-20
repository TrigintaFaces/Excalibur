// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the AuthenticationFailed event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthenticationFailedEventArgs" /> class.
/// </remarks>
/// <param name="authenticationType"> The type of authentication that failed. </param>
/// <param name="failureReason"> The reason for the authentication failure. </param>
/// <param name="failedAt"> The timestamp when the failure occurred. </param>
/// <param name="consecutiveFailures"> The number of consecutive failures for this authentication type. </param>
public sealed class AuthenticationFailedEventArgs(
	ElasticsearchAuthenticationType authenticationType,
	string failureReason,
	DateTimeOffset failedAt,
	int consecutiveFailures) : EventArgs
{
	/// <summary>
	/// Gets the type of authentication that failed.
	/// </summary>
	/// <value> The authentication method that failed validation. </value>
	public ElasticsearchAuthenticationType AuthenticationType { get; } = authenticationType;

	/// <summary>
	/// Gets the reason for the authentication failure.
	/// </summary>
	/// <value> A descriptive message explaining why authentication failed. </value>
	public string FailureReason { get; } = failureReason;

	/// <summary>
	/// Gets the timestamp when the failure occurred.
	/// </summary>
	/// <value> The UTC timestamp of the authentication failure. </value>
	public DateTimeOffset FailedAt { get; } = failedAt;

	/// <summary>
	/// Gets the number of consecutive failures for this authentication type.
	/// </summary>
	/// <value> The count of consecutive authentication failures, useful for security monitoring. </value>
	public int ConsecutiveFailures { get; } = consecutiveFailures;
}
