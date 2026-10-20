// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Azure;
using Azure.Security.KeyVault.Keys;

namespace Excalibur.Compliance.Tests.Azure;

/// <summary>Certifies the exact SDK seams used by the Key Vault adapter fixtures.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class SdkKeyVaultSubstitutionContractShould
{
    [Fact]
    public void PreserveProxyConstructorsAndTheExactVirtualMembersUsedByAdapterTests()
    {
        AssertProxyConstructor(typeof(KeyClient));
        AssertProxyConstructor(typeof(DeleteKeyOperation));

        AssertMethod(typeof(KeyClient), nameof(KeyClient.GetKeyAsync), typeof(Task<Response<KeyVaultKey>>),
            typeof(string), typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.GetPropertiesOfKeyVersionsAsync), typeof(AsyncPageable<KeyProperties>),
            typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.GetPropertiesOfKeysAsync), typeof(AsyncPageable<KeyProperties>),
            typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.RotateKeyAsync), typeof(Task<Response<KeyVaultKey>>),
            typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.CreateRsaKeyAsync), typeof(Task<Response<KeyVaultKey>>),
            typeof(CreateRsaKeyOptions), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.UpdateKeyPropertiesAsync), typeof(Task<Response<KeyVaultKey>>),
            typeof(KeyProperties), typeof(IEnumerable<KeyOperation>), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.StartDeleteKeyAsync), typeof(Task<DeleteKeyOperation>),
            typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.GetDeletedKeyAsync), typeof(Task<Response<DeletedKey>>),
            typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(KeyClient), nameof(KeyClient.PurgeDeletedKeyAsync), typeof(Task<Response>),
            typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(DeleteKeyOperation), nameof(DeleteKeyOperation.WaitForCompletionAsync),
            typeof(ValueTask<Response<DeletedKey>>), typeof(CancellationToken));
        AssertMethod(typeof(DeleteKeyOperation), "get_HasCompleted", typeof(bool));
    }

    private static void AssertProxyConstructor(Type type)
    {
        type.IsSealed.ShouldBeFalse();
        var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        constructor.ShouldNotBeNull();
        (constructor.IsFamily || constructor.IsFamilyOrAssembly).ShouldBeTrue();
    }

    private static void AssertMethod(Type type, string name, Type returnType, params Type[] parameters)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
            binder: null, types: parameters, modifiers: null);
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(returnType);
        method.IsVirtual.ShouldBeTrue();
        method.IsFinal.ShouldBeFalse();
    }
}
