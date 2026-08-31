// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure;
using Azure.Security.KeyVault.Secrets;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Security.KeyManagement;

/// <summary>
/// Binds the audit contract for key rotation across every key provider: SecretAccessed carries every operation that
/// touches secret material, so one rotation produces exactly one Rotate record and no plain Write record, on every
/// provider alike. Parameterised over the implementations so a provider added later fails on arrival rather than
/// shipping a silently non-comparable audit stream.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class KeyRotationAuditConformanceShould
{
	private const string KeyName = "conformance-rotation-key";

	public static TheoryData<string> Providers => ["local", "azure"];

	// SAFETY: exactly one Rotate record per rotation, and NOT a Write. RED at HEAD on both providers before the fix
	// -- Local raised nothing at all for a rotation, and Azure reported it as a plain Write from the inner store.
	[Theory]
	[MemberData(nameof(Providers))]
	public async Task RecordARotationAsExactlyOneRotateAndNeverAsAWrite(string providerName)
	{
		var provider = await CreateProviderWithExistingKeyAsync(providerName);
		try
		{
			var accessed = new List<SecretAccessedEventArgs>();
			provider.SecretAccessed += (_, e) => accessed.Add(e);

			var result = await provider.RotateEncryptionKeyAsync(KeyName, CancellationToken.None);
			result.Success.ShouldBeTrue(result.ErrorMessage ?? "the rotation itself must succeed or the audit assertions prove nothing");

			accessed.Count(e => e.Operation == SecretOperation.Rotate)
				.ShouldBe(1, $"{providerName} must record a rotation on the audit stream");
			accessed.Count(e => e.Operation == SecretOperation.Write)
				.ShouldBe(0, $"{providerName} must not report a rotation as a plain write");
		}
		finally
		{
			(provider as IDisposable)?.Dispose();
		}
	}

	// LIVENESS: the lifecycle channel survives. KeyRotated carries the new version for cache invalidation and
	// re-wrapping and is a different audience from the audit stream; without this arm the safety arm above would be
	// satisfied by a provider that stopped notifying rotation altogether.
	[Theory]
	[MemberData(nameof(Providers))]
	public async Task StillRaiseTheLifecycleNotificationCarryingTheNewVersion(string providerName)
	{
		var provider = await CreateProviderWithExistingKeyAsync(providerName);
		try
		{
			var rotated = new List<KeyRotatedEventArgs>();
			provider.KeyRotated += (_, e) => rotated.Add(e);

			_ = await provider.RotateEncryptionKeyAsync(KeyName, CancellationToken.None);

			rotated.Count.ShouldBe(1, $"{providerName} must still raise the lifecycle notification");
			rotated[0].NewKeyVersion.ShouldNotBeNullOrWhiteSpace();
		}
		finally
		{
			(provider as IDisposable)?.Dispose();
		}
	}

	private static async Task<IElasticsearchKeyProvider> CreateProviderWithExistingKeyAsync(string providerName)
	{
		if (providerName == "local")
		{
			var local = new LocalKeyProvider();
			_ = await local.GenerateEncryptionKeyAsync(
				KeyName, EncryptionKeyType.Aes, 256, metadata: null, CancellationToken.None);
			return local;
		}

		// The vault is substituted rather than reached: rotation reads the current secret's tags to recover the key
		// type and size, then stores the new material.
		var secret = new KeyVaultSecret(KeyName, "c2VjcmV0LW1hdGVyaWFs");
		secret.Properties.Tags["KeyType"] = nameof(EncryptionKeyType.Aes);
		secret.Properties.Tags["KeySize"] = "256";
		var response = Response.FromValue(secret, A.Fake<Response>());

		// Matched by method name rather than by signature: the provider calls these with named arguments, which binds
		// a different overload than an explicit-arity rule would configure, and an unmatched call returns a dummy
		// response whose Value is null.
		var client = A.Fake<SecretClient>();
		A.CallTo(client).Where(call => call.Method.Name == "GetSecretAsync" || call.Method.Name == "SetSecretAsync")
			.WithReturnType<Task<Response<KeyVaultSecret>>>()
			.Returns(response);

		return new AzureKeyVaultProvider(
			client,
			Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions { VaultUri = "https://unused.vault.azure.net/" }),
			NullLogger<AzureKeyVaultProvider>.Instance);
	}
}
