// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// The load-bearing "two-subject" decider for GDPR crypto-shredding (uahb0i). Independent
/// (author != implementer) and bound to emitted behaviour over the REAL encryption stack — a real
/// <see cref="InMemoryKeyManagementProvider"/> + real AES-GCM provider, no mocks. Proves the
/// per-<em>data-subject</em> invariant that shared/purpose-key encryption cannot satisfy: destroying
/// subject A's key renders ONLY A's ciphertext unrecoverable while subject B's still decrypts.
/// GREEN here confirms the per-subject wedge (<c>d4bw08</c> <c>SubjectKeyManager</c> + <c>ktepi9</c>
/// <c>FieldEncryptor</c>) actually shreds per subject; RED means the encryption is not per-subject.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CryptoShredTwoSubjectShould
{
    [Fact]
    public async Task DestroyingSubjectAKey_ShredsOnlySubjectA_LeavesSubjectBDecryptable()
    {
        await using var provider = BuildRealEncryptionStack();
        await ForceEncryptionRegistryInitAsync(provider);

        using var scope = provider.CreateScope();
        var fieldEncryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
        var keyAdmin = scope.ServiceProvider.GetRequiredService<IKeyManagementAdmin>();
        var hasher = scope.ServiceProvider.GetRequiredService<IDataSubjectHasher>();

        var subjectAData = "subject-A-personal-data"u8.ToArray();
        var subjectBData = "subject-B-personal-data"u8.ToArray();

        // Encrypt each subject's PII under their own per-subject key (real AES-GCM, real key store).
        var envelopeA = await fieldEncryptor.EncryptAsync("subject-A", subjectAData, CancellationToken.None);
        var envelopeB = await fieldEncryptor.EncryptAsync("subject-B", subjectBData, CancellationToken.None);

        // Sanity: both round-trip before any erasure (proves the fixtures are real, non-vacuous).
        (await fieldEncryptor.DecryptAsync(envelopeA, CancellationToken.None)).ShouldBe(subjectAData);
        (await fieldEncryptor.DecryptAsync(envelopeB, CancellationToken.None)).ShouldBe(subjectBData);

        // Crypto-shred subject A: destroy A's key (all versions), idempotent.
        await ShredSubjectAsync(keyAdmin, hasher, "subject-A");

        // LOAD-BEARING: A's PII is now unrecoverable (degrade-open tombstone = null), while B's PII is
        // untouched. A shared/purpose-key scheme would either leave A decryptable (no per-subject key)
        // or break B too (shared key) -> RED. Only genuine per-subject shredding is GREEN.
        (await fieldEncryptor.DecryptAsync(envelopeA, CancellationToken.None))
            .ShouldBeNull("destroying subject A's key must render only A's ciphertext unrecoverable");
        (await fieldEncryptor.DecryptAsync(envelopeB, CancellationToken.None))
            .ShouldBe(subjectBData, "subject B's PII must remain decryptable after A is shredded");
    }

    [Fact]
    public async Task DestroyKey_IsIdempotent_ForAnAlreadyShreddedSubject()
    {
        await using var provider = BuildRealEncryptionStack();
        await ForceEncryptionRegistryInitAsync(provider);

        using var scope = provider.CreateScope();
        var fieldEncryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
        var keyAdmin = scope.ServiceProvider.GetRequiredService<IKeyManagementAdmin>();
        var hasher = scope.ServiceProvider.GetRequiredService<IDataSubjectHasher>();

        var data = "erase-me"u8.ToArray();
        var envelope = await fieldEncryptor.EncryptAsync("subject-C", data, CancellationToken.None);

        await ShredSubjectAsync(keyAdmin, hasher, "subject-C");
        // Second destroy must not throw (idempotent crypto-erase).
        await Should.NotThrowAsync(async () =>
            await ShredSubjectAsync(keyAdmin, hasher, "subject-C"));

        (await fieldEncryptor.DecryptAsync(envelope, CancellationToken.None)).ShouldBeNull();
    }

    // Destroys a subject exactly as the erasure service does: the subject-id hash IS the key handle
    // (SubjectKeyManager derives it from the same singleton IDataSubjectHasher), and zero-day retention
    // requests an immediate crypto-shred. Going through IKeyManagementAdmin rather than a dedicated
    // per-subject destroy verb keeps this test bound to the path production actually takes.
    private static async Task ShredSubjectAsync(IKeyManagementAdmin keyAdmin, IDataSubjectHasher hasher, string subjectId)
    {
        _ = await keyAdmin.DeleteKeyAsync(hasher.HashDataSubjectId(subjectId), retentionDays: 0, CancellationToken.None);
    }

    private static ServiceProvider BuildRealEncryptionStack()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Real data-subject hashing (subjectId -> keyId). Pepper must be set or ValidateOnStart fails closed.
        services.AddDataSubjectHashing();
        services.Configure<DataSubjectHashingOptions>(options =>
            options.Pepper = "test-pepper-0123456789abcdef0123456789ab");

        // Real AES-GCM encryption over a real in-memory key-management provider, as the primary provider.
        services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));

        // AddCryptoShredding wires SubjectKeyManager + FieldEncryptor but not IKeyManagementAdmin; the
        // in-memory provider implements both provider + admin, so bridge admin to the same instance.
        services.AddSingleton<IKeyManagementAdmin>(sp =>
            (IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());

        services.AddCryptoShredding();

        return services.BuildServiceProvider();
    }

    // The encryption registry populates on first resolution of IEncryptionProvider, and selects its
    // primary only once every registered IHostedService has started (a real Generic Host does this
    // automatically); force both here so FieldEncryptor's registry.GetPrimary() does not throw.
    private static async Task ForceEncryptionRegistryInitAsync(IServiceProvider provider)
    {
        _ = provider.GetServices<IEncryptionProvider>().ToList();
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }
}
