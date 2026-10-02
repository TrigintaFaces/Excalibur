// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

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
    // Every arm here is single-tenant: the write path collapses an unresolved ambient tenant onto
    // this identity, so the erasure helper must derive its handle under the same one.
    private static readonly TenantId Tenant = new(TenantDefaults.DefaultTenantId);

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
        var envelopeA = await fieldEncryptor.EncryptAsync("subject-A", RetentionScope.NotInAnAggregate, subjectAData, CancellationToken.None);
        var envelopeB = await fieldEncryptor.EncryptAsync("subject-B", RetentionScope.NotInAnAggregate, subjectBData, CancellationToken.None);

        // Sanity: both round-trip before any erasure (proves the fixtures are real, non-vacuous).
        (await fieldEncryptor.DecryptAsync(envelopeA, CancellationToken.None)).ShouldBe(subjectAData);
        (await fieldEncryptor.DecryptAsync(envelopeB, CancellationToken.None)).ShouldBe(subjectBData);

        // Crypto-shred subject A: destroy A's key (all versions), idempotent.
        await ShredSubjectAsync(
            keyAdmin,
            scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>(),
            scope.ServiceProvider.GetRequiredService<IKeyDestructionLedger>(),
            hasher,
            Tenant,
            "subject-A");

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
        var envelope = await fieldEncryptor.EncryptAsync("subject-C", RetentionScope.NotInAnAggregate, data, CancellationToken.None);

        await ShredSubjectAsync(
            keyAdmin,
            scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>(),
            scope.ServiceProvider.GetRequiredService<IKeyDestructionLedger>(),
            hasher,
            Tenant,
            "subject-C");
        // Second destroy must not throw (idempotent crypto-erase).
        await Should.NotThrowAsync(async () =>
            await ShredSubjectAsync(
            keyAdmin,
            scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>(),
            scope.ServiceProvider.GetRequiredService<IKeyDestructionLedger>(),
            hasher,
            Tenant,
            "subject-C"));

        (await fieldEncryptor.DecryptAsync(envelope, CancellationToken.None)).ShouldBeNull();
    }

    // Destroys a subject exactly as the erasure service does: the subject-id hash IS the key handle
    // (SubjectKeyManager derives it from the same singleton IDataSubjectHasher), and zero-day retention
    // requests an immediate crypto-shred. Going through IKeyManagementAdmin rather than a dedicated
    // per-subject destroy verb keeps this test bound to the path production actually takes.
    /// <summary>
    /// THE RE-OCCUPATION ARM. After an erasure, ANY later write for the same subject mints a live key at the
    /// same deterministic handle -- a re-registration, a new record under the same customer number, or a
    /// subject still trading because the law requires their identity kept. An earlier record of that subject
    /// must still load as a tombstone, and must NOT throw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this catches that no predicate-level arm can.</b> The handle is derived from the subject id, so
    /// it is re-mintable by construction. A detector that asks "is a key present at this envelope's handle?"
    /// sees the NEW key, concludes nothing was erased, and hands the old ciphertext to the provider under
    /// material that never produced it -- the authentication tag fails and the load THROWS. The documented
    /// guarantee is that an erased subject's aggregate still loads with its non-personal fields and a
    /// tombstone in place of the erased ones, and after one ordinary write that guarantee would be gone
    /// permanently for every earlier record of that subject, with nothing reporting it.
    /// </para>
    /// <para>
    /// <b>Why it belongs on the real stack rather than beside the predicate arms.</b> Those arms assert the
    /// state directly: a live handle with a destroyed version answers "destroyed". This one proves the state
    /// is REACHABLE by ordinary use -- that re-minting through the subject key manager actually produces a
    /// live handle over a destroyed generation. A predicate can be correct about a state nothing constructs.
    /// </para>
    /// <para>
    /// <b>Not a confidentiality defect.</b> No erased plaintext becomes recoverable: ciphertext under the
    /// destroyed generation cannot be decrypted by the re-minted key. The failure is liveness and honesty.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task StillTombstoneAnEarlierRecord_AfterALaterWriteReMintsTheSubjectsKey()
    {
        await using var provider = BuildRealEncryptionStack();
        await ForceEncryptionRegistryInitAsync(provider);

        using var scope = provider.CreateScope();
        var fieldEncryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
        var keyAdmin = scope.ServiceProvider.GetRequiredService<IKeyManagementAdmin>();
        var hasher = scope.ServiceProvider.GetRequiredService<IDataSubjectHasher>();

        var earlier = "personal-data-written-before-the-erasure"u8.ToArray();

        var earlierEnvelope = await fieldEncryptor.EncryptAsync(
            "subject-R", RetentionScope.NotInAnAggregate, earlier, CancellationToken.None);

        // Non-vacuity: it round-trips before anything is destroyed, so a later null cannot be a broken fixture.
        (await fieldEncryptor.DecryptAsync(earlierEnvelope, CancellationToken.None)).ShouldBe(earlier);

        await ShredSubjectAsync(
            keyAdmin,
            scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>(),
            scope.ServiceProvider.GetRequiredService<IKeyDestructionLedger>(),
            hasher,
            Tenant,
            "subject-R");

        // THE ORDINARY WRITE. Nothing unusual is requested: the subject appears again, and the key manager
        // finds no key at their handle and mints one. This is the step that re-occupies the handle.
        var laterEnvelope = await fieldEncryptor.EncryptAsync(
            "subject-R", RetentionScope.NotInAnAggregate, "personal-data-written-after"u8.ToArray(), CancellationToken.None);

        laterEnvelope.KeyId.ShouldBe(
            earlierEnvelope.KeyId,
            "the premise of this arm is that the handle is deterministic and therefore re-occupied. If these "
            + "differ, re-minting no longer collides and the arm is asserting nothing -- fix the arm, do not "
            + "relax it.");

        // LOAD-BEARING. A live key now sits at this handle, so a handle-scoped detector would report the
        // subject as never erased. The earlier record must still be a tombstone.
        (await fieldEncryptor.DecryptAsync(earlierEnvelope, CancellationToken.None))
            .ShouldBeNull(
                "an earlier record of an erased subject must still load as a tombstone after a later write "
                + "re-mints their key. Throwing here destroys the documented degrade-open guarantee "
                + "permanently for every earlier record of that subject.");

        // LIVENESS. The tombstone above must not come from a stack that has simply stopped decrypting.
        (await fieldEncryptor.DecryptAsync(laterEnvelope, CancellationToken.None))
            .ShouldBe(
                "personal-data-written-after"u8.ToArray(),
                "data written under the re-minted key must still decrypt, or the arm above is satisfied by a "
                + "provider that returns null for everything");
    }

    /// <summary>
    /// Crypto-shreds a subject the way a CONSUMER does it: read the generation, destroy, record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to destroy and stop, with a comment claiming it did what the erasure service does. That
    /// stopped being true when a tombstone started requiring a ledger record: destroying alone now leaves the
    /// subject's reads failing forever instead of reporting their erasure. Recording is the other half.
    /// </para>
    /// <para>
    /// THE ORDER IS THE WHOLE POINT, and it is the order the public ledger write documents. The generation is
    /// read FIRST, because on most backends the identifier is material the destruction takes with it, so after
    /// the destroy there is nothing left to read. It is recorded LAST, because a row written before the
    /// destruction reports live material as erased for however long the gap lasts.
    /// </para>
    /// </remarks>
    private static async Task ShredSubjectAsync(
        IKeyManagementAdmin keyAdmin,
        IKeyManagementProvider keyProvider,
        IKeyDestructionLedger ledger,
        IDataSubjectHasher hasher,
        TenantId tenant,
        string subjectId)
    {
        // THE HANDLE IS DERIVED THE WAY THE ERASURE DERIVES IT -- from (tenant, subject hash) through the
        // single function both production paths use. Deriving the bare subject hash here, as this helper used
        // to, would destroy a key nothing was ever written under: every arm below would then report the
        // subject as NOT shredded, which is the erasure silently achieving nothing.
        var keyHandle = SubjectKeyHandle.ForSubject(tenant, subjectId, hasher).Value;

        // FIRST: the generation, while the material still exists.
        var generation = (await keyProvider.GetKeyAsync(keyHandle, CancellationToken.None))?.Generation;

        var outcome = await keyAdmin.DeleteKeyAsync(keyHandle, retentionDays: 0, CancellationToken.None);

        // LAST, and only on an irreversible destruction. A key that is merely scheduled is still recoverable,
        // so recording it would assert an erasure that has not happened. A repeat shred finds no key and no
        // generation, which is why this is conditional rather than unconditional -- and the ledger row the
        // first shred wrote is what keeps the subject's reads reporting their erasure.
        if (outcome.State == KeyDestructionState.Completed && generation is not null)
        {
            // The record stores the characters, which is the boundary KeyGeneration deliberately stops at.
            await ledger.RecordDestroyedGenerationAsync(keyHandle, generation.Value.ToString(), CancellationToken.None);
        }
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

        // The destruction ledger, which a crypto-shredding read now requires: a tombstone is produced only
        // from a ledger record, so FieldEncryptor takes the ledger as a required dependency and there is
        // deliberately no always-empty default. The in-memory erasure store is this framework's in-process
        // ledger implementation, so registering it is how a test (or a consumer with no database) supplies one.
        services.AddInMemoryErasureStore();

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
