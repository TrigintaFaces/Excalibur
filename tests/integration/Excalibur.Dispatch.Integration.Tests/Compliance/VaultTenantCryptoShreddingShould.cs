// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Vault;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Integration.Tests.Compliance;

/// <summary>Exercises tenant-qualified erasure against real Vault Transit keys and persisted event payloads.</summary>
[Collection(VaultTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class VaultTenantCryptoShreddingShould(VaultContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveOtherTenantsSameSubjectAfterErasureAndWithAFreshKeyProvider(bool provisionVictimLast)
    {
        fixture.DockerAvailable.ShouldBeTrue(fixture.InitializationError ?? "A real Vault container is required.");
        var tenant = new MutableTenantContext();
        var prefix = $"tenant-shred-{Guid.NewGuid():N}-";
        await using var provider = await BuildAsync(tenant, prefix);
        var store = provider.GetRequiredKeyedService<IEventStore>("default");
        var raw = provider.GetRequiredService<InMemoryEventStore>();
        var ledger = provider.GetRequiredService<InMemoryErasureStore>();

        var provisioningOrder = provisionVictimLast
            ? new[] { "tenant-b", "tenant-a" }
            : new[] { "tenant-a", "tenant-b" };
        foreach (var tenantId in provisioningOrder)
        {
            tenant.TenantId = tenantId;
            var evt = new SubjectRegistered
            {
                AggregateId = tenantId,
                SubjectId = "same-subject-42",
                FullName = $"private-{tenantId}",
            };
            var appended = await store.AppendAsync(tenantId, "Subject", [evt], -1, CancellationToken.None);
            appended.Success.ShouldBeTrue();
            var persisted = await raw.LoadAsync(tenantId, "Subject", CancellationToken.None);
            persisted.Count.ShouldBe(1);
            Encoding.UTF8.GetString(persisted[0].EventData).ShouldNotContain($"private-{tenantId}");
            var storedEvent = (SubjectRegistered)provider.GetRequiredService<IEventSerializer>()
                .DeserializeEvent(persisted[0].EventData, typeof(SubjectRegistered));
            EncryptedFieldBinding.TryReadEnvelope(typeof(SubjectRegistered).GetProperty(nameof(SubjectRegistered.FullName))!,
                storedEvent, out var framed).ShouldBeTrue();
            EncryptedData.TryParse(framed, out var envelope).ShouldBeTrue();
            envelope!.TenantId.ShouldBe(tenantId);
            using (var inventoryScope = provider.CreateScope())
            {
                var subjectHash = provider.GetRequiredService<IDataSubjectHasher>().HashDataSubjectId("same-subject-42");
                await inventoryScope.ServiceProvider.GetRequiredService<IDataInventoryStore>().RecordDiscoveredLocationAsync(
                    new DataLocation
                    {
                        TableName = "default-events",
                        FieldName = nameof(SubjectRegistered.FullName),
                        DataCategory = nameof(PersonalDataCategory.General),
                        RecordId = storedEvent.EventId,
                        KeyId = envelope.KeyId,
                        StoreKind = DataStoreKind.EventStore,
                    }, subjectHash, CancellationToken.None);
                var discovered = await inventoryScope.ServiceProvider.GetRequiredService<IDataInventoryService>()
                    .DiscoverAsync(subjectHash, DataSubjectIdType.Hash, tenantId, CancellationToken.None);
                discovered.Locations.Count.ShouldBe(1);
                discovered.Locations[0].RecordId.ShouldBe(storedEvent.EventId);
                discovered.Locations[0].KeyId.ShouldBe(envelope.KeyId);
            }
            (await ReadNameAsync(provider, tenantId)).ShouldBe($"private-{tenantId}");
        }

        tenant.TenantId = "tenant-a";
        var keyA = await provider.GetRequiredService<ISubjectKeyManager>().GetOrCreateKeyAsync(
            new TenantId("tenant-a"), "same-subject-42", RetentionScope.NotInAnAggregate, CancellationToken.None);
        (await provider.GetRequiredService<IKeyManagementProvider>()
            .GetKeyAsync(keyA.KeyId, CancellationToken.None)).ShouldNotBeNull();
        using (var scope = provider.CreateScope())
        {
            var erasure = scope.ServiceProvider.GetRequiredService<IErasureService>();
            var request = await erasure.RequestErasureAsync(new ErasureRequest
            {
                DataSubjectId = "same-subject-42",
                IdType = DataSubjectIdType.UserId,
                LegalBasis = ErasureLegalBasis.DataSubjectRequest,
                RequestedBy = "tenant-isolation-integration-test",
                GracePeriodOverride = TimeSpan.Zero,
            }, CancellationToken.None);
            var result = await scope.ServiceProvider.GetRequiredService<IErasureExecutor>()
                .ExecuteAsync(request.RequestId, CancellationToken.None);
            result.KeysDeleted.ShouldBe(1, $"Vault erasure failed: {result.ErrorMessage}");
            result.Success.ShouldBeTrue(result.ErrorMessage);
            var status = await erasure.GetStatusAsync(request.RequestId, CancellationToken.None);
            status.ShouldNotBeNull().Status.ShouldBe(ErasureRequestStatus.Completed);
            var certificate = await erasure.GenerateCertificateAsync(request.RequestId, CancellationToken.None);
            certificate.Payload.Verification.Verified.ShouldBeTrue();
            certificate.Payload.Verification.DeletedKeyIds.ShouldBe([keyA.KeyId]);
            ErasureCertificateVerifier.Verify(certificate,
                provider.GetRequiredService<IOptions<ErasureOptions>>().Value.Retention.SigningKey)
                .ShouldBe(ErasureCertificateVerificationResult.Verified);
        }

        (await ReadNameAsync(provider, "tenant-a")).ShouldBeNull();
        tenant.TenantId = "tenant-b";
        (await ReadNameAsync(provider, "tenant-b")).ShouldBe("private-tenant-b");

        // Reuse only persisted events and the destruction ledger. A new Vault provider, memory cache,
        // encryption provider and decorator must unwrap B's key from Vault after A's deletion.
        await using var fresh = await BuildAsync(tenant, prefix, raw, ledger);
        fresh.GetRequiredService<IKeyManagementProvider>()
            .ShouldNotBeSameAs(provider.GetRequiredService<IKeyManagementProvider>());
        fresh.GetRequiredService<InMemoryEventStore>().ShouldBeSameAs(raw);
        fresh.GetRequiredService<IKeyDestructionLedger>().ShouldBeSameAs(ledger);
        // A ledger tombstone alone can hide a provider that falsely reports completed deletion.
        // Verify disappearance through the fresh backend client as well as the public read path.
        (await fresh.GetRequiredService<IKeyManagementProvider>()
            .GetKeyAsync(keyA.KeyId, CancellationToken.None)).ShouldBeNull();
        (await ReadNameAsync(fresh, "tenant-b")).ShouldBe("private-tenant-b");
        tenant.TenantId = "tenant-a";
        (await ReadNameAsync(fresh, "tenant-a")).ShouldBeNull();
    }

    private async Task<ServiceProvider> BuildAsync(
        MutableTenantContext tenant, string prefix,
        InMemoryEventStore? persistedEvents = null, InMemoryErasureStore? persistedLedger = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ITenantContext>(tenant);
        _ = services.Configure<TenantContextOptions>(options => options.RequireTenant = true);
        _ = services.AddDataSubjectHashing();
        _ = services.Configure<DataSubjectHashingOptions>(options =>
            options.Pepper = "test-pepper-0123456789abcdef0123456789ab");
        _ = services.AddVaultKeyManagement(builder => builder
            .VaultUri(new Uri(fixture.VaultAddress)).TransitMountPath("transit").KeyNamePrefix(prefix));
        _ = services.Configure<VaultOptions>(options =>
        {
            options.Auth.AuthMethod = VaultAuthMethod.Token;
            options.Auth.Token = fixture.Token;
        });
        _ = services.AddEncryption(builder => builder
            .UseKeyManagement<AesGcmEncryptionProvider>("vault-aes").SetAsPrimary("vault-aes"));
        if (persistedLedger is not null)
        {
            _ = services.AddSingleton(persistedLedger);
        }
        _ = services.AddInMemoryErasureStore();
        _ = services.AddInMemoryDataInventoryStore();
        _ = services.AddDataInventoryService();
        _ = services.AddCryptoShredding();
        _ = services.AddNoLegalHolds();
        _ = services.AddGdprErasure(options =>
        {
            options.KeyShredOnlyErasure = true;
            options.MinimumGracePeriod = TimeSpan.Zero;
            options.DefaultGracePeriod = TimeSpan.Zero;
            options.Retention.SigningKey = RandomNumberGenerator.GetBytes(32);
        });
        _ = services.AddEventTypes(typeof(SubjectRegistered));
        _ = services.AddSingleton<IEventSerializer>(sp => new JsonEventSerializer(
            sp.GetRequiredService<IEventTypeRegistry>(), options: null, allowAssemblyScan: false));
        _ = services.AddInMemoryEventStore();
        if (persistedEvents is not null)
        {
            _ = services.AddSingleton(persistedEvents);
        }
        _ = services.AddEventSourcingCryptoShredding();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
        try
        {
            _ = provider.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<VaultKeyProvider>();
            provider.GetRequiredService<IKeyManagementAdmin>()
                .ShouldBeSameAs(provider.GetRequiredService<IKeyManagementProvider>());
            _ = provider.GetServices<IEncryptionProvider>().ToList();
            foreach (var hosted in provider.GetServices<IHostedService>())
            {
                await hosted.StartAsync(CancellationToken.None);
            }
            return provider;
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    private static async Task<string?> ReadNameAsync(ServiceProvider provider, string aggregateId)
    {
        var events = await provider.GetRequiredKeyedService<IEventStore>("default")
            .LoadAsync(aggregateId, "Subject", CancellationToken.None);
        events.Count.ShouldBe(1);
        return ((SubjectRegistered)provider.GetRequiredService<IEventSerializer>()
            .DeserializeEvent(events[0].EventData, typeof(SubjectRegistered))).FullName;
    }

    private sealed class MutableTenantContext : ITenantContext
    {
        public string? TenantId { get; set; }
        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }

    [MessageName("Test.VaultTenantCryptoShredding.SubjectRegistered")]
    private sealed record SubjectRegistered : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        public string AggregateId { get; init; } = string.Empty;
        public long Version { get; init; }
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public IDictionary<string, object>? Metadata { get; init; }
        [DataSubjectId]
        public string SubjectId { get; init; } = string.Empty;
        [PersonalData]
        public string? FullName { get; init; }
    }
}
