// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Compliance.Encryption.DependencyInjection;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Author≠impl WIRE locks for Lane D store-encryption (Frontend impl @fe66f3fb6). Binds the COMMITTED
/// surface through a real DI container (<c>BuildServiceProvider</c> → resolve), asserting the emitted
/// behavior — the fail-closed guard actually fires, and only when it should — not mere registration presence.
/// </summary>
/// <remarks>
/// <b>safety∧liveness (SA-mandated):</b> a guard asserted only on its safety half (throws when unwired) is
/// satisfied by a guard that throws unconditionally — inert. Each guard therefore has BOTH arms: safety
/// (crypto-shred configured + store present + encryption marker absent ⇒ host start throws) AND liveness
/// (the permitted configurations start cleanly — wired, and no-crypto-shred). The guard probes the store
/// <em>keyed</em> (<c>IsKeyedService(IInboxStore,"default")</c>); a non-keyed probe would read the keyed
/// store as absent and never fire.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class StoreEncryptionWiringShould
{
    // Mirrors the provider registration shape (AddInMemoryInboxStore): a terminal provider-keyed store
    // ("inmemory") plus the "default" forwarding alias the guard and decoration target.
    private static void AddKeyedInboxStore(IServiceCollection services)
    {
        services.AddKeyedSingleton<IInboxStore>("inmemory", (_, _) => A.Fake<IInboxStore>());
        services.AddKeyedSingleton<IInboxStore>("default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("inmemory"));
    }

    private static void AddKeyedOutboxStore(IServiceCollection services)
    {
        services.AddKeyedSingleton<IOutboxStore>("inmemory", (_, _) => A.Fake<IOutboxStore>());
        services.AddKeyedSingleton<IOutboxStore>("default", (sp, _) => sp.GetRequiredKeyedService<IOutboxStore>("inmemory"));
    }

    private static ServiceProvider BuildInbox(bool cryptoShred, bool store, bool encryptionWired)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevEncryption();          // real AES-256-GCM provider + registry + EncryptionOptions
        if (cryptoShred)
        {
            services.AddCryptoShredding();     // registers SubjectFieldCryptor (the guard's crypto-shred signal)
        }

        if (store)
        {
            AddKeyedInboxStore(services);      // BEFORE decoration — AddInboxEncryption decorates existing descriptors
        }

        if (encryptionWired)
        {
            services.AddInboxEncryption();     // decorates the "inmemory" store + records InboxEncryptionMarker
        }

        services.AddStoreEncryptionWiringGuards();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildOutbox(bool cryptoShred, bool store, bool encryptionWired)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevEncryption();
        if (cryptoShred)
        {
            services.AddCryptoShredding();
        }

        if (store)
        {
            AddKeyedOutboxStore(services);
        }

        if (encryptionWired)
        {
            services.AddOutboxEncryption();
        }

        services.AddStoreEncryptionWiringGuards();
        return services.BuildServiceProvider();
    }

    private static T Validator<T>(ServiceProvider sp)
        where T : IHostedService
        => sp.GetServices<IHostedService>().OfType<T>().Single();

    // ---- INBOX ----

    [Fact]
    public async Task Inbox_FailClosed_WhenCryptoShredConfiguredButStoreUnencrypted()
    {
        // SAFETY: crypto-shred on + inbox store present + NOT wired for encryption ⇒ host start must throw.
        using var sp = BuildInbox(cryptoShred: true, store: true, encryptionWired: false);
        var validator = Validator<InboxEncryptionWiringValidator>(sp);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));
        ex.Message.ShouldContain("inbox", Case.Insensitive);
    }

    [Fact]
    public async Task Inbox_StartsCleanly_WhenEncryptionIsWired()
    {
        // LIVENESS: the permitted, correctly-wired configuration must NOT throw — a guard that fires here
        // would be an inert always-throw that also passes the safety test.
        using var sp = BuildInbox(cryptoShred: true, store: true, encryptionWired: true);
        var validator = Validator<InboxEncryptionWiringValidator>(sp);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Inbox_StartsCleanly_WhenNoCryptoShredConfigured()
    {
        // LIVENESS: the guard is narrow — with no crypto-shred it must not fire even though the store is
        // unencrypted (a consumer who never opted into crypto-shred is not in the fail-closed condition).
        using var sp = BuildInbox(cryptoShred: false, store: true, encryptionWired: false);
        var validator = Validator<InboxEncryptionWiringValidator>(sp);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    // ---- OUTBOX ----

    [Fact]
    public async Task Outbox_FailClosed_WhenCryptoShredConfiguredButStoreUnencrypted()
    {
        using var sp = BuildOutbox(cryptoShred: true, store: true, encryptionWired: false);
        var validator = Validator<OutboxEncryptionWiringValidator>(sp);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));
        ex.Message.ShouldContain("outbox", Case.Insensitive);
    }

    [Fact]
    public async Task Outbox_StartsCleanly_WhenEncryptionIsWired()
    {
        using var sp = BuildOutbox(cryptoShred: true, store: true, encryptionWired: true);
        var validator = Validator<OutboxEncryptionWiringValidator>(sp);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Outbox_StartsCleanly_WhenNoCryptoShredConfigured()
    {
        using var sp = BuildOutbox(cryptoShred: false, store: true, encryptionWired: false);
        var validator = Validator<OutboxEncryptionWiringValidator>(sp);

        await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
    }

    // ---- DECORATION (single-wrap through the real container) ----

    [Fact]
    public void AddInboxEncryption_DecoratesTheDefaultKeyedStore_SingleWrap()
    {
        // The "default" forwarding alias must resolve the ENCRYPTING decorator (wrap applied to the terminal
        // provider-keyed store), never the raw store and never a double wrap.
        using var sp = BuildInbox(cryptoShred: true, store: true, encryptionWired: true);

        var resolved = sp.GetRequiredKeyedService<IInboxStore>("default");
        resolved.ShouldBeOfType<EncryptingInboxStoreDecorator>();
    }

    [Fact]
    public void AddOutboxEncryption_DecoratesTheDefaultKeyedStore_SingleWrap()
    {
        using var sp = BuildOutbox(cryptoShred: true, store: true, encryptionWired: true);

        var resolved = sp.GetRequiredKeyedService<IOutboxStore>("default");
        resolved.ShouldBeOfType<EncryptingOutboxStoreDecorator>();
    }
}
