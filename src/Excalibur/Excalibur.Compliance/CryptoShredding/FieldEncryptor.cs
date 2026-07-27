// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Encrypts and decrypts personal-data fields under a data subject's dedicated key, so destroying that key
/// (crypto-shredding) renders the subject's data unrecoverable while every other subject stays intact.
/// </summary>
/// <remarks>
/// Encryption resolves the subject's key handle through <see cref="ISubjectKeyManager"/> and pins it on the
/// encryption context, so each subject's ciphertext is bound to that subject's key. Decryption degrades open
/// for a shredded subject: when the subject's key has been destroyed it returns <see langword="null"/> (a
/// tombstone) rather than throwing, so an aggregate still loads with its non-personal fields intact. Genuine
/// integrity or algorithm failures still surface as exceptions.
/// </remarks>
internal sealed class FieldEncryptor : IFieldEncryptor
{
    private readonly ISubjectKeyManager _keyManager;
    private readonly IEncryptionProviderRegistry _registry;
    private readonly IKeyManagementProvider _keyProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptor"/> class.
    /// </summary>
    /// <param name="keyManager">Resolves and creates the per-subject key handle.</param>
    /// <param name="registry">The encryption provider registry used to encrypt and decrypt.</param>
    /// <param name="keyProvider">The key-management provider used to detect a shredded (destroyed) key.</param>
    public FieldEncryptor(
        ISubjectKeyManager keyManager,
        IEncryptionProviderRegistry registry,
        IKeyManagementProvider keyProvider)
    {
        _keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    }

    /// <inheritdoc/>
    public async ValueTask<EncryptedData> EncryptAsync(
        string subjectId,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var keyId = await _keyManager.GetOrCreateKeyAsync(subjectId, cancellationToken).ConfigureAwait(false);
        var context = new EncryptionContext { KeyId = keyId };

        var provider = _registry.GetPrimary();
        return await provider.EncryptAsync(plaintext.ToArray(), context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> DecryptAsync(EncryptedData envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Degrade open for a crypto-shredded subject: if the envelope's key has been destroyed, the data is
        // permanently unrecoverable — return a null tombstone rather than throwing so the aggregate still
        // loads with its non-personal fields. A genuine decrypt failure (corrupt ciphertext / bad tag) below
        // still throws.
        if (!string.IsNullOrEmpty(envelope.KeyId)
            && await _keyProvider.GetKeyAsync(envelope.KeyId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        // The destroyed-key tombstone is the ONLY degrade-open path (handled above): by here the subject key
        // provably still exists. A null provider means no registered provider supports this envelope's
        // algorithm — a configuration fault, NOT an erasure. Throwing is mandatory: returning a tombstone here
        // would silently null out live, recoverable PII and report a config fault as lawful GDPR erasure
        // ("we lost it" masquerading as "we erased it").
        var provider = _registry.FindDecryptionProvider(envelope)
            ?? throw new EncryptionException(
                $"No registered encryption provider supports algorithm '{envelope.Algorithm}' for the field "
                + "envelope. The subject key is present, so this is a configuration fault, not a crypto-shred "
                + "erasure — refusing to fabricate an erasure tombstone for recoverable data.");

        var context = new EncryptionContext { KeyId = envelope.KeyId, KeyVersion = envelope.KeyVersion };
        return await provider.DecryptAsync(envelope, context, cancellationToken).ConfigureAwait(false);
    }
}
