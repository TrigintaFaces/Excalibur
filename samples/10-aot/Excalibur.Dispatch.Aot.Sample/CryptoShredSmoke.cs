// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>Exercises explicit-owner field encryption using real AES-GCM in a native executable.</summary>
internal static class CryptoShredSmoke
{
    public static async Task RunAsync()
    {
        Require(!RuntimeFeature.IsDynamicCodeSupported, "Run the published NativeAOT executable.");
        await RunPairAsync("tenant:a%one", "tenant:b%two");
        await RunPairAsync(TenantDefaults.DefaultTenantId, KeyedTenantPartition.Untenanted.TenantId);
        Console.WriteLine("Native crypto-shredding: tenant isolation, explicit ownership and lawful tombstone verified.");
    }

    private static async Task RunPairAsync(string tenantA, string tenantB)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(TenantContextHolder.AmbientContext);
        services.Configure<TenantContextOptions>(options => options.RequireTenant = true);
        services.AddDataSubjectHashing();
        services.Configure<DataSubjectHashingOptions>(options =>
            options.Pepper = "native-demo-pepper-not-a-secret-0123456789abcdef");
        services.AddEncryption(builder => builder.UseInMemoryKeyManagement("demo").SetAsPrimary("demo"));
        services.AddSingleton<IKeyManagementAdmin>(sp =>
            (IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());
        services.AddCryptoShredding();
        services.AddInMemoryErasureStore();
        await using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IEncryptionProvider>().ToList();
        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(CancellationToken.None);
        }

        using var scope = provider.CreateScope();
        var fields = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
        var keys = scope.ServiceProvider.GetRequiredService<IKeyManagementProvider>();
        var admin = scope.ServiceProvider.GetRequiredService<IKeyManagementAdmin>();
        Require(ReferenceEquals(keys, admin), "Deletion must use the registered key authority.");
        var plaintextA = "tenant A personal data"u8.ToArray();
        var plaintextB = "tenant B personal data"u8.ToArray();
        EncryptedData envelopeA;
        EncryptedData envelopeB;
        using (TenantContextHolder.BeginScope(tenantA))
        {
            envelopeA = await fields.EncryptAsync("shared-subject", RetentionScope.NotInAnAggregate,
                plaintextA, CancellationToken.None);
        }
        using (TenantContextHolder.BeginScope(tenantB))
        {
            envelopeB = await fields.EncryptAsync("shared-subject", RetentionScope.NotInAnAggregate,
                plaintextB, CancellationToken.None);
        }

        Require(envelopeA.KeyId != envelopeB.KeyId, "Tenant handles must differ.");
        Require(!string.IsNullOrWhiteSpace(envelopeA.KeyGeneration) && !string.IsNullOrWhiteSpace(envelopeB.KeyGeneration),
            "Both envelopes must name an actual generation.");
        Require(envelopeA.KeyGeneration != envelopeB.KeyGeneration, "Independent keys must have distinct generations.");
        using (TenantContextHolder.BeginScope(null))
        {
            Require(TenantContextHolder.Current is null, "Reads must not depend on an ambient tenant.");
            Require((await fields.DecryptAsync(envelopeA, tenantA, CancellationToken.None))!
                .AsSpan().SequenceEqual(plaintextA), "Tenant A must round-trip.");
            Require((await fields.DecryptAsync(envelopeB, tenantB, CancellationToken.None))!
                .AsSpan().SequenceEqual(plaintextB), "Tenant B must round-trip.");
            await RequireRejectedAsync(fields, envelopeA, tenantB);

            var actualKey = await keys.GetKeyAsync(envelopeA.KeyId, CancellationToken.None);
            Require(actualKey?.Generation?.ToString() == envelopeA.KeyGeneration,
                "The destruction authority must own the envelope's exact generation.");
            var destruction = await admin.DeleteKeyAsync(envelopeA.KeyId, 0, CancellationToken.None);
            Require(destruction.State == KeyDestructionState.Completed, "Destruction must be irreversible.");
            Require(await keys.GetKeyAsync(envelopeA.KeyId, CancellationToken.None) is null,
                "Destroyed key material must no longer be available.");
            // This sample is the external destruction caller: assert completion only AFTER observing it.
            // Production ErasureService uses its staged durable lifecycle instead.
            await scope.ServiceProvider.GetRequiredService<IKeyDestructionLedger>()
                .RecordDestroyedGenerationAsync(envelopeA.KeyId, envelopeA.KeyGeneration!, CancellationToken.None);
            Require(await fields.DecryptAsync(envelopeA, tenantA, CancellationToken.None) is null,
                "The lawful owner must observe a tombstone.");
            await RequireRejectedAsync(fields, envelopeA, tenantB);
            Require((await fields.DecryptAsync(envelopeB, tenantB, CancellationToken.None))!
                .AsSpan().SequenceEqual(plaintextB), "Tenant A destruction must preserve tenant B.");
        }


    }

    private static async Task RequireRejectedAsync(IFieldEncryptor fields, EncryptedData envelope, string owner)
    {
        try
        {
            _ = await fields.DecryptAsync(envelope, owner, CancellationToken.None);
        }
        catch (CryptographicException)
        {
            return;
        }
        throw new InvalidOperationException("A cross-tenant read must fail before returning plaintext or a tombstone.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
