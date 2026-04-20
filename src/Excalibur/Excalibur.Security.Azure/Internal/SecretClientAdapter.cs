// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Excalibur.Security.Azure.Internal;

/// <summary>
/// Default <see cref="ISecretClient"/> implementation that forwards to a real
/// <see cref="SecretClient"/>. This adapter is intentionally the only place in
/// the framework that touches live Azure Key Vault SDK call sites — tests do
/// not fake <see cref="SecretClient"/> directly (see bd-wy56o5, ADR-142).
/// </summary>
internal sealed class SecretClientAdapter : ISecretClient
{
	private readonly SecretClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecretClientAdapter"/> class.
	/// </summary>
	/// <param name="inner"> The underlying Azure Key Vault secret client. </param>
	public SecretClientAdapter(SecretClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task<Response<KeyVaultSecret>> GetSecretAsync(string name, CancellationToken cancellationToken)
		=> _inner.GetSecretAsync(name, version: null, cancellationToken: cancellationToken);

	/// <inheritdoc/>
	public Task<Response<KeyVaultSecret>> SetSecretAsync(KeyVaultSecret secret, CancellationToken cancellationToken)
		=> _inner.SetSecretAsync(secret, cancellationToken);
}
