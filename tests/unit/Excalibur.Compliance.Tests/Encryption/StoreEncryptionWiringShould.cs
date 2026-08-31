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

    // ---- nbck06: the DOCUMENTED setup must actually write, not merely start ----
    //
    // The wiring guards above (StartAsync) prove the container is decorated; they never call the store, so
    // they cannot see a decorator that resolves cleanly and then throws on its first real write. This is
    // that gap: AddDevEncryption() (mirrors the documented AddEncryption(b =>
    // b.UseInMemoryKeyManagement(...).SetAsPrimary(...)) pattern) + AddInboxEncryption(), with
    // EncryptionOptions left at every default -- zero custom configuration, exactly what a consumer
    // following the docs verbatim gets. The inner store is faked (as elsewhere in this file); the
    // encryption call that fails happens BEFORE the decorator ever reaches it, so faking the inner store
    // does not hide this defect -- it isolates it.

    [Fact]
    public async Task Inbox_CreateEntry_SucceedsWithZeroCustomConfiguration_OnTheDocumentedSetup()
    {
        // LIVENESS -- RED pre-fix: InMemoryKeyManagementProvider auto-generates its bootstrap key with
        // Purpose=null, but EncryptionOptions.DefaultPurpose ("default", the framework's own default) is
        // what the encrypting decorator asks for, so purpose-based lookup never matches the auto-generated
        // key and every first write throws EncryptionException("No suitable key found for encryption").
        //
        // The inner store is faked (as elsewhere in this file) and its reference is captured BEFORE
        // registration, because AddInboxEncryption() decorates the "inmemory" TERMINAL descriptor itself
        // (not "default" -- see KeyedStoreServiceCollectionExtensions), so re-resolving "inmemory" after
        // decoration returns the decorator, not the fake. Keeping the direct handle lets this test both
        // configure the fake's return value AND capture the payload it actually received -- proving a REAL
        // encrypt ran (ciphertext reached the inner store), not merely that no exception surfaced.
        var innerFake = A.Fake<IInboxStore>();
        A.CallTo(() => innerFake.CreateEntryAsync(
                A<string>._, A<string>._, A<string>._, A<byte[]>._, A<IDictionary<string, object>>._, A<CancellationToken>._))
            .ReturnsLazily((string messageId, string handlerType, string messageType, byte[] payload, IDictionary<string, object> _, CancellationToken _) =>
                new ValueTask<InboxEntry>(new InboxEntry
                {
                    MessageId = messageId,
                    HandlerType = handlerType,
                    MessageType = messageType,
                    Payload = payload,
                    Status = InboxStatus.Received,
                    ReceivedAt = DateTimeOffset.UtcNow,
                }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevEncryption(); // real AES-256-GCM provider + registry + EncryptionOptions, zero custom config
        services.AddKeyedSingleton<IInboxStore>("inmemory", (_, _) => innerFake);
        services.AddKeyedSingleton<IInboxStore>("default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("inmemory"));
        services.AddInboxEncryption();

        using var sp = services.BuildServiceProvider();

        // Not part of nbck06: AddEncryption(...).SetAsPrimary(...) registers the primary-provider setup on
        // IEncryptionProviderInitializer, a plain (non-hosted) singleton nothing in production ever
        // resolves, so SetPrimary never runs in a real host either -- filed separately (hcr3y8). Worked
        // around here, exactly as the DI-resolution lock this bead was discovered from already does, so
        // this test isolates the ONE defect it exists to prove.
        _ = sp.GetServices<IEncryptionProvider>().ToList(); // triggers the lazy AddSingleton<IEncryptionProvider> factory, which self-Registers
        sp.GetRequiredService<IEncryptionProviderRegistry>().SetPrimary("dev-inmemory");

        var store = sp.GetRequiredKeyedService<IInboxStore>("default");
        var plaintext = new byte[] { 1, 2, 3 };

        var entry = await store.CreateEntryAsync(
            "m1",
            "handler",
            "type",
            plaintext,
            new Dictionary<string, object>(StringComparer.Ordinal),
            CancellationToken.None);

        // The fake inner store echoes back whatever it received, so a payload that arrived UNCHANGED
        // means encryption was skipped, not merely that no exception happened to surface.
        entry.Payload.ShouldNotBe(plaintext, "the payload reaching the inner store must be REAL ciphertext, not the untouched plaintext");
    }

    // The bead names EncryptingOutboxStoreDecorator as sharing the defect "by the same call shape" but
    // only confirms it for inbox -- same _registry.EncryptAsync(payload, _defaultContext, ct) pattern
    // (EncryptingOutboxStoreDecorator.cs:58-61), same shared IKeyManagementProvider, so the fix above
    // (which lives in the provider, not either decorator) covers both. This makes that "likely" a proven
    // fact rather than an inference.
    [Fact]
    public async Task Outbox_StageMessage_SucceedsWithZeroCustomConfiguration_OnTheDocumentedSetup()
    {
        var innerFake = A.Fake<IOutboxStore>();
        byte[]? capturedPayload = null;
        A.CallTo(() => innerFake.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
            .Invokes((OutboundMessage message, CancellationToken _) => capturedPayload = message.Payload);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevEncryption();
        services.AddKeyedSingleton<IOutboxStore>("inmemory", (_, _) => innerFake);
        services.AddKeyedSingleton<IOutboxStore>("default", (sp, _) => sp.GetRequiredKeyedService<IOutboxStore>("inmemory"));
        services.AddOutboxEncryption();

        using var sp = services.BuildServiceProvider();

        // Not part of nbck06 -- see the identical note on the inbox arm above (hcr3y8).
        _ = sp.GetServices<IEncryptionProvider>().ToList();
        sp.GetRequiredService<IEncryptionProviderRegistry>().SetPrimary("dev-inmemory");

        var store = sp.GetRequiredKeyedService<IOutboxStore>("default");
        var plaintext = new byte[] { 4, 5, 6 };
        var message = new OutboundMessage("type", plaintext, "destination");

        await store.StageMessageAsync(message, CancellationToken.None);

        capturedPayload.ShouldNotBeNull();
        capturedPayload.ShouldNotBe(plaintext, "the payload reaching the inner store must be REAL ciphertext, not the untouched plaintext");
    }

    // hcr3y8: establishes the ORDERING against nbck06 -- hcr3y8 (no primary provider configured) blocked
    // the pipeline entirely and MASKED nbck06 (wrong-purpose key lookup) behind it; a consumer never saw
    // nbck06's error until hcr3y8 was fixed. This is the definitive proof for BOTH: a REAL
    // Microsoft.Extensions.Hosting IHost (not a bare ServiceProvider), built and started exactly as a
    // real application would, performing a real write through the documented entry points, with NO
    // manual SetPrimary(...) or GetServices<IEncryptionProvider>() call anywhere in this test -- the
    // exact step production omits and every other lock in this file (and the pre-existing
    // SqlServerEncryptionDiResolutionShould / EventStoreEncryptionWiringShould) had to add by hand.
    [Fact]
    public async Task Inbox_CreateEntry_SucceedsThroughARealHost_WithNoManualWiring_OnTheDocumentedSetup()
    {
        var innerFake = A.Fake<IInboxStore>();
        A.CallTo(() => innerFake.CreateEntryAsync(
                A<string>._, A<string>._, A<string>._, A<byte[]>._, A<IDictionary<string, object>>._, A<CancellationToken>._))
            .ReturnsLazily((string messageId, string handlerType, string messageType, byte[] payload, IDictionary<string, object> _, CancellationToken _) =>
                new ValueTask<InboxEntry>(new InboxEntry
                {
                    MessageId = messageId,
                    HandlerType = handlerType,
                    MessageType = messageType,
                    Payload = payload,
                    Status = InboxStatus.Received,
                    ReceivedAt = DateTimeOffset.UtcNow,
                }));

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDevEncryption(); // the documented entry point, zero custom EncryptionOptions
        builder.Services.AddKeyedSingleton<IInboxStore>("inmemory", (_, _) => innerFake);
        builder.Services.AddKeyedSingleton<IInboxStore>("default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("inmemory"));
        builder.Services.AddInboxEncryption();

        using var host = builder.Build();
        await host.StartAsync(); // the ONLY startup step a real consumer takes -- starts every IHostedService

        var store = host.Services.GetRequiredKeyedService<IInboxStore>("default");
        var plaintext = new byte[] { 7, 8, 9 };

        var entry = await store.CreateEntryAsync(
            "m1", "handler", "type", plaintext, new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None);

        entry.Payload.ShouldNotBe(plaintext, "the payload reaching the inner store must be REAL ciphertext, not the untouched plaintext");

        await host.StopAsync();
    }
}
