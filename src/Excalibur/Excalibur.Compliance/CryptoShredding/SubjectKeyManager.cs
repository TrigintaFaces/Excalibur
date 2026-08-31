// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Binds a data subject to a dedicated encryption key over the existing key-management subsystem, so that
/// destroying the subject's key crypto-shreds every value encrypted under it.
/// </summary>
/// <remarks>
/// <para>
/// The subject identifier is pseudonymized through <see cref="IDataSubjectHasher"/> to derive a stable,
/// non-reversible key handle — raw identifiers never reach the key store. Key material is minted by the
/// underlying <see cref="IKeyManagementProvider"/> (whose backends use a cryptographically-secure RNG); this
/// adapter never generates key bytes itself.
/// </para>
/// <para>
/// Key destruction is not performed here. Erasure is owned by the erasure service, which honours legal holds
/// and records attestation before destroying a subject's key through <see cref="IKeyManagementAdmin"/>.
/// </para>
/// </remarks>
internal sealed class SubjectKeyManager : ISubjectKeyManager
{
    private const string CryptoShredPurpose = "crypto-shred";

    private readonly IKeyManagementProvider _keyProvider;
    private readonly IDataSubjectHasher _hasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectKeyManager"/> class.
    /// </summary>
    /// <param name="keyProvider">The key-management provider used to look up and create per-subject keys.</param>
    /// <param name="hasher">The data-subject hasher used to derive stable key handles.</param>
    public SubjectKeyManager(
        IKeyManagementProvider keyProvider,
        IDataSubjectHasher hasher)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetOrCreateKeyAsync(string subjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var keyId = _hasher.HashDataSubjectId(subjectId);

        var existing = await _keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            // No key yet for this subject — mint one. RotateKeyAsync creates the key when it does not exist;
            // key material comes from the provider's CSPRNG-backed backend, never from this adapter.
            await _keyProvider.RotateKeyAsync(
                keyId,
                EncryptionAlgorithm.Aes256Gcm,
                CryptoShredPurpose,
                expiresAt: null,
                cancellationToken).ConfigureAwait(false);
        }

        return keyId;
    }
}
