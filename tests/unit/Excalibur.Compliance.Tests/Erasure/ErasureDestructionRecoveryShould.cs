// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.CryptoShredding;
using Microsoft.Extensions.DependencyInjection;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class ErasureDestructionRecoveryShould
{
    [Fact]
    public async Task DrainPriorGenerationIntentWhenRemintedKeyIsDestroyedImmediatelyOnSameRequestRetry()
    {
        using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
        const string subject = "immediate-remint-recovery";
        var handle = SubjectKeyHandle.ForSubject(new TenantId(TenantDefaults.DefaultTenantId),
            subject, TestDataSubjectHasher.Instance).Value;
        var firstRecord = true;
        var harness = new ErasureLifecycleHarness(keys, keys, decorateStore: store =>
        {
            var wrapped = A.Fake<IErasureStore>(options => options.Wrapping(store));
            A.CallTo(() => wrapped.RecordKeyDestroyedAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily((Guid request, string key, string generation, CancellationToken ct) =>
                {
                    if (firstRecord)
                    {
                        firstRecord = false;
                        return Task.FromException(new IOException("First real destruction completed; ledger insertion interrupted."));
                    }
                    return store.RecordKeyDestroyedAsync(request, key, generation, ct);
                });
            return wrapped;
        });
        var services = new ServiceCollection();
        _ = services.AddDefaultTenantContext();
        using var container = services.BuildServiceProvider();
        var manager = new SubjectKeyManager(keys, TestDataSubjectHasher.Instance, TestRetentions.None);
        using var aes = new AesGcmEncryptionProvider(keys, NullLogger<AesGcmEncryptionProvider>.Instance);
        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.GetPrimary()).Returns(aes);
        A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(aes);
        var ledger = (IKeyDestructionLedger)harness.Store;
        var encryptor = new FieldEncryptor(manager, registry, ledger, container.GetRequiredService<ITenantContext>());
        var plaintext = "original personal data"u8.ToArray();
        var oldEnvelope = await encryptor.EncryptAsync(subject, RetentionScope.NotInAnAggregate, plaintext, CancellationToken.None);
        var g1 = oldEnvelope.KeyGeneration ?? throw new InvalidOperationException("Missing original generation.");
        (await encryptor.DecryptAsync(oldEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None)).ShouldBe(plaintext);

        var (requestId, _, first) = await harness.SubmitAndExecuteAsync(subject);
        first.Success.ShouldBeFalse();
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldBeNull();
        (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);
        (await ledger.IsGenerationDestroyedAsync(handle, g1, CancellationToken.None)).ShouldBeFalse();
        (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
            .ShouldBe(new[] { g1 });

        // Ordinary field encryption remints the destroyed handle, without staging synthetic history.
        var newEnvelope = await encryptor.EncryptAsync(subject, RetentionScope.NotInAnAggregate,
            "later personal data"u8.ToArray(), CancellationToken.None);
        var g2 = newEnvelope.KeyGeneration ?? throw new InvalidOperationException("Missing reminted generation.");
        g2.ShouldNotBe(g1);
        newEnvelope.KeyId.ShouldBe(handle);
        (await encryptor.DecryptAsync(newEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None))
            .ShouldBe("later personal data"u8.ToArray());
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldNotBeNull();
        (await harness.Store.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled,
            "Operator retry of the same stopped request after ordinary remint", CancellationToken.None)).ShouldBeTrue();
        var retried = await harness.Service.ExecuteAsync(requestId, CancellationToken.None);

        // The real second Delete returns Completed, not NotFound. Completion requires both durable rows.
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldBeNull();
        (await ledger.IsGenerationDestroyedAsync(handle, g1, CancellationToken.None)).ShouldBeTrue();
        (await ledger.IsGenerationDestroyedAsync(handle, g2, CancellationToken.None)).ShouldBeTrue();
        (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None)).ShouldBeEmpty();
        retried.Success.ShouldBeTrue(retried.ErrorMessage);
        (await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
        (await encryptor.DecryptAsync(oldEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None)).ShouldBeNull();
        (await encryptor.DecryptAsync(newEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None)).ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepairRemainingGenerationAfterPartialLedgerSuccessOnlyWithPositiveVerification(bool confirmed)
    {
        using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
        const string subject = "partial-generation-recovery";
        var handle = SubjectKeyHandle.ForSubject(new TenantId(TenantDefaults.DefaultTenantId),
            subject, TestDataSubjectHasher.Instance).Value;
        _ = await keys.RotateKeyAsync(handle, EncryptionAlgorithm.Aes256Gcm,
            "crypto-shred", null, CancellationToken.None);
        var older = (await keys.GetKeyAsync(handle, CancellationToken.None))!.Generation!.ToString();
        // Rotation versions retain their generation. A destroyed handle reminted later has a new one.
        (await keys.DeleteKeyAsync(handle, 0, CancellationToken.None)).State.ShouldBe(KeyDestructionState.Completed);
        _ = await keys.RotateKeyAsync(handle, EncryptionAlgorithm.Aes256Gcm,
            "crypto-shred", null, CancellationToken.None);
        var newer = (await keys.GetKeyAsync(handle, CancellationToken.None))!.Generation!.ToString();
        older.ShouldNotBe(newer);
        var verification = true;
        var admin = A.Fake<IKeyManagementAdmin>(options => options.Wrapping(keys)
            .Implements<IKeyManagementProvider>().Implements<IKeyDestructionStatusProvider>());
        A.CallTo(() => ((IKeyManagementProvider)admin).GetKeyAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken ct) => keys.GetKeyAsync(key, ct));
        A.CallTo(() => ((IKeyDestructionStatusProvider)admin).IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(verification));
        A.CallTo(() => ((IServiceProvider)admin).GetService(typeof(IKeyDestructionStatusProvider)))
            .Returns((IKeyDestructionStatusProvider)admin);
        var calls = 0;
        string? committed = null;
        string? pending = null;
        var harness = new ErasureLifecycleHarness(admin, keys, decorateStore: store =>
        {
            var wrapped = A.Fake<IErasureStore>(options => options.Wrapping(store));
            A.CallTo(() => wrapped.StageKeyDestructionAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily(async (Guid request, string key, string generation, CancellationToken ct) =>
                {
                    await store.StageKeyDestructionAsync(request, key, older, ct);
                    await store.StageKeyDestructionAsync(request, key, generation, ct);
                });
            A.CallTo(() => wrapped.RecordKeyDestroyedAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily(async (Guid request, string key, string generation, CancellationToken ct) =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        throw new IOException("Interruption after destruction, before recording either generation.");
                    }
                    if (calls == 3)
                    {
                        pending = generation;
                        throw new IOException("Interruption after recording only one generation.");
                    }
                    await store.RecordKeyDestroyedAsync(request, key, generation, ct);
                    committed ??= generation;
                });
            return wrapped;
        });
        var (requestId, _, first) = await harness.SubmitAndExecuteAsync(subject);
        first.Success.ShouldBeFalse();
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldBeNull();
        var staged = await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None);
        staged.Count.ShouldBe(2);
        staged.ShouldContain(older);
        staged.ShouldContain(newer);
        (await harness.Store.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled,
            "Explicit retry after destruction", CancellationToken.None)).ShouldBeTrue();
        (await harness.Service.ExecuteAsync(requestId, CancellationToken.None)).Success.ShouldBeFalse();
        committed.ShouldNotBeNullOrWhiteSpace();
        pending.ShouldNotBeNullOrWhiteSpace();
        committed.ShouldNotBe(pending);
        var ledger = (IKeyDestructionLedger)harness.Store;
        (await ledger.IsGenerationDestroyedAsync(handle, committed, CancellationToken.None)).ShouldBeTrue();
        (await ledger.IsGenerationDestroyedAsync(handle, pending, CancellationToken.None)).ShouldBeFalse();
        (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
            .ShouldBe(new[] { pending });
        (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);

        verification = confirmed;
        (await harness.Store.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled,
            "Explicit retry after partial ledger write", CancellationToken.None)).ShouldBeTrue();
        var final = await harness.Service.ExecuteAsync(requestId, CancellationToken.None);
        final.Success.ShouldBe(confirmed, final.ErrorMessage);
        (await ledger.IsGenerationDestroyedAsync(handle, committed, CancellationToken.None)).ShouldBeTrue();
        (await ledger.IsGenerationDestroyedAsync(handle, pending, CancellationToken.None)).ShouldBe(confirmed);
        var remaining = await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None);
        if (confirmed)
        {
            remaining.ShouldBeEmpty();
            (await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
        }
        else
        {
            remaining.ShouldBe(new[] { pending });
            (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecoverAnExplicitlyRequeuedDestructionOnlyAfterPositiveProviderVerification(bool confirmed)
    {
        using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
        const string subject = "recover-destroyed-subject";
        var handle = SubjectKeyHandle.ForSubject(new TenantId(TenantDefaults.DefaultTenantId),
            subject, TestDataSubjectHasher.Instance).Value;
        _ = await keys.RotateKeyAsync(handle, EncryptionAlgorithm.Aes256Gcm,
            "crypto-shred", null, CancellationToken.None);
        var metadata = await keys.GetKeyAsync(handle, CancellationToken.None);
        var generation = metadata?.Generation?.ToString() ?? throw new InvalidOperationException("Real key has no generation.");
        var firstRecord = true;
        var admin = A.Fake<IKeyManagementAdmin>(options => options.Wrapping(keys)
            .Implements<IKeyManagementProvider>().Implements<IKeyDestructionStatusProvider>());
        A.CallTo(() => ((IKeyManagementProvider)admin).GetKeyAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken ct) => keys.GetKeyAsync(key, ct));
        A.CallTo(() => ((IKeyDestructionStatusProvider)admin).IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(confirmed));
        A.CallTo(() => ((IServiceProvider)admin).GetService(typeof(IKeyDestructionStatusProvider)))
            .Returns((IKeyDestructionStatusProvider)admin);
        var harness = new ErasureLifecycleHarness(admin, keys, decorateStore: store =>
        {
            var wrapped = A.Fake<IErasureStore>(options => options.Wrapping(store));
            A.CallTo(() => wrapped.RecordKeyDestroyedAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily((Guid request, string key, string keyGeneration, CancellationToken ct) =>
                {
                    if (firstRecord)
                    {
                        firstRecord = false;
                        return Task.FromException(new IOException("Injected interruption after physical key destruction."));
                    }
                    return store.RecordKeyDestroyedAsync(request, key, keyGeneration, ct);
                });
            return wrapped;
        });
        var (requestId, _, first) = await harness.SubmitAndExecuteAsync(subject);
        first.Success.ShouldBeFalse();
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldBeNull();
        (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
            .ShouldContain(generation);
        (await ((IKeyDestructionLedger)harness.Store).IsGenerationDestroyedAsync(handle, generation, CancellationToken.None))
            .ShouldBeFalse();

        // Execute requires a scheduled request. Model the scheduler/operator retry decision here.
        // This verifies ledger repair, without claiming automatic abandoned-request recovery.
        (await harness.Store.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled,
            "Explicit retry after interruption", CancellationToken.None)).ShouldBeTrue();
        var retried = await harness.Service.ExecuteAsync(requestId, CancellationToken.None);

        A.CallTo(() => ((IKeyDestructionStatusProvider)admin).IsKeyDestroyedAsync(handle, A<CancellationToken>._))
            .MustHaveHappened();
        retried.Success.ShouldBe(confirmed, retried.ErrorMessage);
        (await ((IKeyDestructionLedger)harness.Store).IsGenerationDestroyedAsync(handle, generation, CancellationToken.None))
            .ShouldBe(confirmed, "absence alone must not repair the ledger");
        if (confirmed)
        {
            (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None)).ShouldBeEmpty();
            (await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
        }
        else
        {
            (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
                .ShouldContain(generation);
            (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecoverRemainingRealGenerationAfterPartialImmediateDrainOnlyWithPositiveVerification(bool confirmed)
    {
        using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
        const string subject = "real-partial-immediate-remint-recovery";
        var handle = SubjectKeyHandle.ForSubject(new TenantId(TenantDefaults.DefaultTenantId),
            subject, TestDataSubjectHasher.Instance).Value;
        var firstRecord = true;
        var phase = 1;
        var partialInjected = false;
        var committed = new HashSet<string>(StringComparer.Ordinal);
        string? pending = null;
        var verification = true;
        var deleteOutcomes = new List<KeyDestructionState>();
        var admin = A.Fake<IKeyManagementAdmin>(options => options.Wrapping(keys)
            .Implements<IKeyManagementProvider>().Implements<IKeyDestructionStatusProvider>());
        A.CallTo(() => ((IKeyManagementProvider)admin).GetKeyAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken ct) => keys.GetKeyAsync(key, ct));
        A.CallTo(() => admin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(async (string key, int retention, CancellationToken ct) =>
            {
                var result = await keys.DeleteKeyAsync(key, retention, ct);
                deleteOutcomes.Add(result.State);
                return result;
            });
        A.CallTo(() => ((IKeyDestructionStatusProvider)admin).IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(verification));
        A.CallTo(() => ((IServiceProvider)admin).GetService(typeof(IKeyDestructionStatusProvider)))
            .Returns((IKeyDestructionStatusProvider)admin);
        var harness = new ErasureLifecycleHarness(admin, keys, decorateStore: store =>
        {
            var wrapped = A.Fake<IErasureStore>(options => options.Wrapping(store));
            A.CallTo(() => wrapped.RecordKeyDestroyedAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily(async (Guid request, string key, string generation, CancellationToken ct) =>
                {
                    if (firstRecord)
                    {
                        firstRecord = false;
                        throw new IOException("First real destruction completed; ledger insertion interrupted.");
                    }
                    // Fail the second DISTINCT pair in this drain, irrespective of enumeration order.
                    if (phase == 2 && !partialInjected && committed.Count == 1 && !committed.Contains(generation))
                    {
                        pending = generation;
                        partialInjected = true;
                        throw new IOException("Second distinct generation interrupted after one durable pair.");
                    }
                    await store.RecordKeyDestroyedAsync(request, key, generation, ct);
                    committed.Add(generation);
                });
            return wrapped;
        });
        var services = new ServiceCollection();
        _ = services.AddDefaultTenantContext();
        using var container = services.BuildServiceProvider();
        var manager = new SubjectKeyManager(keys, TestDataSubjectHasher.Instance, TestRetentions.None);
        using var aes = new AesGcmEncryptionProvider(keys, NullLogger<AesGcmEncryptionProvider>.Instance);
        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.GetPrimary()).Returns(aes);
        A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(aes);
        var ledger = (IKeyDestructionLedger)harness.Store;
        var encryptor = new FieldEncryptor(manager, registry, ledger, container.GetRequiredService<ITenantContext>());
        var plaintext = "original personal data"u8.ToArray();
        var oldEnvelope = await encryptor.EncryptAsync(subject, RetentionScope.NotInAnAggregate, plaintext, CancellationToken.None);
        var g1 = oldEnvelope.KeyGeneration ?? throw new InvalidOperationException("Missing original generation.");
        (await encryptor.DecryptAsync(oldEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None)).ShouldBe(plaintext);

        var (requestId, _, first) = await harness.SubmitAndExecuteAsync(subject);
        first.Success.ShouldBeFalse();
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldBeNull();
        (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);
        (await ledger.IsGenerationDestroyedAsync(handle, g1, CancellationToken.None)).ShouldBeFalse();
        (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
            .ShouldBe(new[] { g1 });

        // Ordinary field encryption remints the destroyed handle, without staging synthetic history.
        var newEnvelope = await encryptor.EncryptAsync(subject, RetentionScope.NotInAnAggregate,
            "later personal data"u8.ToArray(), CancellationToken.None);
        var g2 = newEnvelope.KeyGeneration ?? throw new InvalidOperationException("Missing reminted generation.");
        g2.ShouldNotBe(g1);
        newEnvelope.KeyId.ShouldBe(handle);
        (await encryptor.DecryptAsync(newEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None))
            .ShouldBe("later personal data"u8.ToArray());
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldNotBeNull();
        (await harness.Store.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled,
            "Operator retry of the same stopped request after ordinary remint", CancellationToken.None)).ShouldBeTrue();
        phase = 2;
        var retried = await harness.Service.ExecuteAsync(requestId, CancellationToken.None);

        deleteOutcomes.ShouldBe(new[] { KeyDestructionState.Completed, KeyDestructionState.Completed });
        (await keys.GetKeyAsync(handle, CancellationToken.None)).ShouldBeNull();
        retried.Success.ShouldBeFalse();
        (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);
        partialInjected.ShouldBeTrue();
        committed.Count.ShouldBe(1);
        pending.ShouldNotBeNullOrWhiteSpace();
        committed.ShouldNotContain(pending!);
        new[] { g1, g2 }.ShouldContain(pending!);
        foreach (var generation in new[] { g1, g2 })
        {
            (await ledger.IsGenerationDestroyedAsync(handle, generation, CancellationToken.None))
                .ShouldBe(committed.Contains(generation));
        }
        (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
            .ShouldBe(new[] { pending! });

        verification = confirmed;
        phase = 3;
        (await harness.Store.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled,
            "Operator retry after partial immediate destruction ledger drain", CancellationToken.None)).ShouldBeTrue();
        var final = await harness.Service.ExecuteAsync(requestId, CancellationToken.None);
        deleteOutcomes.ShouldBe(new[] { KeyDestructionState.Completed, KeyDestructionState.Completed, KeyDestructionState.NotFound });
        A.CallTo(() => ((IKeyDestructionStatusProvider)admin).IsKeyDestroyedAsync(handle, A<CancellationToken>._))
            .MustHaveHappened();
        final.Success.ShouldBe(confirmed, final.ErrorMessage);
        (await ledger.IsGenerationDestroyedAsync(handle, pending!, CancellationToken.None)).ShouldBe(confirmed);
        if (confirmed)
        {
            (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None)).ShouldBeEmpty();
            (await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
            (await encryptor.DecryptAsync(oldEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None)).ShouldBeNull();
            (await encryptor.DecryptAsync(newEnvelope, TenantDefaults.DefaultTenantId, CancellationToken.None)).ShouldBeNull();
        }
        else
        {
            (await harness.Store.GetStagedKeyGenerationsAsync(requestId, handle, CancellationToken.None))
                .ShouldBe(new[] { pending! });
            (await harness.StatusOfAsync(requestId)).ShouldNotBe(ErasureRequestStatus.Completed);
        }
        foreach (var generation in committed)
        {
            (await ledger.IsGenerationDestroyedAsync(handle, generation, CancellationToken.None)).ShouldBeTrue();
        }
    }

}
