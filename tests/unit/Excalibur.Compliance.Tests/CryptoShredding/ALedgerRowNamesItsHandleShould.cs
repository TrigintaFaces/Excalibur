// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Tests.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// The tombstone oracle is keyed on a HANDLE AND a generation, so a destruction at one handle never reports
/// a different handle's identically named generation as destroyed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant this protects, and why it is not the cross-tenant handle defect.</b> The handle fix makes
/// a handle identify a (tenant, subject) pair. This one concerns the SECOND invariant the oracle depends on:
/// that a generation identifies key material uniquely across every handle. Nothing enforces it.
/// <see cref="KeyGeneration"/> constrains SHAPE and not ENTROPY and says so in its own documentation, so a
/// CONSUMER-SUPPLIED <see cref="IKeyManagementProvider"/> that DERIVED its generation from the data subject
/// yields one 32-hex string for two distinct handles. With the ledger keyed on the generation alone, that is
/// one row, and one tenant's destruction reports another tenant's live personal data as lawfully erased.
/// </para>
/// <para>
/// The two are independent: this is reachable with the handle fix fully in place, and closing it does not
/// need the handle fix. Both halves are reachable here because the derivation is the PROVIDER's, which a
/// consumer supplies.
/// </para>
/// <para>
/// <b>P1 rather than P0.</b> Every shipped provider mints from a cryptographic random source through
/// <see cref="KeyGeneration.Mint"/>, so no shipped configuration reaches the state these arms construct. The
/// fixture therefore supplies a deriving ledger-and-provider pair by hand, which is exactly what a consumer
/// is free to write.
/// </para>
/// <para>
/// RED input: key the in-memory store's ledger on the generation alone.
/// <see cref="NotTombstoneAnotherHandlesCiphertext_WhenAProviderDerivesOneGenerationForBoth"/> then returns a
/// tombstone over live data, which is the defect verbatim.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ALedgerRowNamesItsHandleShould
{
    private const string Generation = "0123456789abcdef0123456789abcdef";

    /// <summary>
    /// THE SAFETY ARM, over the real store: one generation, two handles, one destruction.
    /// </summary>
    [Fact]
    public async Task NotReportADestructionAtOneHandle_AsADestructionAtAnother()
    {
        await using var provider = BuildStore();
        var store = provider.GetRequiredService<InMemoryErasureStore>();
        var ledger = (IKeyDestructionLedger)store;

        var destroyed = "handle-destroyed";
        var untouched = "handle-untouched";

        var requestId = await GivenARequestAsync(store);
        await store.StageKeyDestructionAsync(requestId, destroyed, Generation, CancellationToken.None);
        await store.RecordKeyDestroyedAsync(requestId, destroyed, Generation, CancellationToken.None);

        // LIVENESS first. Without it the assertion below is satisfied by a ledger that answers false for
        // everything, and a genuinely erased subject's reads would fail forever rather than report the erasure.
        (await ledger.IsGenerationDestroyedAsync(destroyed, Generation, CancellationToken.None)).ShouldBeTrue(
            "the handle whose key was destroyed must report its generation as destroyed");

        // SAFETY. One generation at two handles is what a provider DERIVING its generations produces.
        (await ledger.IsGenerationDestroyedAsync(untouched, Generation, CancellationToken.None)).ShouldBeFalse(
            "a destruction recorded at one handle must not report a DIFFERENT handle's identically named "
            + "generation as destroyed. True here means the two handles share one ledger row, so a read of the "
            + "untouched handle's ciphertext returns a tombstone -- asserting a lawful erasure over personal "
            + "data that was never erased and whose key is still live.");
    }

    /// <summary>
    /// The same property through the READ PATH, which is where a consumer experiences it: the tombstone.
    /// </summary>
    [Fact]
    public async Task NotTombstoneAnotherHandlesCiphertext_WhenAProviderDerivesOneGenerationForBoth()
    {
        await using var provider = BuildStore();
        var store = provider.GetRequiredService<InMemoryErasureStore>();
        var ledger = (IKeyDestructionLedger)store;

        var requestId = await GivenARequestAsync(store);
        await store.StageKeyDestructionAsync(requestId, "tenant-a-handle", Generation, CancellationToken.None);
        await store.RecordKeyDestroyedAsync(requestId, "tenant-a-handle", Generation, CancellationToken.None);

        // The envelope a DIFFERENT handle's write produced, naming the generation the deriving provider gave
        // both of them. This is the read FieldEncryptor performs, with the handle taken from the envelope.
        var victim = new EncryptedData
        {
            KeyId = "tenant-b-handle",
            KeyVersion = 1,
            KeyGeneration = Generation,
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            Ciphertext = [1, 2, 3],
            Iv = [4, 5, 6],
        };

        (await ledger.IsGenerationDestroyedAsync(victim.KeyId, victim.KeyGeneration!, CancellationToken.None))
            .ShouldBeFalse(
                "the read path asks about the handle the ENVELOPE names. A true here is the tombstone this "
                + "framework produces to state a lawful erasure, returned over data no erasure request ever "
                + "named and whose key is still live -- irreversible once the consumer acts on it, and nothing "
                + "downstream can tell it from a real erasure.");
    }

    /// <summary>
    /// A second destruction at ONE handle is a distinct record, which is the property the generation-alone key
    /// bought and which the composite must not lose.
    /// </summary>
    /// <remarks>
    /// This is the objection the schema comments raised against keying on the handle, and it was correct
    /// against the handle ALONE. The pair admits many generations per handle, so it does not apply -- and this
    /// arm is what keeps that true rather than merely argued.
    /// </remarks>
    [Fact]
    public async Task RecordBothGenerations_WhenOneHandleIsDestroyedTwice()
    {
        await using var provider = BuildStore();
        var store = provider.GetRequiredService<InMemoryErasureStore>();
        var ledger = (IKeyDestructionLedger)store;

        var first = "1111111111111111aaaaaaaaaaaaaaaa";
        var second = "2222222222222222bbbbbbbbbbbbbbbb";

        var requestId = await GivenARequestAsync(store);
        await store.RecordKeyDestroyedAsync(requestId, "one-handle", first, CancellationToken.None);
        await store.RecordKeyDestroyedAsync(requestId, "one-handle", second, CancellationToken.None);

        (await ledger.IsGenerationDestroyedAsync("one-handle", first, CancellationToken.None)).ShouldBeTrue();
        (await ledger.IsGenerationDestroyedAsync("one-handle", second, CancellationToken.None)).ShouldBeTrue(
            "re-provisioning a handle and destroying it again is a second destruction, not a repeat. A key "
            + "that absorbed it would leave every read of the second generation's ciphertext failing instead "
            + "of reporting the subject's erasure.");
    }

    /// <summary>
    /// The consumer-asserted write requires a handle, so a row keyed on the generation alone is no longer
    /// constructible through the public surface.
    /// </summary>
    [Fact]
    public async Task RequireAHandle_OnTheConsumerAssertedWrite()
    {
        await using var provider = BuildStore();
        var ledger = provider.GetRequiredService<IKeyDestructionLedger>();

        _ = await Should.ThrowAsync<ArgumentException>(
            () => ledger.RecordDestroyedGenerationAsync(null!, Generation, CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(
            () => ledger.RecordDestroyedGenerationAsync(string.Empty, Generation, CancellationToken.None));

        // And the asserted row is readable back under its handle and nowhere else.
        await ledger.RecordDestroyedGenerationAsync("asserted-handle", Generation, CancellationToken.None);

        (await ledger.IsGenerationDestroyedAsync("asserted-handle", Generation, CancellationToken.None))
            .ShouldBeTrue("a caller-asserted destruction must be readable back, or the assertion is lost");
        (await ledger.IsGenerationDestroyedAsync("other-handle", Generation, CancellationToken.None))
            .ShouldBeFalse("and it must not answer for a handle the caller never named");
    }

    private static async Task<Guid> GivenARequestAsync(InMemoryErasureStore store)
    {
        var request = new ErasureRequest
        {
            DataSubjectId = "subject-1",
            IdType = DataSubjectIdType.UserId,
            LegalBasis = ErasureLegalBasis.DataSubjectRequest,
            RequestedBy = "test",
        };

        await store.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);
        return request.RequestId;
    }

    private static ServiceProvider BuildStore()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddDataSubjectHashing();
        _ = services.Configure<DataSubjectHashingOptions>(
            options => options.Pepper = "test-pepper-0123456789abcdef0123456789ab");
        _ = services.AddInMemoryErasureStore();

        var provider = services.BuildServiceProvider();
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            _ = hostedService.StartAsync(CancellationToken.None);
        }

        return provider;
    }
}
