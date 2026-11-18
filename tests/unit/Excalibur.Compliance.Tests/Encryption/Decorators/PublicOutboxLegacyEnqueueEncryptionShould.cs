// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Tests.Encryption.Decorators;

// Actual public composition and real backend/crypto. This is deliberately JIT-only:
// legacy Enqueue retains its annotated provider-specific reflective serialization contract.
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class PublicOutboxLegacyEnqueueEncryptionShould(ITestOutputHelper output)
{
    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt, true)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll, true)]
    [InlineData(EncryptionMode.DecryptOnlyWritePlaintext, false)]
    [InlineData(EncryptionMode.Disabled, false)]
    public async Task PersistTheLegacySerializedMessageWithTheSelectedAtRestModeAndReadItThroughTheDecorator(
        EncryptionMode mode, bool encryptedAtRest)
    {
        await RunHostAsync(mode, async (host, token) =>
        {
            AssertRealProviders(host.Services);
            var raw = host.Services.GetRequiredService<InMemoryOutboxStore>();
            var decorated = host.Services.GetRequiredKeyedService<IOutboxStore>("default");
            decorated.ShouldNotBeSameAs(raw);
            var message = new LegacyMessage("private-value-Δ", 17);
            var context = Context(message, host.Services);
            await decorated.EnqueueAsync(message, context, token).ConfigureAwait(false);

            var persisted = (await raw.GetUnsentMessagesAsync(2, token).ConfigureAwait(false)).Single();
            persisted.Id.ShouldNotBeNullOrWhiteSpace();
            // FromContext creates an independent persisted envelope ID: never assume MessageId equality.
            persisted.CorrelationId.ShouldBe(context.CorrelationId);
            persisted.CausationId.ShouldBe(context.CausationId);
            persisted.TenantId.ShouldBe("legacy-owner");
            EncryptedData.IsFieldEncrypted(persisted.Payload).ShouldBe(encryptedAtRest,
                "AddOutboxEncryption must cover public EnqueueAsync, not only raw StageMessageAsync");
            var storedBytes = persisted.Payload.ToArray();
            persisted.DispatcherId.ShouldNotBeNullOrWhiteSpace();

            // Read failure snapshots publicly without stealing/releasing/reclaiming another lease.
            // This actual claimed failure transition does not rewrite the already persisted payload.
            var completion = ((IOutboxStore)raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>();
            (await completion.MarkFailedAsync(persisted.Id, "public-read-control", 1, persisted.DispatcherId!, token)
                .ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            var admin = decorated.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>();
            var read = (await admin.GetAllTenantsFailedMessagesAsync(0, null, 8, token).ConfigureAwait(false)).Single();
            read.Id.ShouldBe(persisted.Id);
            read.CorrelationId.ShouldBe(context.CorrelationId);
            read.CausationId.ShouldBe(context.CausationId);
            read.TenantId.ShouldBe("legacy-owner");
            using var json = JsonDocument.Parse(read.Payload);
            // Independent original InMemory serialization oracle, not Serialize() used as both expected/actual.
            json.RootElement.GetProperty(nameof(LegacyMessage.Secret)).GetString().ShouldBe(message.Secret);
            json.RootElement.GetProperty(nameof(LegacyMessage.Sequence)).GetInt32().ShouldBe(17);
            var rawAfter = (await raw.GetAllTenantsFailedMessagesAsync(0, null, 8, token).ConfigureAwait(false)).Single();
            rawAfter.Payload.ShouldBe(storedBytes, "decorated read must leave the actual raw row unchanged");
            EncryptedData.IsFieldEncrypted(rawAfter.Payload).ShouldBe(encryptedAtRest);
        });
    }

    [Fact]
    public async Task RefuseReadOnlyLegacyEnqueueBeforeAnyStoredRow()
    {
        await RunHostAsync(EncryptionMode.DecryptOnlyReadOnly, async (host, token) =>
        {
            AssertRealProviders(host.Services);
            var raw = host.Services.GetRequiredService<InMemoryOutboxStore>();
            var store = host.Services.GetRequiredKeyedService<IOutboxStore>("default");
            var message = new LegacyMessage("read-only-secret", 17);
            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                store.EnqueueAsync(message, Context(message, host.Services), token).AsTask());
            error.Message.ShouldContain("read-only", Case.Insensitive);
            (await raw.GetUnsentMessagesAsync(2, token).ConfigureAwait(false)).ShouldBeEmpty();
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task RefuseUndefinedNumericModeBeforeLegacyEnqueueStoresAnything(int mode)
    {
        await RunHostAsync((EncryptionMode)mode, async (host, token) =>
        {
            AssertRealProviders(host.Services);
            var raw = host.Services.GetRequiredService<InMemoryOutboxStore>();
            var store = host.Services.GetRequiredKeyedService<IOutboxStore>("default");
            var message = new LegacyMessage("undefined-mode-secret", 17);
            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                store.EnqueueAsync(message, Context(message, host.Services), token).AsTask());
            error.Message.ShouldContain("encryption mode", Case.Insensitive);
            error.Message.ShouldContain(mode.ToString(CultureInfo.InvariantCulture));
            (await raw.GetUnsentMessagesAsync(2, token).ConfigureAwait(false)).ShouldBeEmpty();
        });
    }

    private async Task RunHostAsync(EncryptionMode mode, Func<IHost, CancellationToken, Task> assertion)
    {
        var host = BuildHost(mode);
        Exception? primary = null;
        var cleanupErrors = new List<Exception>();
        try
        {
            using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await host.StartAsync(operation.Token).ConfigureAwait(false);
            await assertion(host, operation.Token).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            // Stop even after partial Start. Independent budget, never an expired workload token.
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await host.StopAsync(cleanup.Token).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (Exception error) { cleanupErrors.Add(error); }
            try { host.Dispose(); }
            catch (Exception error) { cleanupErrors.Add(error); }
        }
        foreach (var error in cleanupErrors)
        {
            if (primary is null) { primary = error; continue; }
            primary.Data["SecondaryHostCleanup" + primary.Data.Count.ToString(CultureInfo.InvariantCulture)] = error;
            try { output.WriteLine("Secondary legacy-enqueue test cleanup failure: " + error); }
            catch (Exception logError) { primary.Data["CleanupLoggingFailure"] = logError; }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static IHost BuildHost(EncryptionMode mode)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDevEncryption();
        builder.Services.AddInMemoryOutboxStore();
        builder.Services.Configure<EncryptionOptions>(options => options.Mode = mode);
        builder.Services.AddOutboxEncryption();
        return builder.Build();
    }

    private static MessageContext Context(IDispatchMessage message, IServiceProvider services)
    {
        var context = new MessageContext(message, services)
        {
            MessageId = "caller-message-id",
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = "caller-cause"
        };
        context.GetOrCreateIdentityFeature().TenantId = "legacy-owner";
        return context;
    }

    private static void AssertRealProviders(IServiceProvider services)
    {
        services.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<InMemoryKeyManagementProvider>();
        services.GetRequiredService<IEncryptionProviderRegistry>().GetPrimary().ShouldBeOfType<AesGcmEncryptionProvider>();
    }

    private sealed record LegacyMessage(string Secret, int Sequence) : IDispatchMessage;
}
