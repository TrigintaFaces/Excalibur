// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines the contract for a credential store.
/// </summary>
public interface ICredentialStore
{
	/// <summary>
	/// Retrieves a credential from the store.
	/// </summary>
	/// <param name="key">The credential key to retrieve.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the retrieved credential, if available.</returns>
	Task<SecureString?> GetCredentialAsync(string key, CancellationToken cancellationToken);
}
