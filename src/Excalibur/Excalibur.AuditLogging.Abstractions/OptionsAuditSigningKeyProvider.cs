// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging;

/// <summary>
/// Default <see cref="IAuditSigningKeyProvider"/> that sources the audit signing key from
/// <see cref="AuditIntegrityOptions"/>. It performs no key generation: when no key is configured it fails
/// closed (throws on the compute path, returns <see langword="null"/> on the verify path) so audit
/// integrity is never silently downgraded to an unprotected or fabricated value.
/// </summary>
/// <remarks>
/// Production deployments should register a KMS / secret-manager-backed <see cref="IAuditSigningKeyProvider"/>
/// instead; this options-backed default keeps the contract honest without inventing key material.
/// </remarks>
internal sealed class OptionsAuditSigningKeyProvider : IAuditSigningKeyProvider
{
	private readonly IOptions<AuditIntegrityOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="OptionsAuditSigningKeyProvider"/> class.
	/// </summary>
	/// <param name="options">The audit-integrity options carrying the optional signing key and key identifier.</param>
	public OptionsAuditSigningKeyProvider(IOptions<AuditIntegrityOptions> options) => _options = options;

	/// <inheritdoc />
	public ValueTask<(string KeyId, byte[] Key)> GetCurrentSigningKeyAsync(CancellationToken cancellationToken)
	{
		var configuration = _options.Value;
		var key = configuration.SigningKey;
		if (key is null || key.Length == 0)
		{
			throw new InvalidOperationException(
				"Audit log integrity is enabled but no signing key is configured. Set " +
				$"{nameof(AuditIntegrityOptions)}.{nameof(AuditIntegrityOptions.SigningKey)} (sourced from a secret " +
				$"manager / KMS) or register a custom {nameof(IAuditSigningKeyProvider)}.");
		}

		return ValueTask.FromResult((configuration.KeyId, key));
	}

	/// <inheritdoc />
	public ValueTask<byte[]?> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		var configuration = _options.Value;
		var key = configuration.SigningKey;
		if (key is null || key.Length == 0
			|| !string.Equals(keyId, configuration.KeyId, StringComparison.Ordinal))
		{
			return ValueTask.FromResult<byte[]?>(null);
		}

		return ValueTask.FromResult<byte[]?>(key);
	}
}
