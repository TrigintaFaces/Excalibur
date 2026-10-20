// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;

namespace Excalibur.Compliance.Tests.CryptoShredding;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class FieldDecryptionTenantBoundaryShould
{
    [Theory]
    [InlineData(false, "tenant-b", "tenant-a")]
    [InlineData(true, "tenant-b", "tenant-a")]
    [InlineData(false, "Tenant-A", "tenant-a")]
    [InlineData(true, "tenant-a ", "tenant-a")]
    [InlineData(false, null, "tenant-a")]
    [InlineData(true, "", "tenant-a")]
    [InlineData(false, " ", "tenant-a")]
    [InlineData(true, "tenant-a", null)]
    [InlineData(false, "tenant-a", "")]
    [InlineData(true, "tenant-a", " ")]
    public async Task RejectUnresolvedOrMismatchedOwnerBeforeProviderOrDestructionLookup(
        bool destroyed, string? expectedTenantId, string? envelopeTenantId)
    {
        var ledger = A.Fake<IKeyDestructionLedger>();
        A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new ValueTask<bool>(destroyed));
        var provider = A.Fake<IEncryptionProvider>();
        A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new byte[] { 42 }));
        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);
        var tenant = A.Fake<ITenantContext>();
        A.CallTo(() => tenant.HasTenant).Returns(true);
        A.CallTo(() => tenant.TenantId).Returns("tenant-b");
        var encryptor = new FieldEncryptor(A.Fake<ISubjectKeyManager>(), registry, ledger, tenant);
        var envelope = new EncryptedData
        {
            Ciphertext = new byte[32],
            KeyId = "tenant-a-subject-key",
            KeyVersion = 1,
            KeyGeneration = "f3a1c0de5b7948e2a6d4019fcb82e57d",
            TenantId = envelopeTenantId,
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            Iv = new byte[12],
            AuthTag = new byte[16],
        };

        _ = await Should.ThrowAsync<CryptographicException>(() =>
            encryptor.DecryptAsync(envelope, expectedTenantId!, CancellationToken.None).AsTask());

        A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).MustNotHaveHappened();
        A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseTheExplicitOwnerWithoutAnAmbientTenant(bool destroyed)
    {
        var ledger = A.Fake<IKeyDestructionLedger>();
        A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new ValueTask<bool>(destroyed));
        var provider = A.Fake<IEncryptionProvider>();
        A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new byte[] { 42 }));
        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);
        var ambient = A.Fake<ITenantContext>();
        var encryptor = new FieldEncryptor(A.Fake<ISubjectKeyManager>(), registry, ledger, ambient);
        var envelope = new EncryptedData
        {
            Ciphertext = new byte[32],
            KeyId = "tenant-a-subject-key",
            KeyVersion = 1,
            KeyGeneration = "f3a1c0de5b7948e2a6d4019fcb82e57d",
            TenantId = "tenant-a",
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            Iv = new byte[12],
            AuthTag = new byte[16],
        };

        var result = await encryptor.DecryptAsync(envelope, "tenant-a", CancellationToken.None);

        if (destroyed)
        {
            result.ShouldBeNull();
            A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
        else
        {
            result.ShouldBe(new byte[] { 42 });
            A.CallTo(() => provider.DecryptAsync(envelope,
                A<EncryptionContext>.That.Matches(context => context.TenantId == "tenant-a"), A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        A.CallTo(() => ambient.TenantId).MustNotHaveHappened();
        A.CallTo(() => ambient.HasTenant).MustNotHaveHappened();
        A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, envelope.KeyGeneration, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

}
