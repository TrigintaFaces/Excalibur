// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net.Http.Headers;
using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for Elasticsearch authentication providers that handle secure credential management and authentication token
/// provisioning with enterprise security features.
/// </summary>
public interface IElasticsearchAuthenticationProvider
{
	/// <summary>
	/// Occurs when authentication credentials are rotated successfully.
	/// </summary>
	event EventHandler<AuthenticationRotatedEventArgs>? CredentialsRotated;

	/// <summary>
	/// Occurs when authentication validation fails, enabling security monitoring and alerting.
	/// </summary>
	event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;

	/// <summary>
	/// Gets the authentication type supported by this provider for security auditing and monitoring.
	/// </summary>
	/// <value> The authentication method type implemented by this provider. </value>
	ElasticsearchAuthenticationType AuthenticationType { get; }

	/// <summary>
	/// Gets a value indicating whether this authentication provider supports credential rotation.
	/// </summary>
	/// <value> True if the provider supports automatic credential rotation, false otherwise. </value>
	bool SupportsRotation { get; }

	/// <summary>
	/// Gets a value indicating whether this authentication provider supports token refresh.
	/// </summary>
	/// <value> True if the provider supports authentication token refresh, false otherwise. </value>
	bool SupportsRefresh { get; }

	/// <summary>
	/// Retrieves the current authentication configuration for Elasticsearch client setup.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the authentication configuration or null if
	/// authentication is not configured.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when authentication credentials cannot be retrieved securely. </exception>
	Task<AuthenticationHeaderValue?> GetAuthenticationAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the current authentication credentials are valid and have not expired.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if authentication is valid and active, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when authentication validation fails due to security constraints. </exception>
	Task<bool> ValidateAuthenticationAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Refreshes expired or expiring authentication tokens to maintain continuous secure access.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the authentication was successfully
	/// refreshed, false if refresh is not supported or failed.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when authentication refresh fails due to security constraints. </exception>
	Task<bool> RefreshAuthenticationAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rotates authentication credentials according to security policies and compliance requirements.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the rotation result indicating success, failure, or
	/// if rotation is not applicable.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when credential rotation fails due to security constraints. </exception>
	Task<AuthenticationRotationResult> RotateCredentialsAsync(CancellationToken cancellationToken);
}
