// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the SecretAccessed event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecretAccessedEventArgs" /> class.
/// </remarks>
/// <param name="secretName"> The name of the accessed secret. </param>
/// <param name="operation"> The type of operation performed. </param>
/// <param name="accessedAt"> The timestamp when the secret was accessed. </param>
/// <param name="userId"> The identifier of the user or service that accessed the secret. </param>
public sealed class SecretAccessedEventArgs(string secretName, SecretOperation operation, DateTimeOffset accessedAt, string? userId = null)
	: EventArgs
{
	/// <summary>
	/// Gets the name of the accessed secret.
	/// </summary>
	/// <value> The unique identifier for the secret that was accessed. </value>
	public string SecretName { get; } = secretName ?? throw new ArgumentNullException(nameof(secretName));

	/// <summary>
	/// Gets the type of operation performed on the secret.
	/// </summary>
	/// <value> The operation that was performed. </value>
	public SecretOperation Operation { get; } = operation;

	/// <summary>
	/// Gets the timestamp when the secret was accessed.
	/// </summary>
	/// <value> The UTC timestamp of the access operation. </value>
	public DateTimeOffset AccessedAt { get; } = accessedAt;

	/// <summary>
	/// Gets the identifier of the user or service that accessed the secret.
	/// </summary>
	/// <value> The user or service identifier, or null if not available. </value>
	public string? UserId { get; } = userId;
}
