// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for authenticating message principals.
/// </summary>
public interface IAuthenticationService
{
	/// <summary>
	/// Authenticates the message and returns the principal.
	/// </summary>
	/// <param name="message"> The message to authenticate. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The authenticated principal or null if authentication fails. </returns>
	Task<ClaimsPrincipal?> AuthenticateAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates an authentication token.
	/// </summary>
	/// <param name="token"> The token to validate. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The claims principal if valid, null otherwise. </returns>
	Task<ClaimsPrincipal?> ValidateTokenAsync(
		string token,
		CancellationToken cancellationToken);

	/// <summary>
	/// Authenticates a bearer token (JWT).
	/// </summary>
	/// <param name="token"> The bearer token to authenticate. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The authenticated principal or null if authentication fails. </returns>
	Task<ClaimsPrincipal?> AuthenticateBearerTokenAsync(string token, CancellationToken cancellationToken);

	/// <summary>
	/// Authenticates an API key.
	/// </summary>
	/// <param name="apiKey"> The API key to authenticate. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The authenticated principal or null if authentication fails. </returns>
	Task<ClaimsPrincipal?> AuthenticateApiKeyAsync(string apiKey, CancellationToken cancellationToken);

	/// <summary>
	/// Authenticates a client certificate.
	/// </summary>
	/// <param name="certificate"> The client certificate to authenticate. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The authenticated principal or null if authentication fails. </returns>
	Task<ClaimsPrincipal?> AuthenticateCertificateAsync(object certificate, CancellationToken cancellationToken);
}
