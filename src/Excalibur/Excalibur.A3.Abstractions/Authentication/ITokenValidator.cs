// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authentication;

/// <summary>
/// Validates opaque or structured authentication tokens and produces a principal.
/// </summary>
public interface ITokenValidator
{
	/// <summary>
	/// Validates the supplied token and returns an <see cref="AuthenticationResult" />.
	/// </summary>
	/// <param name="token"> The token to validate. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The <see cref="AuthenticationResult" /> for the validation. </returns>
	Task<AuthenticationResult> ValidateAsync(string token, CancellationToken cancellationToken);
}
