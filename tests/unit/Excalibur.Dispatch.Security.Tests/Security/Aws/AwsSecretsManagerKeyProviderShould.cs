// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Excalibur.Security;
using Excalibur.Security.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Security.Tests.Aws;

/// <summary>
///     Engage-tests for <see cref="AwsSecretsManagerKeyProvider"/> (h8s2vf, author≠impl). The provider
///     stores key material as the secret's <em>binary</em> payload (never a plaintext string), caches
///     retrieved keys with a bounded TTL, and an unknown key fails closed by throwing a
///     <see cref="SigningException"/> — the retrieval path never mints a substitute. These locks drive a
///     stateful faked <see cref="IAmazonSecretsManager"/> (never real AWS / committed credentials).
///     Each is non-vacuous: it RED-fails if the contract it pins were violated (plaintext storage,
///     a minted-substitute on miss, an unbounded cache miss, or a non-random rotation).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class AwsSecretsManagerKeyProviderShould
{
	private const string KeyId = "signing-key-h8s2vf";

	/// <summary>
	///     Round-trip: <c>StoreKeyAsync</c> persists material through <c>SecretBinary</c> (NOT a plaintext
	///     <c>SecretString</c>), and a fresh provider reading the same backing store reloads the exact bytes.
	/// </summary>
	[Fact]
	public async Task RoundTrip_StoreThenGet_PersistsKeyMaterialAsSecretBinary()
	{
		// Arrange — a stateful fake backing store shared by two provider instances (no caching shortcut).
		var backing = new FakeSecretsManager();
		var options = NewOptions(enableCache: false);
		var writer = NewProvider(backing.Client, options);
		using var writerScope = writer;

		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// Act — store via one provider, read back via a second (forces the real SDK Get path / SecretBinary).
		await writer.StoreKeyAsync(KeyId, keyMaterial, CancellationToken.None);

		using var reader = NewProvider(backing.Client, options);
		var loaded = await reader.GetKeyAsync(KeyId, CancellationToken.None);

		// Assert — exact bytes round-trip, and they were written to SecretBinary, never SecretString.
		loaded.ShouldBe(keyMaterial);
		backing.LastStoredAsBinary.ShouldBeTrue("key material must be stored as SecretBinary, never a plaintext SecretString");
		backing.LastStoredString.ShouldBeNull();
	}

	/// <summary>
	///     Fail-closed: an unknown key throws <see cref="SigningException"/> and the retrieval path
	///     never returns minted/substitute material.
	/// </summary>
	[Fact]
	public async Task FailClosed_GetUnknownKey_ThrowsSigningExceptionAndNeverMints()
	{
		// Arrange — empty backing store: every GetSecretValue raises ResourceNotFound.
		var backing = new FakeSecretsManager();
		using var provider = NewProvider(backing.Client, NewOptions(enableCache: false));

		// Act & Assert — fails closed; no key bytes are ever produced.
		_ = await Should.ThrowAsync<SigningException>(
			async () => await provider.GetKeyAsync("does-not-exist", CancellationToken.None));
	}

	/// <summary>
	///     Cache-hit: a second read within the bounded TTL is served from the local cache and does NOT
	///     issue a second Secrets Manager API call.
	/// </summary>
	[Fact]
	public async Task CacheHit_SecondGetWithinTtl_DoesNotCallSdkAgain()
	{
		// Arrange — seed the backing store directly (so the first Get genuinely hits the SDK).
		var backing = new FakeSecretsManager();
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		backing.Seed(KeyId, keyMaterial);

		var timeProvider = new FakeTimeProvider();
		using var provider = NewProvider(backing.Client, NewOptions(enableCache: true), timeProvider);

		// Act — two reads, no time advance (well within the TTL).
		var first = await provider.GetKeyAsync(KeyId, CancellationToken.None);
		var second = await provider.GetKeyAsync(KeyId, CancellationToken.None);

		// Assert — both return the material, but the SDK was hit exactly once (second served from cache).
		first.ShouldBe(keyMaterial);
		second.ShouldBe(keyMaterial);
		backing.GetCallCount.ShouldBe(1, "the second read within TTL must be served from cache, not a second SDK call");
	}

	/// <summary>
	///     Rotation: <c>RotateKeyAsync</c> mints fresh random material of the configured size and stores it
	///     (as SecretBinary), so a subsequent read returns the rotated bytes.
	/// </summary>
	[Fact]
	public async Task Rotate_MintsRandomKeyOfConfiguredSize_AndStoresIt()
	{
		// Arrange
		var backing = new FakeSecretsManager();
		var options = NewOptions(enableCache: false);
		options.RotatedKeySizeBytes = 64;
		using var provider = NewProvider(backing.Client, options);

		// Act
		var rotated = await provider.RotateKeyAsync(KeyId, CancellationToken.None);

		// Assert — minted at the configured size, non-trivial (not all-zero), stored as binary, and re-readable.
		rotated.Length.ShouldBe(64);
		rotated.ShouldContain(b => b != 0, "rotated key material must be random, not all-zero");
		backing.LastStoredAsBinary.ShouldBeTrue("rotated material must be stored as SecretBinary");

		using var reader = NewProvider(backing.Client, options);
		var reloaded = await reader.GetKeyAsync(KeyId, CancellationToken.None);
		reloaded.ShouldBe(rotated);
	}

	private static AwsSecretsManagerKeyProviderOptions NewOptions(bool enableCache) => new()
	{
		EnableCache = enableCache,
		CacheTtlSeconds = 300,
		CacheMaxEntries = 1024,
		RotatedKeySizeBytes = 64,
	};

	private static AwsSecretsManagerKeyProvider NewProvider(
		IAmazonSecretsManager client,
		AwsSecretsManagerKeyProviderOptions options,
		TimeProvider? timeProvider = null)
		=> new(
			NullLogger<AwsSecretsManagerKeyProvider>.Instance,
			client,
			options,
			timeProvider ?? new FakeTimeProvider());

	/// <summary>
	///     A stateful, in-memory wrapper over a faked <see cref="IAmazonSecretsManager"/>: stores the binary
	///     payload by secret id, raises <see cref="ResourceNotFoundException"/> on an unknown read, and records
	///     whether the last write used SecretBinary (vs SecretString) plus the SDK Get call count. Implicitly
	///     convertible to the faked client so it can be passed straight to the provider ctor.
	/// </summary>
	private sealed class FakeSecretsManager
	{
		private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);
		private readonly IAmazonSecretsManager _client = A.Fake<IAmazonSecretsManager>();

		public FakeSecretsManager()
		{
			A.CallTo(() => _client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetSecretValueRequest request, CancellationToken _) =>
				{
					GetCallCount++;
					return _store.TryGetValue(request.SecretId, out var material)
						? new GetSecretValueResponse { SecretBinary = new MemoryStream(material, writable: false) }
						: throw new ResourceNotFoundException($"secret '{request.SecretId}' not found");
				});

			A.CallTo(() => _client.PutSecretValueAsync(A<PutSecretValueRequest>._, A<CancellationToken>._))
				.ReturnsLazily((PutSecretValueRequest request, CancellationToken _) =>
				{
					RecordWrite(request.SecretId, request.SecretBinary, request.SecretString);
					return new PutSecretValueResponse();
				});

			A.CallTo(() => _client.CreateSecretAsync(A<CreateSecretRequest>._, A<CancellationToken>._))
				.ReturnsLazily((CreateSecretRequest request, CancellationToken _) =>
				{
					RecordWrite(request.Name, request.SecretBinary, request.SecretString);
					return new CreateSecretResponse();
				});
		}

		public bool LastStoredAsBinary { get; private set; }

		public string? LastStoredString { get; private set; }

		public int GetCallCount { get; private set; }

		public IAmazonSecretsManager Client => _client;

		public void Seed(string secretId, byte[] material) => _store[secretId] = material;

		private void RecordWrite(string secretId, Stream? binary, string? text)
		{
			LastStoredString = text;
			if (binary is not null)
			{
				LastStoredAsBinary = true;
				using var ms = new MemoryStream();
				binary.Position = 0;
				binary.CopyTo(ms);
				_store[secretId] = ms.ToArray();
			}
			else
			{
				LastStoredAsBinary = false;
				if (text is not null)
				{
					_store[secretId] = Convert.FromBase64String(text);
				}
			}
		}
	}
}
