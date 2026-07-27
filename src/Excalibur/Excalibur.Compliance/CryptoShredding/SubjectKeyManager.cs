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
/// Destruction delegates to <see cref="IKeyManagementAdmin.DeleteKeyAsync"/> with a zero-day retention so
/// erasure is immediate, and is idempotent: destroying an absent key is a successful no-op.
/// </para>
/// </remarks>
internal sealed class SubjectKeyManager : ISubjectKeyManager
{
    private const string CryptoShredPurpose = "crypto-shred";

    private readonly IKeyManagementProvider _keyProvider;
    private readonly IKeyManagementAdmin _keyAdmin;
    private readonly IDataSubjectHasher _hasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectKeyManager"/> class.
    /// </summary>
    /// <param name="keyProvider">The key-management provider used to look up and create per-subject keys.</param>
    /// <param name="keyAdmin">The administrative key surface used to destroy per-subject keys.</param>
    /// <param name="hasher">The data-subject hasher used to derive stable key handles.</param>
    public SubjectKeyManager(
        IKeyManagementProvider keyProvider,
        IKeyManagementAdmin keyAdmin,
        IDataSubjectHasher hasher)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _keyAdmin = keyAdmin ?? throw new ArgumentNullException(nameof(keyAdmin));
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

    /// <inheritdoc/>
    public async ValueTask DestroyKeyAsync(string subjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var keyId = _hasher.HashDataSubjectId(subjectId);

        // Zero-day retention = request immediate crypto-shred. An absent key yields KeyDestructionState.NotFound,
        // the idempotent case (already-erased or never-created); no error is raised. Erasure attestation is gated
        // on the tri-state outcome at the ErasureService call site — only KeyDestructionState.Completed is treated
        // as irrecoverable-now — so the void outcome here is intentionally not surfaced.
        _ = await _keyAdmin.DeleteKeyAsync(keyId, retentionDays: 0, cancellationToken).ConfigureAwait(false);
    }
}
