// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Tests.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// The cross-tenant crypto-shred decider: one tenant's completed erasure must never render another
/// tenant's personal fields unreadable, for a data-subject identifier the two tenants happen to share.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this arm exists.</b> The key handle used to be <c>H_pepper(subjectId)</c> — constant in the
/// tenant. Data-subject identifiers are consumer-supplied from consumer entities, so customer numbers,
/// employee identifiers and e-mail addresses repeat across tenants in ordinary multi-tenant deployments.
/// Two such tenants therefore shared ONE encryption key, and either tenant's erasure destroyed it for
/// both: the victim's reads then returned <see langword="null"/> — this framework's statement that the
/// data was lawfully erased — over data that was never erased and was no longer recoverable. Nothing
/// downstream learned, and nothing could.
/// </para>
/// <para>
/// <b>RED input.</b> Change <c>SubjectKeyHandle.ForSubjectHash</c> to ignore its <c>tenant</c> argument
/// and return <c>hasher.HashDataSubjectId(dataSubjectIdHash)</c>, which is the pre-fix derivation modulo
/// one extra HMAC. <see cref="TenantBsFieldsStillDecrypt_AfterTenantAErasesTheSameSubjectId"/> then goes
/// RED on tenant B reading <see langword="null"/>, which is the defect verbatim.
/// </para>
/// <para>
/// <b>Bound to emitted behaviour over the real stack, no mocks on the path under test</b> — the real
/// keyed hasher, the real AES-GCM provider over a real in-memory key store, and the real
/// <see cref="ErasureService"/> writing the real destruction ledger. The only test doubles are the
/// ambient tenant (which a tenant resolver or middleware supplies in production) and the legal-hold
/// service, which this arm is not about.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ATenantQualifiedSubjectKeyHandleShould
{
    private const string SharedSubjectId = "42";
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    /// <summary>
    /// THE SAFETY ARM, on the exact interleaving: two ordinary writes and one ordinary erasure, no race
    /// and no crash.
    /// </summary>
    [Fact]
    public async Task TenantBsFieldsStillDecrypt_AfterTenantAErasesTheSameSubjectId()
    {
        await using var host = await BuildAsync();

        var tenantAData = "tenant-A-personal-data"u8.ToArray();
        var tenantBData = "tenant-B-personal-data"u8.ToArray();

        // Tenant A appends for its subject 42, minting a key.
        host.Tenant.TenantId = TenantA;
        var tenantAEnvelope = await host.EncryptAsync(SharedSubjectId, tenantAData);

        // Tenant B appends for ITS subject 42. Under the defect, the key manager found a key already at
        // the handle, so this was a no-op and B's envelope named A's key and generation.
        host.Tenant.TenantId = TenantB;
        var tenantBEnvelope = await host.EncryptAsync(SharedSubjectId, tenantBData);

        // The premise -- that the two tenants hold different handles -- is asserted in its own arm below
        // rather than guarded here, deliberately. A guard here would ABORT this arm the moment the handles
        // collided, so the pre-fix defect would surface as "the premise does not hold" instead of as what it
        // actually is: tenant B reading a tombstone over live data. This arm must be allowed to run all the
        // way to that assertion.

        // Non-vacuity: both round-trip before anything is destroyed, so a later null cannot be a broken
        // fixture or a provider that has simply stopped decrypting.
        (await host.DecryptAsync(tenantAEnvelope)).ShouldBe(tenantAData);
        (await host.DecryptAsync(tenantBEnvelope)).ShouldBe(tenantBData);

        // Tenant A erases subject 42, through the real erasure service, under tenant A's own scope.
        host.Tenant.TenantId = TenantA;
        var erasure = await host.EraseAsync(SharedSubjectId);
        erasure.KeysDeleted.ShouldBeGreaterThan(
            0,
            "an erasure that destroyed no key cannot distinguish a correct fix from a derivation nothing "
            + "was ever written under -- both leave tenant B readable");

        // LOAD-BEARING. Tenant B was not party to this erasure and holds its own key, so its personal
        // fields must still decrypt. Under the defect this returned null: a tombstone asserting a lawful
        // erasure over data that was never erased and can no longer be recovered.
        (await host.DecryptAsync(tenantBEnvelope)).ShouldBe(
            tenantBData,
            "tenant A's erasure of ITS subject 42 must leave tenant B's subject 42 readable. A null here is "
            + "this framework asserting a lawful erasure over another tenant's live personal data, which is "
            + "irreversible and which nothing downstream ever learns about.");
    }

    /// <summary>
    /// THE LIVENESS HALF. A fix that stopped erasure working at all would satisfy the safety arm above
    /// perfectly, so the same interleaving must also still tombstone the erasing tenant's own fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This arm does NOT detect the loss of the tenant from the handle derivation, and that is correct
    /// rather than a gap.</b> Tenant A erasing tenant A's own subject works perfectly well when two tenants
    /// have been collapsed onto one key -- the erasure destroys a key, and the read of A's own field finds
    /// its generation recorded. Measured: with the tenant removed from the composition,
    /// <see cref="TenantBsFieldsStillDecrypt_AfterTenantAErasesTheSameSubjectId" /> and
    /// <see cref="MintADifferentKey_ForEachTenantSharingOneSubjectId" /> both go RED and this arm stays
    /// GREEN.
    /// </para>
    /// <para>
    /// The note exists because this is the arm a reader reaches for, and two of us reached for it. It names
    /// erasure, it names a tenant, it runs the whole write-erase-read path, and it is aimed one inch beside
    /// the property -- so an argument that "the coupling is already covered here" sounds right and is not.
    /// A test withdrawal was nearly made on exactly that reasoning; the two arms named above are what
    /// actually hold tenant separation, and the envelope-versus-re-derivation property is held by neither,
    /// in <c>LedgerQuestionIsAnsweredFromTheEnvelopeShould</c>. Three properties, three arms, and the
    /// reason they cannot be merged is that each stays green under the others' mutations.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TombstoneTenantAsOwnFields_WhenTenantAErasesItsSubject()
    {
        await using var host = await BuildAsync();

        host.Tenant.TenantId = TenantA;
        var tenantAEnvelope = await host.EncryptAsync(SharedSubjectId, "tenant-A-personal-data"u8.ToArray());

        host.Tenant.TenantId = TenantB;
        _ = await host.EncryptAsync(SharedSubjectId, "tenant-B-personal-data"u8.ToArray());

        host.Tenant.TenantId = TenantA;
        _ = await host.EraseAsync(SharedSubjectId);

        // The erasure must reach the handle the WRITE path actually used. A derivation that disagreed with
        // the write -- an erasure reading ambient state instead of its own request's tenant, or collapsing
        // an absent tenant differently -- would destroy a key nothing was written under and leave this
        // readable, while reporting a completed erasure.
        (await host.DecryptAsync(tenantAEnvelope)).ShouldBeNull(
            "tenant A's own fields must be a tombstone after tenant A's erasure. Live data here means the "
            + "erasure destroyed a key nothing was ever written under and reported success.");
    }

    /// <summary>
    /// THE PREMISE the interleaving arms rest on, asserted separately so that its failure and the
    /// data-loss failure are distinguishable.
    /// </summary>
    [Fact]
    public async Task MintADifferentKey_ForEachTenantSharingOneSubjectId()
    {
        await using var host = await BuildAsync();

        host.Tenant.TenantId = TenantA;
        var tenantAEnvelope = await host.EncryptAsync(SharedSubjectId, "a"u8.ToArray());

        host.Tenant.TenantId = TenantB;
        var tenantBEnvelope = await host.EncryptAsync(SharedSubjectId, "b"u8.ToArray());

        tenantBEnvelope.KeyId.ShouldNotBe(
            tenantAEnvelope.KeyId,
            "two tenants sharing one data-subject identifier must not share one key handle -- a handle is a "
            + "name in a key store with no row beside it, so the name itself has to carry the tenant");
    }

    /// <summary>
    /// THE COMPOSITION ARM. Naive concatenation would make these two pairs one input, which is a
    /// cross-tenant collision introduced by the very change that removes one.
    /// </summary>
    [Fact]
    public void DeriveDifferentHandles_ForTenantAndSubjectSplitsThatConcatenateIdentically()
    {
        var hasher = TestDataSubjectHasher.Instance;

        var shortTenant = SubjectKeyHandle.ForSubjectHash(new TenantId("a"), "bc", hasher);
        var longTenant = SubjectKeyHandle.ForSubjectHash(new TenantId("ab"), "c", hasher);

        longTenant.ShouldNotBe(
            shortTenant,
            "(tenant \"a\", subject \"bc\") and (tenant \"ab\", subject \"c\") concatenate to the same "
            + "string. Without a length prefix on each field they derive one handle, so those two tenants "
            + "share a key and either one's erasure destroys it for both.");
    }

    /// <summary>
    /// THE UNTENANTED-SPELLING ARM, which is the mirror defect rather than the original one.
    /// </summary>
    /// <remarks>
    /// A single-tenant write resolves the ambient tenant to the framework default identity, while an
    /// erasure request filed with no tenant is PERSISTED as the reserved untenanted sentinel. Two
    /// spellings of "no tenant" would derive two handles, so the erasure would destroy a key nothing was
    /// written under — destroying nothing while reporting a completed erasure. Both must collapse onto one
    /// identity.
    /// </remarks>
    [Fact]
    public void DeriveOneHandle_ForEverySpellingOfAnAbsentTenant()
    {
        var hasher = TestDataSubjectHasher.Instance;
        var subjectHash = hasher.HashDataSubjectId(SharedSubjectId);

        var fromDefaultIdentity = SubjectKeyHandle.ForSubjectHash(
            new TenantId(TenantDefaults.DefaultTenantId), subjectHash, hasher);

        foreach (var spelling in new string?[] { null, "", "   ", TenantScope.UntenantedSentinel })
        {
            SubjectKeyHandle
                .ForSubjectHash(SubjectKeyHandle.OwningTenant(spelling), subjectHash, hasher)
                .ShouldBe(
                    fromDefaultIdentity,
                    $"the spelling '{spelling ?? "<null>"}' of an absent tenant must derive the same handle "
                    + "as the framework default identity a single-tenant write uses. Two spellings is an "
                    + "erasure that destroys nothing and reports success.");
        }
    }

    /// <summary>
    /// A derivation with no tenant must be inexpressible, not defaulted.
    /// </summary>
    [Fact]
    public void RefuseToDeriveAHandle_WhenNoTenantIsSupplied()
    {
        _ = Should.Throw<ArgumentNullException>(
            () => SubjectKeyHandle.ForSubjectHash(null!, "a-subject-hash", TestDataSubjectHasher.Instance));

        _ = Should.Throw<InvalidOperationException>(
            () => default(SubjectKeyHandle).Value,
            "a default handle names no key, and yielding an empty string would queue it for destruction");
    }

    private static async Task<CrossTenantHost> BuildAsync()
    {
        var tenant = new MutableTenantContext();

        var services = new ServiceCollection();
        _ = services.AddLogging();

        // Registered BEFORE the framework's own TryAdd, so this is the ambient context the whole stack
        // reads -- the stand-in for a tenant resolver or inbound middleware.
        _ = services.AddSingleton<ITenantContext>(tenant);

        _ = services.AddDataSubjectHashing();
        _ = services.Configure<DataSubjectHashingOptions>(
            options => options.Pepper = "test-pepper-0123456789abcdef0123456789ab");

        _ = services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
        _ = services.AddSingleton<IKeyManagementAdmin>(
            sp => (IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());

        _ = services.AddCryptoShredding();

        // The in-memory erasure store is both the erasure store and the destruction ledger, from one
        // instance -- so the row the erasure writes is the row the read-side tombstone test reads.
        _ = services.AddInMemoryErasureStore();

        // The erasure request carries no tenant of its own, so the store records the AMBIENT tenant. That
        // is the realistic multi-tenant shape: middleware establishes the scope and the request does not
        // restate it, which is also what makes write-tenant and erasure-tenant agreement a real property
        // rather than one the test hands over.
        _ = services.Configure<TenantContextOptions>(options => options.RequireTenant = true);

        var provider = services.BuildServiceProvider();

        // The encryption registry populates on first resolution and selects its primary once every hosted
        // service has started; a real Generic Host does both.
        _ = provider.GetServices<IEncryptionProvider>().ToList();
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        return new CrossTenantHost(provider, tenant);
    }

    /// <summary>An ambient tenant a test can move, standing in for a tenant resolver.</summary>
    private sealed class MutableTenantContext : ITenantContext
    {
        public string? TenantId { get; set; }

        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }

    private sealed class CrossTenantHost(ServiceProvider provider, MutableTenantContext tenant) : IAsyncDisposable
    {
        public MutableTenantContext Tenant { get; } = tenant;

        public ValueTask<EncryptedData> EncryptAsync(string subjectId, byte[] plaintext) =>
            provider.GetRequiredService<IFieldEncryptor>()
                .EncryptAsync(subjectId, RetentionScope.NotInAnAggregate, plaintext, CancellationToken.None);

        public ValueTask<byte[]?> DecryptAsync(EncryptedData envelope) =>
            provider.GetRequiredService<IFieldEncryptor>().DecryptAsync(envelope, CancellationToken.None);

        /// <summary>Runs a real erasure for the ambient tenant, key-shred only.</summary>
        public async Task<ErasureExecutionResult> EraseAsync(string subjectId)
        {
            var legalHolds = A.Fake<ILegalHoldService>();
            _ = A.CallTo(() => legalHolds.CheckHoldsAsync(
                    A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
                .Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

            var options = Options.Create(new ErasureOptions
            {
                KeyShredOnlyErasure = true,
                Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
            });

            var service = new ErasureService(
                provider.GetRequiredService<IErasureStore>(),
                provider.GetRequiredService<IKeyManagementAdmin>(),
                options,
                NullLogger<ErasureService>.Instance,
                provider.GetRequiredService<IDataSubjectHasher>(),
                legalHolds,
                null,
                null,
                TestAnnotationSource.None,
                TestRetentions.None,
                null);

            var request = new ErasureRequest
            {
                DataSubjectId = subjectId,
                IdType = DataSubjectIdType.UserId,
                LegalBasis = ErasureLegalBasis.DataSubjectRequest,
                RequestedBy = "test",
                GracePeriodOverride = TimeSpan.Zero,
            };

            _ = await service.RequestErasureAsync(request, CancellationToken.None);
            return await service.ExecuteAsync(request.RequestId, CancellationToken.None);
        }

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }
}
