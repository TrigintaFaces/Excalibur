// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Contains information about a secret without exposing the secret value.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecretInfo" /> class.
/// </remarks>
/// <param name="name"> The name of the secret. </param>
/// <param name="metadata"> The metadata associated with the secret. </param>
public sealed class SecretInfo(string name, SecretMetadata? metadata = null)
{
	/// <summary>
	/// Gets the name of the secret.
	/// </summary>
	/// <value> The unique identifier for the secret. </value>
	public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets the metadata associated with the secret.
	/// </summary>
	/// <value> The metadata for the secret, or null if not available. </value>
	public SecretMetadata? Metadata { get; } = metadata;
}
