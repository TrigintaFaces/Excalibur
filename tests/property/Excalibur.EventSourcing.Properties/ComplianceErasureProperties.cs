// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using Shouldly;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Properties;

/// <summary>
/// Seeded sampling of public field-crypto owner isolation and externally recorded destruction.
/// This does not model provider crashes, ErasureService orchestration or durable database behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Property")]
public sealed class ComplianceErasureProperties
{
    [Fact]
    public async Task PreserveOwnerIsolationAcrossGeneratedWriteEraseAndRemintSchedules()
    {
        const int rootSeed = 20261019;
        const int cases = 300;
        for (var index = 0; index < cases; index++)
        {
            var seed = rootSeed + index;
            try
            {
                await RunCaseAsync(seed);
            }
            catch (Exception exception)
            {
                Assert.Fail($"Owner/erasure property failed at reproducible seed {seed}: {exception}");
            }
        }
    }

    private static async Task RunCaseAsync(int seed)
    {
        var random = new Random(seed);
        string[] population = [TenantDefaults.DefaultTenantId, KeyedTenantPartition.Untenanted.TenantId,
            "tenant:a%one", "tenant:a%ONE", "tenant:é%two"];
        var first = random.Next(population.Length);
        var second = (first + 1 + random.Next(population.Length - 1)) % population.Length;
        var ownerA = population[first];
        var ownerB = population[second];
        var plaintextA = new byte[random.Next(1, 257)];
        var plaintextB = new byte[random.Next(1, 257)];
        random.NextBytes(plaintextA);
        random.NextBytes(plaintextB);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(TenantContextHolder.AmbientContext);
        services.Configure<TenantContextOptions>(options => options.RequireTenant = true);
        services.AddDataSubjectHashing();
        services.Configure<DataSubjectHashingOptions>(options =>
            options.Pepper = "property-test-pepper-not-a-secret-0123456789abcdef");
        services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
        services.AddSingleton<IKeyManagementAdmin>(sp =>
            (IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());
        services.AddCryptoShredding();
        services.AddInMemoryErasureStore();
        await using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IEncryptionProvider>().ToList();
        var started = new List<IHostedService>();
        try
        {
            foreach (var service in provider.GetServices<IHostedService>())
            {
                await service.StartAsync(CancellationToken.None);
                started.Add(service);
            }
            using var scope = provider.CreateScope();
            var fields = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
            var keys = scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>();
            var admin = scope.ServiceProvider.GetRequiredService<IKeyManagementAdmin>();
            var ledger = scope.ServiceProvider.GetRequiredService<IKeyDestructionLedger>();
            async Task<EncryptedData> WriteAsync(string owner, byte[] plaintext)
            {
                using var tenant = TenantContextHolder.BeginScope(owner);
                return await fields.EncryptAsync("shared-subject:%42", RetentionScope.NotInAnAggregate,
                    plaintext, CancellationToken.None);
            }
            EncryptedData a;
            EncryptedData b;
            if (random.Next(2) == 0)
            {
                a = await WriteAsync(ownerA, plaintextA);
                b = await WriteAsync(ownerB, plaintextB);
            }
            else
            {
                b = await WriteAsync(ownerB, plaintextB);
                a = await WriteAsync(ownerA, plaintextA);
            }
            a.KeyId.ShouldNotBe(b.KeyId);
            a.KeyGeneration.ShouldNotBeNullOrWhiteSpace();
            b.KeyGeneration.ShouldNotBeNullOrWhiteSpace();
            a.KeyGeneration.ShouldNotBe(b.KeyGeneration);
            a.TenantId.ShouldBe(ownerA);
            b.TenantId.ShouldBe(ownerB);
            using var absentAmbient = TenantContextHolder.BeginScope(null);
            (await fields.DecryptAsync(a, ownerA, CancellationToken.None)).ShouldBe(plaintextA);
            (await fields.DecryptAsync(b, ownerB, CancellationToken.None)).ShouldBe(plaintextB);
            _ = await Assert.ThrowsAsync<CryptographicException>(async () =>
                _ = await fields.DecryptAsync(a, ownerB, CancellationToken.None));
            var eraseA = random.Next(2) == 0;
            var erased = eraseA ? a : b;
            var erasedOwner = eraseA ? ownerA : ownerB;
            var live = eraseA ? b : a;
            var liveOwner = eraseA ? ownerB : ownerA;
            var livePlaintext = eraseA ? plaintextB : plaintextA;
            var key = await keys.GetKeyAsync(erased.KeyId, CancellationToken.None);
            key.ShouldNotBeNull();
            key.Generation.ShouldNotBeNull();
            key.Generation.Value.ToString().ShouldBe(erased.KeyGeneration);
            (await admin.DeleteKeyAsync(erased.KeyId, 0, CancellationToken.None)).State
                .ShouldBe(KeyDestructionState.Completed);
            await ledger.RecordDestroyedGenerationAsync(erased.KeyId, erased.KeyGeneration!, CancellationToken.None);
            (await fields.DecryptAsync(erased, erasedOwner, CancellationToken.None)).ShouldBeNull();
            (await fields.DecryptAsync(live, liveOwner, CancellationToken.None)).ShouldBe(livePlaintext);
            _ = await Assert.ThrowsAsync<CryptographicException>(async () =>
                _ = await fields.DecryptAsync(erased, liveOwner, CancellationToken.None));
            var reminted = await WriteAsync(erasedOwner, plaintextA);
            reminted.KeyId.ShouldBe(erased.KeyId);
            reminted.KeyGeneration.ShouldNotBe(erased.KeyGeneration);
            (await fields.DecryptAsync(reminted, erasedOwner, CancellationToken.None)).ShouldBe(plaintextA);
            (await fields.DecryptAsync(erased, erasedOwner, CancellationToken.None)).ShouldBeNull();
        }
        finally
        {
            using var stopDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            for (var index = started.Count - 1; index >= 0; index--)
            {
                await started[index].StopAsync(stopDeadline.Token);
            }
        }
    }
}
