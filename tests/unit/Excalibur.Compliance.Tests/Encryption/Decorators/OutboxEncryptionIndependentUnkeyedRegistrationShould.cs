// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using System.Text;
using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Tests.Encryption.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Encryption")]
public sealed class OutboxEncryptionIndependentUnkeyedRegistrationShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncryptTheIndependentUnkeyedStoreEvenWhenAProviderKeyAlsoExists(bool addOtherProvider)
    {
        // Obtain an independent real public store without using its internal constructor.
        var independentServices = new ServiceCollection();
        independentServices.AddLogging();
        independentServices.AddInMemoryOutboxStore();
        await using var independentOwner = independentServices.BuildServiceProvider();
        var independentRaw = independentOwner.GetRequiredService<InMemoryOutboxStore>();
        var independent = independentOwner.GetRequiredService<IOutboxStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDevEncryption();
        // Public providers explicitly TryAdd the unkeyed convenience alias so a consumer's store wins.
        builder.Services.AddSingleton<IOutboxStore>(independent);
        if (addOtherProvider)
        {
            builder.Services.AddInMemoryOutboxStore();
        }
        builder.Services.AddOutboxEncryption();
        var host = builder.Build();
        await RunHostAsync(host, async () =>
        {
            host.Services.GetRequiredService<IEncryptionProviderRegistry>().GetPrimary().ShouldBeOfType<AesGcmEncryptionProvider>();
            var plain = Encoding.UTF8.GetBytes("independent plaintext must be encrypted at rest");
            var expectedPlain = plain.ToArray();
            var caller = new OutboundMessage("alias-control", plain, "destination")
            {
                Id = "independent-row", TenantId = "independent-tenant", ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            var original = caller.Payload;
            var consumerStore = host.Services.GetRequiredService<IOutboxStore>();
            await consumerStore.StageMessageAsync(caller, CancellationToken.None).ConfigureAwait(false);
            ReferenceEquals(caller.Payload, original).ShouldBeTrue();
            caller.Payload.ShouldBe(expectedPlain);
            if (addOtherProvider)
            {
                var keyed = host.Services.GetRequiredKeyedService<IOutboxStore>("inmemory");
                keyed.ShouldBeSameAs(host.Services.GetRequiredKeyedService<IOutboxStore>("default"));
                var providerCaller = new OutboundMessage("alias-control", plain.ToArray(), "destination")
                {
                    Id = "provider-row", TenantId = "provider-tenant", ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
                };
                await keyed.StageMessageAsync(providerCaller, CancellationToken.None).ConfigureAwait(false);
                var providerRaw = host.Services.GetRequiredService<InMemoryOutboxStore>();
                var providerStored = (await providerRaw.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.MaxValue, 10, CancellationToken.None)).Single();
                providerStored.Id.ShouldBe("provider-row");
                EncryptedData.IsFieldEncrypted(providerStored.Payload).ShouldBeTrue("the existing keyed provider route must stay encrypted");
                providerCaller.Payload.ShouldBe(expectedPlain);
                consumerStore.ShouldNotBeSameAs(keyed, "independent backing stores must not be collapsed into the provider default");
            }
            var stored = (await independentRaw.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.MaxValue, 10, CancellationToken.None)).Single();
            stored.Id.ShouldBe("independent-row");
            stored.TenantId.ShouldBe("independent-tenant");
            EncryptedData.IsFieldEncrypted(stored.Payload).ShouldBeTrue("AddOutboxEncryption must mediate this actual consumer-resolved unkeyed store, not only the other provider");
            var ciphertext = stored.Payload.ToArray();
            var admin = consumerStore.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
            var read = (await admin.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.MaxValue, 10, CancellationToken.None)).Single();
            read.Payload.ShouldBe(expectedPlain, "one public decrypt returns the original caller bytes");
            (await independentRaw.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.MaxValue, 10, CancellationToken.None)).Single().Payload.ShouldBe(ciphertext);
        }).ConfigureAwait(false);
    }

    private static async Task RunHostAsync(IHost host, Func<Task> assertion)
    {
        Exception? primary = null;
        var cleanupErrors = new List<Exception>();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await host.StartAsync(deadline.Token).ConfigureAwait(false);
            await assertion().ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        finally
        {
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
            primary.Data["SecondaryAliasControlCleanup" + primary.Data.Count] = error;
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }
}
