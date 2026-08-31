// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Vault;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Vault;

/// <summary>
/// Proves a key service that never exports key material can serve as the production key source,
/// against a real Vault rather than a stand-in.
/// </summary>
/// <remarks>
/// <para>
/// This is the defect these arms exist for: the encryption provider used to require raw key material,
/// which a KMS or HSM will not give up — that refusal is the reason to use one. A mocked client cannot
/// reproduce it, because a mock returns whatever it was told to return. Only a real Transit engine
/// demonstrates both halves: that it will not hand over the key, and that wrapping works anyway.
/// </para>
/// <para>
/// Deliberately NOT skip-gated. A real-infrastructure arm that passes by being skipped is how the gap
/// it was written to catch reaches production. If Docker is unavailable these fail rather than report
/// a success nobody earned.
/// </para>
/// </remarks>
[Collection(VaultTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class VaultEnvelopeKeyWrappingShould : IAsyncLifetime
{
	private const string KeyId = "envelope-round-trip";

	private readonly VaultContainerFixture _fixture;
	private readonly List<IMemoryCache> _caches = [];

	public VaultEnvelopeKeyWrappingShould(VaultContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"this arm must exercise a real Vault; a skipped run is not a pass.");

		await _fixture.CreateKeyAsync(BuildKeyName(KeyId), TestContext.Current.CancellationToken)
			.ConfigureAwait(true);
	}

	public ValueTask DisposeAsync()
	{
		foreach (var cache in _caches)
		{
			cache.Dispose();
		}

		return default;
	}

	[Fact]
	public void Advertise_key_wrapping_through_capability_discovery()
	{
		// Consumers query for the capability rather than casting, so a provider that supplies it must
		// answer for it here. If this returns null the encryption provider never takes the envelope
		// path, and the whole production key source is silently unreachable.
		using var provider = CreateProvider();

		// GetService is a default interface member, so it is reached through the interface --
		// which is also how a consumer discovers the capability.
		((IKeyManagementProvider)provider).GetService(typeof(IKeyWrappingProvider))
			.ShouldBeOfType<VaultKeyProvider>();
	}

	[Fact]
	public async Task Wrap_a_data_key_without_disclosing_it()
	{
		using var provider = CreateProvider();
		var dataKey = Convert.FromHexString(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");

		var wrapped = await provider
			.WrapDataKeyAsync(KeyId, 1, dataKey, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		wrapped.CiphertextBlob.ShouldNotBeEmpty();

		// The wrapped form must not contain the data key. This is the assertion a mock cannot make
		// meaningfully, and the property the whole scheme rests on.
		wrapped.CiphertextBlob.ShouldNotBe(dataKey);

		// Transit ciphertext names the key version that produced it, which is why a rotated key can
		// still read what an earlier version wrote.
		Encoding.UTF8.GetString(wrapped.CiphertextBlob).ShouldStartWith("vault:v");
	}

	[Fact]
	public async Task Unwrap_a_data_key_on_a_completely_fresh_provider_instance()
	{
		// The restart case. An in-process key holder loses its key here and reports nothing; a real
		// Vault does not, which is the entire difference between the two.
		var dataKey = Convert.FromHexString(
			"ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100");

		WrappedDataKey wrapped;
		using (var writer = CreateProvider())
		{
			wrapped = await writer.WrapDataKeyAsync(KeyId, 1, dataKey, TestContext.Current.CancellationToken)
				.ConfigureAwait(true);
		}

		using var reader = CreateProvider();
		var recovered = await reader
			.UnwrapDataKeyAsync(KeyId, 1, wrapped, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		recovered.ShouldBe(dataKey);
	}

	[Fact]
	public async Task Round_trip_a_payload_through_the_encryption_provider_across_instances()
	{
		// End to end on the real production seam: AesGcmEncryptionProvider driven by a Vault that never
		// exports its key, encrypting on one instance and decrypting on another. Before this change the
		// provider refused to construct at all against this key service.
		var plaintext = "escrowed material that must survive a restart"u8.ToArray();
		var context = new EncryptionContext { KeyId = KeyId, TenantId = "tenant-a" };

		EncryptedData encrypted;
		using (var writingKeys = CreateProvider())
		using (var writer = new AesGcmEncryptionProvider(writingKeys, NullLogger<AesGcmEncryptionProvider>.Instance))
		{
			encrypted = await writer.EncryptAsync(plaintext, context, TestContext.Current.CancellationToken)
				.ConfigureAwait(true);
		}

		encrypted.WrappedKey.ShouldNotBeNull();

		using var readingKeys = CreateProvider();
		using var reader = new AesGcmEncryptionProvider(readingKeys, NullLogger<AesGcmEncryptionProvider>.Instance);

		var decrypted = await reader.DecryptAsync(encrypted, context, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		decrypted.ShouldBe(plaintext);
	}

	private static string BuildKeyName(string keyId) => $"excalibur-envelope-{keyId}";

	// A fresh cache per instance on purpose: two providers sharing a cache would let one answer from
	// the other's memory, which is exactly the cross-instance behaviour under test.
	private VaultKeyProvider CreateProvider()
	{
		var cache = new MemoryCache(new MemoryCacheOptions());
		_caches.Add(cache);

		var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = new Uri(_fixture.VaultAddress),
			Auth = { AuthMethod = VaultAuthMethod.Token, Token = _fixture.Token },
			Keys = new() { TransitMountPath = "transit", KeyNamePrefix = "excalibur-envelope-" },
			MetadataCacheDuration = TimeSpan.FromMinutes(5)
		});

		return new VaultKeyProvider(options, cache, NullLogger<VaultKeyProvider>.Instance);
	}
}
