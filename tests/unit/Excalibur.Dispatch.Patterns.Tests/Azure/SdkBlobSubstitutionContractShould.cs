// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Models;

namespace Excalibur.Dispatch.Patterns.Tests.Azure;

/// <summary>
/// Certifies the exact SDK members substituted by the real claim-check adapter tests.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Patterns)]
[Trait("Pattern", "Regression")]
public sealed class SdkBlobSubstitutionContractShould
{
	[Fact]
	public void ExposeTheContainerOverloadsUsedByTheProvider()
	{
		AssertSubstitutable(typeof(BlobContainerClient), nameof(BlobContainerClient.CreateIfNotExistsAsync),
			typeof(PublicAccessType), typeof(IDictionary<string, string>),
			typeof(BlobContainerEncryptionScopeOptions), typeof(CancellationToken));
		AssertSubstitutable(typeof(BlobContainerClient), nameof(BlobContainerClient.GetBlobClient), typeof(string));
		AssertProxyConstructor(typeof(BlobContainerClient));
	}

	[Fact]
	public void ExposeTheBlobOverloadsUsedByTheProvider()
	{
		AssertSubstitutable(typeof(BlobClient), nameof(BlobClient.UploadAsync),
			typeof(BinaryData), typeof(BlobUploadOptions), typeof(CancellationToken));
		AssertSubstitutable(typeof(BlobClient), nameof(BlobClient.DownloadContentAsync), typeof(CancellationToken));
		AssertSubstitutable(typeof(BlobClient), nameof(BlobClient.DeleteIfExistsAsync),
			typeof(DeleteSnapshotsOption), typeof(BlobRequestConditions), typeof(CancellationToken));
		var getter = typeof(BlobClient).GetProperty(nameof(BlobClient.Uri))?.GetMethod;
		getter.ShouldNotBeNull();
		getter.IsPublic.ShouldBeTrue();
		getter.IsVirtual.ShouldBeTrue();
		getter.IsFinal.ShouldBeFalse();
		AssertProxyConstructor(typeof(BlobClient));
	}

	private static void AssertSubstitutable(Type clientType, string methodName, params Type[] parameterTypes)
	{
		var method = clientType.GetMethod(methodName, parameterTypes);
		method.ShouldNotBeNull();
		method.IsPublic.ShouldBeTrue();
		method.IsVirtual.ShouldBeTrue();
		method.IsFinal.ShouldBeFalse();
	}

	private static void AssertProxyConstructor(Type clientType)
	{
		clientType.IsSealed.ShouldBeFalse();
		var constructor = clientType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
			binder: null, Type.EmptyTypes, modifiers: null);
		constructor.ShouldNotBeNull();
		(constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly).ShouldBeTrue();
	}
}
