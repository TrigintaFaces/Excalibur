// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Contains metadata information about a stored secret.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecretMetadata" /> class.
/// </remarks>
/// <param name="description"> A description of the secret's purpose. </param>
/// <param name="expiresAt"> The expiration time for the secret. </param>
/// <param name="tags"> Optional tags for categorizing the secret. </param>
/// <param name="rotationPolicy"> The rotation policy for the secret. </param>
public sealed class SecretMetadata(
	string? description = null,
	DateTimeOffset? expiresAt = null,
	IReadOnlyDictionary<string, string>? tags = null,
	SecretRotationPolicy? rotationPolicy = null)
{
	/// <summary>
	/// Gets the description of the secret's purpose.
	/// </summary>
	/// <value> A human-readable description of what the secret is used for. </value>
	public string? Description { get; } = description;

	/// <summary>
	/// Gets the expiration time for the secret.
	/// </summary>
	/// <value> The UTC timestamp when the secret expires, or null if it doesn't expire. </value>
	public DateTimeOffset? ExpiresAt { get; } = expiresAt;

	/// <summary>
	/// Gets the tags associated with the secret for categorization.
	/// </summary>
	/// <value> A read-only dictionary of key-value pairs for secret categorization. </value>
	public IReadOnlyDictionary<string, string> Tags { get; } = tags ?? new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the rotation policy for the secret.
	/// </summary>
	/// <value> The policy defining how and when the secret should be rotated. </value>
	public SecretRotationPolicy? RotationPolicy { get; } = rotationPolicy;

	/// <summary>
	/// Gets the timestamp when the secret was created.
	/// </summary>
	/// <value> The UTC timestamp of secret creation. </value>
	public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the timestamp when the secret was last modified.
	/// </summary>
	/// <value> The UTC timestamp of the last secret modification. </value>
	public DateTimeOffset LastModifiedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the version of the secret metadata.
	/// </summary>
	/// <value> The version identifier for tracking metadata changes. </value>
	public string Version { get; } = "1.0";
}
