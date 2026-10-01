// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// A <see langword="null"/> from <see cref="IFieldEncryptor.DecryptAsync"/> is a statement that the field was
/// lawfully crypto-shredded, so it may be produced ONLY on an affirmative statement of destruction and never on
/// the mere absence of a key. The two are indistinguishable at the read: a backend with a recovery window
/// answers a deleted-but-fully-restorable key exactly as it answers a destroyed one -- Azure Key Vault reports a
/// soft-deleted key as not found for its entire retention period while one restore call brings it back. Reading
/// absence as erasure therefore tells the controller an erasure request was discharged over data that is still
/// there, silently, with nothing downstream ever learning otherwise.
/// </summary>
/// <remarks>
/// The arms below drive <see cref="FieldEncryptor"/> against a decryption provider that raises the
/// key-not-found failure. That failure is not invented here:
/// <see cref="AesGcmEncryptionProvider"/> raises exactly it when the key version an envelope names cannot be
/// resolved, which the last arm holds it to over the real provider. The full real-stack path -- real key store,
/// real AES-GCM, a genuinely destroyed key, a tombstone -- is locked by
/// <see cref="CryptoShredTwoSubjectShould"/>.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AnAbsentKeyIsNotAnErasureTombstoneShould
{
    private const string KeyId = "subject-key-handle";
    private const int KeyVersion = 3;

    // The envelope must name the GENERATION of material it was written under, because the read path refuses
    // an envelope that does not: without it a destroyed key and a key provisioned again at the same handle
    // are indistinguishable, which is the difference between reporting a lawful erasure and reporting one
    // that did not happen. These arms are about WHAT the provider states, so the generation is simply
    // present and the statement under test is unchanged.
    private const string KeyGeneration = "generation-of-the-material-these-arms-are-about";


    /// <summary>
    /// THE SAFETY ARM. The key behind the envelope cannot be resolved, and the provider states the material is
    /// still recoverable. RED on the defect this locks: a predicate that degrades open on absence returns a
    /// tombstone here, asserting a lawful erasure over PII a single restore call recovers.
    /// </summary>
    [Fact]
    public async Task Refuse_AnAbsentButRecoverableKey_RatherThanReportItErased()
    {
        var keyProvider = KeyProviderStating(destroyed: false);
        var encryptor = new FieldEncryptor(
            A.Fake<ISubjectKeyManager>(),
            RegistryWhoseProviderCannotFindTheKey(),
            keyProvider);

        var refusal = await Should.ThrowAsync<EncryptionException>(
            () => encryptor.DecryptAsync(Envelope(), CancellationToken.None).AsTask());

        refusal.ErrorCode.ShouldBe(
            EncryptionErrorCode.KeyNotFound,
            "an unreachable-but-recoverable key must surface as the read failure it is, never as an erasure");

        A.CallTo(() => ((IKeyDestructionStatusProvider)keyProvider)
                .IsKeyDestroyedAsync(KeyId, KeyGeneration, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>
    /// THE LIVENESS ARM. Without it, a fix that simply refuses every unresolvable key satisfies the arm above
    /// while destroying the documented degrade-open guarantee: an erased subject's aggregate must still load
    /// with its non-personal fields intact.
    /// </summary>
    [Fact]
    public async Task DegradeOpen_WhenTheProviderStatesTheMaterialIsDestroyed()
    {
        var encryptor = new FieldEncryptor(
            A.Fake<ISubjectKeyManager>(),
            RegistryWhoseProviderCannotFindTheKey(),
            KeyProviderStating(destroyed: true));

        var plaintext = await encryptor.DecryptAsync(Envelope(), CancellationToken.None);

        plaintext.ShouldBeNull("a key the provider states is destroyed is a lawful crypto-shred tombstone");
    }

    /// <summary>
    /// A provider with no way to answer the destruction question is never read as having answered "destroyed".
    /// The refusal is loud and names the capability to implement, because the alternative is a tombstone
    /// nothing confirmed.
    /// </summary>
    [Fact]
    public async Task Refuse_AndNameTheCapability_WhenNoProviderCanStateDestruction()
    {
        // A bare provider answers no optional capability, which is exactly a consumer-authored provider that
        // never implemented this one.
        var encryptor = new FieldEncryptor(
            A.Fake<ISubjectKeyManager>(),
            RegistryWhoseProviderCannotFindTheKey(),
            A.Fake<IKeyManagementProvider>());

        var refusal = await Should.ThrowAsync<EncryptionException>(
            () => encryptor.DecryptAsync(Envelope(), CancellationToken.None).AsTask());

        refusal.Message.ShouldContain(
            nameof(IKeyDestructionStatusProvider),
            Case.Sensitive,
            "an operator cannot act on a refusal that does not name what to implement");
    }

    /// <summary>
    /// Binds the arms above to the real decryption provider: the failure they simulate is the one
    /// <see cref="AesGcmEncryptionProvider"/> actually raises when the key version an envelope names is gone.
    /// Were it any other error code, the arms above would be locking a path production never takes.
    /// </summary>
    [Fact]
    public async Task TheRealProvider_RaisesKeyNotFound_WhenTheEnvelopesKeyVersionIsGone()
    {
        var keyProvider = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyMaterialProvider>());
        A.CallTo(() => keyProvider.GetKeyVersionAsync(KeyId, KeyVersion, A<CancellationToken>._))
            .Returns(Task.FromResult<KeyMetadata?>(null));

        using var provider = new AesGcmEncryptionProvider(
            keyProvider,
            NullLogger<AesGcmEncryptionProvider>.Instance);

        var failure = await Should.ThrowAsync<EncryptionException>(
            () => provider.DecryptAsync(
                Envelope(),
                new EncryptionContext { KeyId = KeyId, KeyVersion = KeyVersion },
                CancellationToken.None));

        failure.ErrorCode.ShouldBe(EncryptionErrorCode.KeyNotFound);
    }

    private static EncryptedData Envelope() => new()
    {
        Ciphertext = new byte[32],
        KeyId = KeyId,
        KeyVersion = KeyVersion,
        KeyGeneration = KeyGeneration,
        Algorithm = EncryptionAlgorithm.Aes256Gcm,
        Iv = new byte[12],
        AuthTag = new byte[16],
    };

    // A provider that answers the destruction question, and only that question, with the given statement.
    private static IKeyManagementProvider KeyProviderStating(bool destroyed)
    {
        var provider = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyDestructionStatusProvider>());
        A.CallTo(() => provider.GetService(typeof(IKeyDestructionStatusProvider))).Returns(provider);
        A.CallTo(() => ((IKeyDestructionStatusProvider)provider)
                .IsKeyDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(destroyed));

        // The handle-scoped overload must NOT be what decides a read: a handle holding one destroyed version and
        // one live version is not a destroyed handle. Answering it the opposite way here means an arm that
        // silently fell back to it reddens instead of passing.
        A.CallTo(() => ((IKeyDestructionStatusProvider)provider)
                .IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(!destroyed));

        return provider;
    }

    // Stands in for the registered decryption provider once the envelope's key version can no longer be
    // resolved -- the shape AesGcmEncryptionProvider takes on a soft-deleted or destroyed key alike.
    private static IEncryptionProviderRegistry RegistryWhoseProviderCannotFindTheKey()
    {
        var provider = A.Fake<IEncryptionProvider>();
        A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ThrowsAsync(new EncryptionException("The encryption key was not found.")
            {
                ErrorCode = EncryptionErrorCode.KeyNotFound,
            });

        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);

        return registry;
    }
}
