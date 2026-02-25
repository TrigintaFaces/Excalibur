// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines the contract for a writable credential store.
/// </summary>
public interface IWritableCredentialStore : ICredentialStore
{
	/// <summary>
	/// Stores a credential in the store.
	/// </summary>
	/// <param name="key">The credential key to store.</param>
	/// <param name="credential">The credential value to persist.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the credential is stored.</returns>
	Task StoreCredentialAsync(string key, SecureString credential, CancellationToken cancellationToken);
}
