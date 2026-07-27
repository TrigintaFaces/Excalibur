// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Azure;
using Azure.Security.KeyVault.Secrets;

using Excalibur.Security;
using Excalibur.Security.Azure;
using Excalibur.Security.Azure.Internal;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Security.Tests.Azure;

/// <summary>
///     Engage-lock (jyk9ta, author≠impl) for <see cref="AzureKeyVaultKeyProvider"/> — the Azure sibling of
///     the AWS Secrets Manager provider. Key material is stored as a base64-encoded Key Vault secret, retrieved
///     keys are cached with a bounded TTL, and an unknown key fails closed by throwing a
///     <see cref="SigningException"/> — the retrieval path never mints a substitute. Drives a stateful faked
///     <see cref="ISecretClient"/> (never a real vault / credentials). Each lock is non-vacuous: it RED-fails
///     if the pinned contract were violated (string-coerced storage, a minted substitute on 404, an unbounded
///     cache miss, or a non-random rotation).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class AzureKeyVaultKeyProviderShould
{
	private const string KeyId = "signing-key-jyk9ta";

	[Fact]
	public async Task RoundTrip_StoreThenGet_PersistsKeyMaterialBase64()
	{
		// Arrange — stateful fake vault shared by writer + a fresh reader (no caching shortcut).
		var vault = new FakeSecretVault();
		var options = NewOptions(enableCache: false);
		var writer = NewProvider(vault.Client, options);

		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act — store via one provider, read back via a second (forces the SDK Get path / base64 decode).
		await writer.StoreKeyAsync(KeyId, keyMaterial, CancellationToken.None);
		var loaded = await NewProvider(vault.Client, options).GetKeyAsync(KeyId, CancellationToken.None);

		// Assert — exact bytes round-trip, and the secret value was stored base64-encoded (a string secret),
		// not the raw/garbled bytes.
		loaded.ShouldBe(keyMaterial);
		vault.LastStoredValue.ShouldNotBeNull();
		Convert.FromBase64String(vault.LastStoredValue!).ShouldBe(keyMaterial);
	}

	[Fact]
	public async Task FailClosed_GetUnknownKey_ThrowsSigningExceptionAndNeverMints()
	{
		// Arrange — empty vault: GetSecret raises a 404.
		var vault = new FakeSecretVault();
		var provider = NewProvider(vault.Client, NewOptions(enableCache: false));

		// Act & Assert — fails closed; no key bytes are ever produced.
		_ = await Should.ThrowAsync<SigningException>(
			async () => await provider.GetKeyAsync("does-not-exist", CancellationToken.None));
	}

	[Fact]
	public async Task CacheHit_SecondGetWithinTtl_DoesNotCallSdkAgain()
	{
		// Arrange — seed the vault directly so the first Get genuinely hits the SDK.
		var vault = new FakeSecretVault();
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		vault.Seed(KeyId, Convert.ToBase64String(keyMaterial));

		var provider = NewProvider(vault.Client, NewOptions(enableCache: true), new FakeTimeProvider());

		// Act — two reads, no time advance (well within TTL).
		var first = await provider.GetKeyAsync(KeyId, CancellationToken.None);
		var second = await provider.GetKeyAsync(KeyId, CancellationToken.None);

		// Assert — both return the material, but the SDK was hit exactly once (second served from cache).
		first.ShouldBe(keyMaterial);
		second.ShouldBe(keyMaterial);
		vault.GetCallCount.ShouldBe(1, "the second read within TTL must be served from cache, not a second SDK call");
	}

	[Fact]
	public async Task Rotate_MintsRandomKeyOfConfiguredSize_AndStoresIt()
	{
		// Arrange
		var vault = new FakeSecretVault();
		var options = NewOptions(enableCache: false);
		options.RotatedKeySizeBytes = 64;
		var provider = NewProvider(vault.Client, options);

		// Act
		var rotated = await provider.RotateKeyAsync(KeyId, CancellationToken.None);

		// Assert — minted at configured size, non-trivial (not all-zero), stored base64, and re-readable.
		rotated.Length.ShouldBe(64);
		rotated.ShouldContain(b => b != 0, "rotated key material must be random, not all-zero");
		vault.LastStoredValue.ShouldNotBeNull();
		Convert.FromBase64String(vault.LastStoredValue!).ShouldBe(rotated);

		var reloaded = await NewProvider(vault.Client, options).GetKeyAsync(KeyId, CancellationToken.None);
		reloaded.ShouldBe(rotated);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not-a-uri")]
	[InlineData("http://insecure.vault.azure.net/")]
	public void Validator_RejectsMissingOrNonHttpsVaultUri(string? vaultUri)
	{
		var validator = new AzureKeyVaultKeyProviderOptionsValidator();
		var options = new AzureKeyVaultKeyProviderOptions { VaultUri = vaultUri };

		var result = validator.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validator_AcceptsAbsoluteHttpsVaultUri()
	{
		var validator = new AzureKeyVaultKeyProviderOptionsValidator();
		var options = new AzureKeyVaultKeyProviderOptions { VaultUri = "https://my-vault.vault.azure.net/" };

		validator.Validate(name: null, options).Succeeded.ShouldBeTrue();
	}

	private static AzureKeyVaultKeyProviderOptions NewOptions(bool enableCache) => new()
	{
		VaultUri = "https://my-vault.vault.azure.net/",
		EnableCache = enableCache,
		CacheTtlSeconds = 300,
		CacheMaxEntries = 1024,
		RotatedKeySizeBytes = 64,
	};

	private static AzureKeyVaultKeyProvider NewProvider(
		ISecretClient client,
		AzureKeyVaultKeyProviderOptions options,
		TimeProvider? timeProvider = null)
		=> new(
			NullLogger<AzureKeyVaultKeyProvider>.Instance,
			client,
			options,
			timeProvider ?? new FakeTimeProvider());

	/// <summary>
	///     A stateful, in-memory wrapper over a faked <see cref="ISecretClient"/>: stores the secret value by
	///     name, raises a 404 <see cref="RequestFailedException"/> on an unknown read, and records the last
	///     stored value plus the SDK Get call count.
	/// </summary>
	private sealed class FakeSecretVault
	{
		private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.Ordinal);
		private readonly ISecretClient _client = A.Fake<ISecretClient>();

		public FakeSecretVault()
		{
			A.CallTo(() => _client.GetSecretAsync(A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string name, CancellationToken _) =>
				{
					GetCallCount++;
					if (!_store.TryGetValue(name, out var value))
					{
						throw new RequestFailedException(404, $"secret '{name}' not found");
					}

					var secret = new KeyVaultSecret(name, value);
					return Task.FromResult(Response.FromValue(secret, A.Fake<Response>()));
				});

			A.CallTo(() => _client.SetSecretAsync(A<KeyVaultSecret>._, A<CancellationToken>._))
				.ReturnsLazily((KeyVaultSecret secret, CancellationToken _) =>
				{
					LastStoredValue = secret.Value;
					_store[secret.Name] = secret.Value;
					return Task.FromResult(Response.FromValue(secret, A.Fake<Response>()));
				});
		}

		public ISecretClient Client => _client;

		public string? LastStoredValue { get; private set; }

		public int GetCallCount { get; private set; }

		public void Seed(string name, string value) => _store[name] = value;
	}
}
