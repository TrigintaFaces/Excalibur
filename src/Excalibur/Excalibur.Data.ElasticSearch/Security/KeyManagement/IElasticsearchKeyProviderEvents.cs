// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines events and secret listing operations for key provider audit and monitoring.
/// </summary>
public interface IElasticsearchKeyProviderEvents
{
	/// <summary>
	/// Occurs when a secret is accessed, for audit and monitoring purposes.
	/// </summary>
	event EventHandler<SecretAccessedEventArgs>? SecretAccessed;

	/// <summary>
	/// Occurs when a key rotation is completed successfully.
	/// </summary>
	event EventHandler<KeyRotatedEventArgs>? KeyRotated;

	/// <summary>
	/// Lists all secrets managed by this provider, optionally filtered by prefix.
	/// </summary>
	/// <param name="prefix"> Optional prefix to filter secret names. If null, all secrets are returned. </param>
	/// <param name="includeMetadata"> Whether to include metadata in the results. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the list of secret information matching the
	/// specified criteria.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when secret listing fails due to security constraints. </exception>
	Task<IReadOnlyList<SecretInfo>> ListSecretsAsync(string? prefix, bool includeMetadata,
		CancellationToken cancellationToken);
}
