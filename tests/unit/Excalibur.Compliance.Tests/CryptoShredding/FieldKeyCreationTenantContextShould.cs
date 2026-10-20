// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance.Tests.CryptoShredding;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class FieldKeyCreationTenantContextShould
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "tenant-a")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    public async Task RefuseUnresolvedOrInconsistentContextBeforeKeyCreation(bool hasTenant, string? tenantId)
    {
        var tenant = A.Fake<ITenantContext>();
        A.CallTo(() => tenant.HasTenant).Returns(hasTenant);
        A.CallTo(() => tenant.TenantId).Returns(tenantId);
        var manager = A.Fake<ISubjectKeyManager>();
        var provider = A.Fake<IEncryptionProvider>();
        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.GetPrimary()).Returns(provider);
        A.CallTo(() => manager.GetOrCreateKeyAsync(A<TenantId>._, A<string>._, A<RetentionScope>._, A<CancellationToken>._))
            .Returns(new ValueTask<SubjectKey>(new SubjectKey("test-key", null)));
        A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Envelope()));
        var encryptor = new FieldEncryptor(manager, registry, A.Fake<IKeyDestructionLedger>(), tenant);

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            encryptor.EncryptAsync("subject", RetentionScope.NotInAnAggregate, new byte[] { 1 }, CancellationToken.None).AsTask());

        A.CallTo(() => manager.GetOrCreateKeyAsync(A<TenantId>._, A<string>._, A<RetentionScope>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => registry.GetPrimary()).MustNotHaveHappened();
        A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task AcceptTheRealFrameworkSingleTenantDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddDefaultTenantContext();
        using var container = services.BuildServiceProvider();
        var tenant = container.GetRequiredService<ITenantContext>();
        tenant.HasTenant.ShouldBeTrue();
        var manager = A.Fake<ISubjectKeyManager>();
        var provider = A.Fake<IEncryptionProvider>();
        var registry = A.Fake<IEncryptionProviderRegistry>();
        A.CallTo(() => registry.GetPrimary()).Returns(provider);
        A.CallTo(() => manager.GetOrCreateKeyAsync(A<TenantId>._, A<string>._, A<RetentionScope>._, A<CancellationToken>._))
            .Returns(new ValueTask<SubjectKey>(new SubjectKey("test-key", null)));
        A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Envelope()));
        var encryptor = new FieldEncryptor(manager, registry, A.Fake<IKeyDestructionLedger>(), tenant);

        _ = await encryptor.EncryptAsync("subject", RetentionScope.NotInAnAggregate, new byte[] { 1 }, CancellationToken.None);

        A.CallTo(() => manager.GetOrCreateKeyAsync(
                new TenantId(TenantDefaults.DefaultTenantId), "subject", RetentionScope.NotInAnAggregate, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => provider.EncryptAsync(A<byte[]>._,
                A<EncryptionContext>.That.Matches(context => context.TenantId == TenantDefaults.DefaultTenantId), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreserveCapturedTenantAcrossKeyCreationAwait()
    {
        var tenantId = "tenant-a";
        var tenant = A.Fake<ITenantContext>();
        A.CallTo(() => tenant.HasTenant).Returns(true);
        A.CallTo(() => tenant.TenantId).ReturnsLazily(() => tenantId);
        var manager = A.Fake<ISubjectKeyManager>();
        var provider = A.Fake<IEncryptionProvider>();
        var registry = A.Fake<IEncryptionProviderRegistry>();
        var key = new TaskCompletionSource<SubjectKey>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => registry.GetPrimary()).Returns(provider);
        A.CallTo(() => manager.GetOrCreateKeyAsync(A<TenantId>._, A<string>._, A<RetentionScope>._, A<CancellationToken>._))
            .Returns(new ValueTask<SubjectKey>(key.Task));
        A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Envelope()));
        var encryptor = new FieldEncryptor(manager, registry, A.Fake<IKeyDestructionLedger>(), tenant);

        var pending = encryptor.EncryptAsync("subject", RetentionScope.NotInAnAggregate, new byte[] { 1 }, CancellationToken.None).AsTask();
        pending.IsCompleted.ShouldBeFalse("the key manager must still hold this invocation at its await");
        A.CallTo(() => manager.GetOrCreateKeyAsync(new TenantId("tenant-a"), "subject", RetentionScope.NotInAnAggregate, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        tenantId = "tenant-b";
        key.SetResult(new SubjectKey("test-key", null));
        _ = await pending;

        A.CallTo(() => provider.EncryptAsync(A<byte[]>._,
                A<EncryptionContext>.That.Matches(context => context.TenantId == "tenant-a"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => tenant.TenantId).MustHaveHappenedOnceExactly();
    }

    private static EncryptedData Envelope() => new()
    {
        Ciphertext = [1], Iv = new byte[12], KeyId = "test-key", KeyVersion = 1,
        Algorithm = EncryptionAlgorithm.Aes256Gcm,
    };
}
