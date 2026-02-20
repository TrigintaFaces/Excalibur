// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the CredentialsRotated event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthenticationRotatedEventArgs" /> class.
/// </remarks>
/// <param name="authenticationType"> The type of authentication that was rotated. </param>
/// <param name="rotatedAt"> The timestamp when the rotation occurred. </param>
/// <param name="nextRotationDue"> The timestamp when the next rotation is due. </param>
public sealed class AuthenticationRotatedEventArgs(
	ElasticsearchAuthenticationType authenticationType,
	DateTimeOffset rotatedAt,
	DateTimeOffset? nextRotationDue) : EventArgs
{
	/// <summary>
	/// Gets the type of authentication that was rotated.
	/// </summary>
	/// <value> The authentication method that underwent credential rotation. </value>
	public ElasticsearchAuthenticationType AuthenticationType { get; } = authenticationType;

	/// <summary>
	/// Gets the timestamp when the rotation occurred.
	/// </summary>
	/// <value> The UTC timestamp of the credential rotation. </value>
	public DateTimeOffset RotatedAt { get; } = rotatedAt;

	/// <summary>
	/// Gets the timestamp when the next rotation is due.
	/// </summary>
	/// <value> The UTC timestamp for the next scheduled rotation, or null if not applicable. </value>
	public DateTimeOffset? NextRotationDue { get; } = nextRotationDue;
}
