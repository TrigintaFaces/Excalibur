// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Integration.Tests.Compliance;

/// <summary>Reconstructs stores and services across irreversible-destruction interruption windows.</summary>
internal static class ErasureRecoveryControls
{
    internal static async Task RecoverAsync(Func<IErasureStore> createStore, IDataSubjectHasher hasher,
        string owner, bool deferred, CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(hasher);
        services.AddSingleton<ITenantContext>(new OwnerContext(owner));
        services.Configure<TenantContextOptions>(options => options.RequireTenant = true);
        services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
        services.AddCryptoShredding();
        services.AddSingleton((IKeyDestructionLedger)createStore());
        await using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IEncryptionProvider>().ToList();
        var started = new List<IHostedService>();
        try
        {
            foreach (var hosted in provider.GetServices<IHostedService>())
            {
                await hosted.StartAsync(ct);
                started.Add(hosted);
            }
            using var scope = provider.CreateScope();
            var keys = scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>();
            var realAdmin = (IKeyManagementAdmin)keys;
            var fields = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
            var subject = $"recovery-{Guid.NewGuid():N}";
            var plaintext = "personal data surviving until destruction"u8.ToArray();
            var envelope = await fields.EncryptAsync(subject, RetentionScope.NotInAnAggregate,
                plaintext, ct);
            (await fields.DecryptAsync(envelope, owner, ct)).ShouldBe(plaintext);
            var generation = envelope.KeyGeneration.ShouldNotBeNull();
            var legalHolds = A.Fake<ILegalHoldService>();
            var noHolds = new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] };
            A.CallTo(() => legalHolds.CheckHoldsAsync(A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
                .Returns(Task.FromResult(noHolds));
            A.CallTo(() => legalHolds.CheckHoldsByHashAsync(A<string>._, A<string?>._, A<CancellationToken>._))
                .Returns(Task.FromResult(noHolds));
            var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
            {
                KeyShredOnlyErasure = true,
                Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
            });
            var retentions = A.Fake<IErasureRetentionRegistry>();
            ErasureService CreateService(IErasureStore store, IKeyManagementAdmin admin) => new(store, admin,
                options, NullLogger<ErasureService>.Instance, hasher, legalHolds, null, null,
                new NoAnnotatedDomain(), retentions);
            var request = new ErasureRequest
            {
                DataSubjectId = subject, TenantId = owner, IdType = DataSubjectIdType.UserId,
                LegalBasis = ErasureLegalBasis.DataSubjectRequest, RequestedBy = "recovery-control",
            };
            var firstStore = createStore();
            IKeyManagementAdmin firstAdmin = realAdmin;
            IErasureStore executionStore = firstStore;
            if (deferred)
            {
                firstAdmin = A.Fake<IKeyManagementAdmin>(fake => fake.Wrapping(realAdmin)
                    .Implements<IKeyManagementProvider>());
                A.CallTo(() => ((IServiceProvider)firstAdmin).GetService(typeof(IKeyManagementProvider))).Returns(keys);
                A.CallTo(() => firstAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
                    .Returns(Task.FromResult(KeyDestructionOutcome.ScheduledAt(DateTimeOffset.UtcNow.AddDays(1))));
            }
            else
            {
                executionStore = A.Fake<IErasureStore>(fake => fake.Wrapping(firstStore));
                A.CallTo(() => executionStore.RecordKeyDestroyedAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
                    .Returns(Task.FromException(new IOException("Injected interruption after physical destruction.")));
            }
            var firstService = CreateService(executionStore, firstAdmin);
            _ = await firstService.RequestErasureAsync(request, ct);
            (await firstService.ExecuteAsync(request.RequestId, ct)).Success.ShouldBeFalse();
            // Reconstruct the persistence object: subsequent observations use fresh SQL connections.
            var recoveredStore = createStore();
            (await recoveredStore.GetStagedKeyGenerationsAsync(request.RequestId, envelope.KeyId, ct))
                .ShouldContain(generation);
            if (deferred)
            {
                (await realAdmin.DeleteKeyAsync(envelope.KeyId, 0, ct)).State.ShouldBe(KeyDestructionState.Completed);
                // Simulate the other interruption window: ledger committed and its intent cleared,
                // but completion status/certificate never written.
                await recoveredStore.RecordKeyDestroyedAsync(request.RequestId, envelope.KeyId, generation, ct);
                (await createStore().GetStagedKeyGenerationsAsync(request.RequestId, envelope.KeyId, ct)).ShouldBeEmpty();
                var verifier = new ErasureVerificationService(recoveredStore, keys, A.Fake<IDataInventoryService>(),
                    A.Fake<IAuditStore>(), options, NullLogger<ErasureVerificationService>.Instance);
                var completion = new ErasureCompletionProcessor(CreateService(recoveredStore, realAdmin),
                    recoveredStore, verifier, NullLogger<ErasureCompletionProcessor>.Instance);
                var completed = await completion.CompletePendingErasuresAsync(ct);
                completed.ShouldBe(1, (await recoveredStore.GetStatusAsync(request.RequestId, ct))?.ErrorMessage);
            }
            else
            {
                (await keys.GetKeyAsync(envelope.KeyId, ct)).ShouldBeNull();
                (await recoveredStore.UpdateStatusAsync(request.RequestId, ErasureRequestStatus.Scheduled,
                    "Explicit scheduler/operator retry", ct)).ShouldBeTrue();
                var retried = await CreateService(recoveredStore, realAdmin).ExecuteAsync(request.RequestId, ct);
                retried.Success.ShouldBeTrue(retried.ErrorMessage);
            }
            var finalStore = createStore();
            (await finalStore.GetStatusAsync(request.RequestId, ct))!.Status.ShouldBe(ErasureRequestStatus.Completed);
            var certificates = finalStore.GetService(typeof(IErasureCertificateStore)) as IErasureCertificateStore
                ?? throw new InvalidOperationException("The durable store does not expose certificate retrieval.");
            (await certificates.GetCertificateAsync(request.RequestId, ct)).ShouldNotBeNull();
            (await ((IKeyDestructionLedger)finalStore).IsGenerationDestroyedAsync(envelope.KeyId, generation, ct)).ShouldBeTrue();
            (await finalStore.GetStagedKeyGenerationsAsync(request.RequestId, envelope.KeyId, ct)).ShouldBeEmpty();
            (await fields.DecryptAsync(envelope, owner, ct)).ShouldBeNull();
        }
        finally
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            for (var index = started.Count - 1; index >= 0; index--)
            {
                await started[index].StopAsync(stop.Token);
            }
        }
    }

    private sealed class OwnerContext(string owner) : ITenantContext
    {
        public string? TenantId => owner;
        public bool HasTenant => true;
    }

    // This control's domain is raw field bytes. Other test classes' annotated fixture entities
    // are not its inventory; do not let an ambient assembly scan change the recovery experiment.
    private sealed class NoAnnotatedDomain : IPersonalDataAnnotationSource
    {
        public PersonalDataAnnotationScan GetAnnotatedCategories() =>
            new(new HashSet<PersonalDataCategory>(), ScanEstablished: true);
    }
}
