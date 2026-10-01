// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Reflection;

using Excalibur.Compliance;
using Excalibur.Compliance.Vault;

using FakeItEasy;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.KeyValue;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Excalibur.Compliance.Tests.Vault;

/// <summary>
/// Vault Transit has no per-generation identifier of its own — its version ordinals restart at 1 after a
/// delete-and-create — so the identity of the material at a handle is minted by the provider and kept in a
/// sidecar document. That sidecar is a SECOND SOURCE OF TRUTH, and it must die with the key.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT THIS LOCKS, and it was live.</b> The erasure path deleted the purpose and suspension
/// sidecars and not this one. A data subject's key handle is derived from the subject, so the next ordinary
/// write after an erasure provisions new material at the same handle — and the mint is write-if-absent, so a
/// surviving document hands that new material the DESTROYED generation's identifier. Every payload written
/// before the erasure then names the live generation and is reported not destroyed, which is the precise
/// confusion the identifier exists to remove. The fix looked correct with the leak still in it.
/// </para>
/// <para>
/// <b>Asserted as BEHAVIOUR, not as a call.</b> These arms provision, destroy and provision again through the
/// provider's own public surface and compare the identifiers it reports. A <c>MustHaveHappened</c> on the
/// delete would pass against a provider that deleted the wrong path, and would have to be rewritten the
/// moment the sidecar moved; what matters is that a re-provisioned handle cannot come back wearing the dead
/// generation's name.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TheVaultGenerationMarkerDiesWithTheKeyShould
{
	private const string KeyId = "orders";

	/// <summary>
	/// THE LOAD-BEARING ARM. A handle provisioned again after an erasure reports a DIFFERENT generation.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: leave the generation sidecar in place on erasure and the second
	/// provisioning reads it back, so both identifiers are equal and an erased subject's earlier payloads
	/// report as live.
	/// </remarks>
	[Fact]
	public async Task MintANewGeneration_WhenTheHandleIsProvisionedAgainAfterAnErasure()
	{
		var vault = new FakeVault();
		var provider = CreateProvider(vault);

		var first = await provider.CreateKeyIfAbsentAsync(
			KeyId, EncryptionAlgorithm.Aes256Gcm, null, CancellationToken.None);

		first.Generation.ShouldNotBeNull(
			"precondition: provisioning must record a generation, or this arm compares nothing");

		_ = await provider.DeleteKeyAsync(KeyId, 0, CancellationToken.None);

		var second = await provider.CreateKeyIfAbsentAsync(
			KeyId, EncryptionAlgorithm.Aes256Gcm, null, CancellationToken.None);

		second.Generation.ShouldNotBeNull();
		second.Generation.ShouldNotBe(
			first.Generation,
			"the material at this handle was destroyed and new material provisioned, so it must not carry the "
			+ "destroyed generation's identifier. Equal identifiers here mean every payload written before the "
			+ "erasure is reported NOT destroyed, and an erased subject's fields read as live");
	}

	/// <summary>
	/// The same property observed at the store: the erasure leaves no document behind that a later
	/// provisioning could read back.
	/// </summary>
	/// <remarks>
	/// The arm above would also pass if the document survived but the mint happened to overwrite it. This one
	/// pins the erasure itself, so the two together distinguish "erased" from "overwritten later".
	/// </remarks>
	[Fact]
	public async Task LeaveNoGenerationDocumentBehind_WhenTheKeyIsDestroyed()
	{
		var vault = new FakeVault();
		var provider = CreateProvider(vault);

		_ = await provider.CreateKeyIfAbsentAsync(
			KeyId, EncryptionAlgorithm.Aes256Gcm, null, CancellationToken.None);

		var generationPaths = vault.Paths.FindAll(static p => p.Contains("-generation/", StringComparison.Ordinal));
		generationPaths.Count.ShouldBe(
			1,
			"precondition: provisioning must have written exactly one generation document, or the assertion "
			+ "below holds vacuously. Paths present: " + string.Join(", ", vault.Paths));

		_ = await provider.DeleteKeyAsync(KeyId, 0, CancellationToken.None);

		vault.Paths.ShouldNotContain(
			generationPaths[0],
			"the generation document must be erased with the key. A surviving document is read back by the next "
			+ "provisioning at this handle, because the mint is write-if-absent");
	}

	/// <summary>
	/// LIVENESS. Without it, both arms above are satisfied by a provider that never records a generation at
	/// all, or deletes it on every call — either of which makes the handle unusable for crypto-shredded fields.
	/// </summary>
	[Fact]
	public async Task KeepTheGeneration_AcrossAProvisioningOfAHandleThatWasNotDestroyed()
	{
		var vault = new FakeVault();
		var provider = CreateProvider(vault);

		var first = await provider.CreateKeyIfAbsentAsync(
			KeyId, EncryptionAlgorithm.Aes256Gcm, null, CancellationToken.None);

		// The handle is already occupied, so this is a no-op provisioning — the identity of the material must
		// not move under payloads already written against it.
		var again = await provider.CreateKeyIfAbsentAsync(
			KeyId, EncryptionAlgorithm.Aes256Gcm, null, CancellationToken.None);

		again.Generation.ShouldBe(
			first.Generation,
			"a provisioning that found the handle occupied changed no material, so the generation must be the "
			+ "same. Minting a new one here makes every payload already written unreadable");
	}

	private static VaultKeyProvider CreateProvider(FakeVault vault)
	{
		var provider = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(new VaultOptions
			{
				VaultUri = new Uri("http://127.0.0.1:8200"),
				Keys = new() { KeyNamePrefix = "dispatch-" },
				Auth =
				{
					AuthMethod = VaultAuthMethod.Token,
					Token = "unit-token"
				}
			}),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		var client = new VaultClient(
			new VaultClientSettings("http://127.0.0.1:8200", new TokenAuthMethodInfo("unit-token")));

		SetPrivateField(client, "<V1>k__BackingField", vault.V1);
		SetPrivateField(provider, "_vaultClient", client);

		return provider;
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}

	/// <summary>
	/// A Vault whose KV store and Transit key actually hold state, so a document written by one call is read
	/// by the next and a deleted one is gone. A stateless fake cannot express the defect these arms lock,
	/// because the defect IS a document outliving its key.
	/// </summary>
	/// <summary>
	/// A Vault whose KV store and Transit key actually hold state, so a document written by one call is read
	/// by the next and a deleted one is gone. A stateless fake cannot express the defect these arms lock,
	/// because the defect IS a document outliving its key.
	/// </summary>
	/// <remarks>
	/// The KV calls are matched by METHOD NAME rather than by a typed expression. VaultSharp's read and write
	/// members are generic with optional parameters, so a typed configuration pins one instantiation and goes
	/// silently unmatched when the provider calls another — which would leave the store empty and every arm
	/// here vacuous.
	/// </remarks>
	private sealed class FakeVault
	{
		private readonly Dictionary<string, IDictionary<string, object>> _kv = new(StringComparer.Ordinal);

		private bool _transitKeyExists;

		public IVaultClientV1 V1 { get; }

		/// <summary>Gets the paths the KV store currently holds.</summary>
		public List<string> Paths => [.. _kv.Keys];

		public FakeVault()
		{
			var kv2 = A.Fake<IKeyValueSecretsEngineV2>();
			var kv = A.Fake<IKeyValueSecretsEngine>();
			var transit = A.Fake<ITransitSecretsEngine>();
			var secrets = A.Fake<ISecretsEngine>();
			var v1 = A.Fake<IVaultClientV1>();

			_ = A.CallTo(() => v1.Secrets).Returns(secrets);
			_ = A.CallTo(() => secrets.KeyValue).Returns(kv);
			_ = A.CallTo(() => kv.V2).Returns(kv2);
			_ = A.CallTo(() => secrets.Transit).Returns(transit);

			// ---- KV v2: a real little store ----
			A.CallTo(kv2)
				.Where(static call => call.Method.Name == "WriteSecretAsync")
				.Invokes(call => _kv[(string)call.Arguments[0]!] = ToDictionary(call.Arguments[1]));

			A.CallTo(kv2)
				.Where(static call => call.Method.Name == "DeleteMetadataAsync")
				.Invokes(call => _kv.Remove((string)call.Arguments[0]!));

			A.CallTo(kv2)
				.Where(static call => call.Method.Name == "ReadSecretAsync")
				.WithReturnType<Task<Secret<SecretData>>>()
				.ReturnsLazily(call => ReadOrThrow((string)call.Arguments[0]!));

			// ---- Transit: one key that may or may not be there ----
			_ = A.CallTo(() => transit.CreateEncryptionKeyAsync(
					A<string>._, A<CreateKeyRequestOptions>._, A<string>._))
				.ReturnsLazily(() =>
				{
					_transitKeyExists = true;
					return Task.CompletedTask;
				});

			_ = A.CallTo(() => transit.ReadEncryptionKeyAsync(A<string>._, A<string>._, A<string?>._))
				.ReturnsLazily(() => _transitKeyExists
					? Task.FromResult(new Secret<EncryptionKeyInfo> { Data = KeyInfo() })
					: throw new VaultApiException(HttpStatusCode.NotFound, "no such key"));

			_ = A.CallTo(() => transit.UpdateEncryptionKeyConfigAsync(
					A<string>._, A<UpdateKeyRequestOptions>._, A<string>._))
				.Returns(Task.CompletedTask);

			_ = A.CallTo(() => transit.DeleteEncryptionKeyAsync(A<string>._, A<string>._))
				.ReturnsLazily(() =>
				{
					_transitKeyExists = false;
					return Task.CompletedTask;
				});

			V1 = v1;
		}

		private static EncryptionKeyInfo KeyInfo() => new()
		{
			LatestVersion = 1,
			Type = TransitKeyType.aes256_gcm96,
			DeletionAllowed = false,
			Keys = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["1"] = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["creation_time"] = DateTimeOffset.UtcNow,
				},
			},
		};

		private static IDictionary<string, object> ToDictionary(object? written) => written switch
		{
			IDictionary<string, object> typed => new Dictionary<string, object>(typed, StringComparer.Ordinal),
			null => new Dictionary<string, object>(StringComparer.Ordinal),
			_ => throw new InvalidOperationException(
				$"The provider wrote a {written.GetType().Name} the fake store cannot hold. Teach the fake "
				+ "rather than loosening it, or these arms stop measuring the real write."),
		};

		private Task<Secret<SecretData>> ReadOrThrow(string path) =>
			_kv.TryGetValue(path, out var data)
				? Task.FromResult(new Secret<SecretData> { Data = new SecretData { Data = data } })
				: throw new VaultApiException(HttpStatusCode.NotFound, "no such secret");
	}
}
