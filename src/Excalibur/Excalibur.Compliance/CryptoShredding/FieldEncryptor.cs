// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Encrypts and decrypts personal-data fields under a data subject's dedicated key, so destroying that key
/// (crypto-shredding) renders the subject's data unrecoverable while every other subject stays intact.
/// </summary>
/// <remarks>
/// <para>
/// Encryption resolves the subject's key handle through <see cref="ISubjectKeyManager"/> and pins it on the
/// encryption context, so each subject's ciphertext is bound to that subject's key. Decryption degrades open
/// for a shredded subject: when the subject's key has been destroyed it returns <see langword="null"/> (a
/// tombstone) rather than throwing, so an aggregate still loads with its non-personal fields intact. Genuine
/// integrity or algorithm failures still surface as exceptions.
/// </para>
/// <para>
/// A tombstone is a statement that data was lawfully erased, so it is produced ONLY when the key-management
/// provider STATES that the material behind this field's key version is destroyed, through
/// <see cref="IKeyDestructionStatusProvider"/>. A key that merely cannot be found is not evidence of erasure:
/// backends with a recovery window report a deleted-but-restorable key as absent for the whole window, so
/// degrading open on absence would claim an erasure over data a single restore call brings back. When the
/// provider cannot answer, decryption throws rather than guessing -- an erased subject's aggregate then fails
/// to load on that provider, which is loud and recoverable, where a fabricated tombstone is neither.
/// </para>
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
        RetentionScope retentionScope,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var subjectKey = await _keyManager.GetOrCreateKeyAsync(subjectId, retentionScope, cancellationToken)
            .ConfigureAwait(false);

        // The generation travels in the CONTEXT, which is the one channel the encryption provider reads it
        // from on both paths. It is also what the read side rebuilds from the envelope, so write and read bind
        // the same value from the same kind of source.
        var context = new EncryptionContext
        {
            KeyId = subjectKey.KeyId,
            KeyGeneration = subjectKey.Generation
        };

        var provider = _registry.GetPrimary();
        return await provider.EncryptAsync(plaintext.ToArray(), context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> DecryptAsync(EncryptedData envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // A null provider means no registered provider supports this envelope's algorithm — a configuration
        // fault, NOT an erasure. Throwing is mandatory: returning a tombstone here would silently null out live,
        // recoverable PII and report a config fault as lawful GDPR erasure ("we lost it" masquerading as "we
        // erased it").
        var provider = _registry.FindDecryptionProvider(envelope)
            ?? throw new EncryptionException(
                $"No registered encryption provider supports algorithm '{envelope.Algorithm}' for the field "
                + "envelope. A missing provider is a configuration fault, not a crypto-shred erasure — refusing "
                + "to fabricate an erasure tombstone for data that is very likely still recoverable.");

        // REFUSED BY NAME, before any key is resolved and any associated data is computed. An envelope from a
        // layout this build does not write carries no generation, so the material behind it cannot be
        // identified and a destroyed subject cannot be told from a live one. Attempting it would fail the
        // authentication tag instead, which a reader would reasonably read as corrupted data.
        if (envelope.FormatVersion != EncryptedData.CurrentFormatVersion)
        {
            throw new EncryptionException(
                $"This field envelope declares format version {envelope.FormatVersion}, and this build writes "
                + $"and reads version {EncryptedData.CurrentFormatVersion}. The material behind an envelope of "
                + "the earlier layout cannot be identified, so a destroyed subject cannot be distinguished from "
                + "one whose key was provisioned again afterwards. The envelope is refused rather than read.")
            {
                ErrorCode = EncryptionErrorCode.InvalidCiphertext
            };
        }

        // FAIL CLOSED ON AN ABSENT GENERATION, and this refusal is what makes binding-only-when-present safe.
        // The associated data includes the generation only when the caller supplies one, so an envelope with
        // the property stripped would otherwise re-read as the no-generation form and authenticate correctly --
        // a downgrade anyone could perform by deleting one JSON field. Refusing absence here makes that
        // inexpressible on this path, while a caller whose envelope has nowhere to carry a generation keeps
        // today's behaviour untouched.
        if (string.IsNullOrEmpty(envelope.KeyGeneration))
        {
            throw new EncryptionException(
                "This field envelope carries no key generation, so the material it was written under cannot be "
                + "identified. A destroyed key and a key provisioned again at the same handle are "
                + "indistinguishable without it, which is the difference between reporting a lawful erasure and "
                + "reporting one that did not happen. The envelope is refused rather than read.")
            {
                ErrorCode = EncryptionErrorCode.InvalidCiphertext
            };
        }

        var context = new EncryptionContext
        {
            KeyId = envelope.KeyId,
            KeyVersion = envelope.KeyVersion,
            KeyGeneration = envelope.KeyGeneration
        };

        // ASKED BEFORE DECRYPTING, not after it fails, because the failure this has to catch is not a
        // recognisable one. When a handle has been re-provisioned, the key this envelope names resolves to LIVE
        // material of a different generation, and the decryption fails its authentication tag -- which is
        // indistinguishable from corruption and is not the key-not-found condition the handler below catches.
        // So the generation's destruction is established first, and a destroyed generation is a tombstone
        // without a decryption ever being attempted.
        if (await IsEnvelopeGenerationDestroyedAsync(envelope, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return await provider.DecryptAsync(envelope, context, cancellationToken).ConfigureAwait(false);
        }
        catch (EncryptionException ex)
            when (ex.ErrorCode == EncryptionErrorCode.KeyNotFound && !string.IsNullOrEmpty(envelope.KeyId))
        {
            // UNREACHABLE BUT NOT DESTROYED, and that is already established rather than asked again here.
            //
            // A crypto-shredded subject and a key merely out of reach are indistinguishable from the failure
            // alone, and only one of them may answer a read with "lawfully erased". The provider has already
            // STATED which it is: the check above ran before this decryption was attempted and said the
            // generation's material is not destroyed. Absence is never accepted as that statement — a
            // soft-deleted key is absent for its whole recovery window while one restore call brings it back,
            // and a tombstone over such a key reports an erasure that did not happen.
            //
            // So the read fails, loudly, and the data is still there to recover. Asking a second time would
            // only widen the window in which an erasure landing mid-read turns a failure into a tombstone, and
            // would charge every failed read an extra round trip to the key store for an answer it already has.
            throw;
        }
    }

    /// <summary>
    /// Asks the key-management provider to state whether the material of this envelope's key GENERATION is
    /// destroyed, and refuses to decide the read when it has no way to say.
    /// </summary>
    /// <remarks>
    /// The generation rather than the handle or the version, because only the generation's answer cannot be
    /// changed by a later provisioning: destroy a subject's key, let one ordinary write provision another at the
    /// same handle, and both the handle and the restarted version ordinal truthfully report "not destroyed" --
    /// about material this reader is not holding.
    /// </remarks>
    private async ValueTask<bool> IsEnvelopeGenerationDestroyedAsync(
        EncryptedData envelope,
        CancellationToken cancellationToken)
    {
        // A provider that cannot answer is never read as having answered "destroyed". Degrading open on the
        // strength of a question nobody answered is the same fabrication as degrading open on absence.
        if (_keyProvider.GetService(typeof(IKeyDestructionStatusProvider))
            is not IKeyDestructionStatusProvider destructionStatus)
        {
            throw new EncryptionException(
                $"The key-management provider '{_keyProvider.GetType().Name}' does not implement "
                + $"{nameof(IKeyDestructionStatusProvider)}, so it cannot state whether the key behind this "
                + "field was destroyed, and a crypto-shredded subject's fields cannot degrade open on it. "
                + $"Implement {nameof(IKeyDestructionStatusProvider)} on the provider to restore that "
                + "behaviour; until then a read of an erased subject fails rather than reporting an erasure "
                + "nothing confirmed.");
        }

        return await destructionStatus
            .IsKeyDestroyedAsync(envelope.KeyId, envelope.KeyGeneration!, cancellationToken)
            .ConfigureAwait(false);
    }
}
