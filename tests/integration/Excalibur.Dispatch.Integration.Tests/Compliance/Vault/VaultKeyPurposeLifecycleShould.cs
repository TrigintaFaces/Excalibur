// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Excalibur.Compliance;
using Excalibur.Compliance.Vault;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Vault;

/// <summary>
/// Real-Vault locks for the full lifecycle of a key's purpose: set, preserved across a rotation that does
/// not mention it, cleared on request, and reported identically by both accessors.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never skipped.</b> The purpose lives in a KV sidecar on the real server; a fake KV client returns
/// whatever it was told, so it cannot show that a document survived a rotation or was really removed by a
/// clear. Docker being unavailable is a failure here, not a reason to pass.
/// </para>
/// <para>
/// <b>Pre-fix, two defects.</b> A purpose could be set and never cleared: rotation early-returned on a
/// null-or-empty purpose, so both "not supplied" and "supplied as empty" preserved the recorded value and
/// no other path removed the sidecar. And <c>GetKeyVersionAsync</c> did not overlay the sidecar, so a
/// version lookup described the key as having no purpose while <c>GetKeyAsync</c> described the same key as
/// having one.
/// </para>
/// <para>
/// <b>Liveness matters here.</b> A provider that cleared the sidecar on every rotation would satisfy the
/// clear arm, and one that never recorded a purpose at all would satisfy it too.
/// <see cref="PreserveARecordedPurposeWhenARotationDoesNotMentionIt"/> and the guard assertions inside each
/// arm are what make those cheats fail.
/// </para>
/// </remarks>
[Collection(VaultTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class VaultKeyPurposeLifecycleShould : IDisposable
{
	private const string SuspensionMount = "secret";

	private readonly VaultContainerFixture _fixture;
	private readonly IMemoryCache _cache;
	private readonly VaultOptions _options;

	public VaultKeyPurposeLifecycleShould(VaultContainerFixture fixture)
	{
		_fixture = fixture;
		_cache = new MemoryCache(new MemoryCacheOptions());
		_options = new VaultOptions
		{
			VaultUri = new Uri(_fixture.VaultAddress),
			Auth = { AuthMethod = VaultAuthMethod.Token, Token = _fixture.Token },
			Keys = new() { TransitMountPath = "transit", KeyNamePrefix = "excalibur-purpose-test-" },
			MetadataCacheDuration = TimeSpan.FromMinutes(5),
		};
	}

	public void Dispose() => _cache.Dispose();

	/// <summary>
	/// Liveness. A purpose supplied on creation is recorded and readable.
	/// </summary>
	[Fact]
	public async Task RecordAPurposeSuppliedOnCreation()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);

		(await ReadPurposeFromVaultAsync(keyId)).ShouldBe(
			"customer-pii-eu",
			"a supplied purpose must reach the sidecar, or every arm below is vacuous.");
	}

	/// <summary>
	/// Safety for the preserve case. A rotation that does not mention the purpose must not erase one
	/// recorded earlier -- and this is what makes the clear arm below a real distinction rather than a
	/// provider that simply always clears.
	/// </summary>
	[Fact]
	public async Task PreserveARecordedPurposeWhenARotationDoesNotMentionIt()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);
		(await ReadPurposeFromVaultAsync(keyId)).ShouldBe("customer-pii-eu");

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		(await ReadPurposeFromVaultAsync(keyId)).ShouldBe(
			"customer-pii-eu",
			"a null purpose means 'not supplied', which must leave the recorded value untouched.");
	}

	/// <summary>
	/// The reported defect. An explicitly empty purpose removes the sidecar, so a purpose is not write-once.
	/// </summary>
	[Fact]
	public async Task ClearARecordedPurposeWhenAnEmptyPurposeIsSupplied()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);

		// Guard: the sidecar must exist before the clear, or the assertion after it proves nothing.
		(await PurposeDocumentExistsAsync(keyId)).ShouldBeTrue(
			"the purpose sidecar must exist before the clear, or this lock is vacuous.");

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, string.Empty, null, CancellationToken.None);

		(await PurposeDocumentExistsAsync(keyId)).ShouldBeFalse(
			"an explicitly empty purpose must remove the sidecar, not preserve it.");

		// And the removal must be observable through the provider, from a cache that never saw the value.
		using var freshProvider = CreateProvider(new MemoryCache(new MemoryCacheOptions()));
		var metadata = await freshProvider.GetKeyAsync(keyId, CancellationToken.None);

		metadata.ShouldNotBeNull("clearing the purpose must not destroy the key.");
		metadata.Purpose.ShouldBeNull("a cleared purpose must read back as absent.");
	}

	/// <summary>
	/// The second reported defect. A version lookup must describe the key the same way the key lookup does.
	/// </summary>
	[Fact]
	public async Task ReportTheSamePurposeFromAVersionLookupAsFromAKeyLookup()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		var created = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);
		created.Success.ShouldBeTrue($"the key must be creatable: {created.ErrorMessage}");

		// A fresh instance, so both answers come from Vault rather than from a cache the write populated.
		using var freshProvider = CreateProvider(new MemoryCache(new MemoryCacheOptions()));

		var byKey = await freshProvider.GetKeyAsync(keyId, CancellationToken.None);
		byKey.ShouldNotBeNull();
		byKey.Purpose.ShouldBe("customer-pii-eu", "the key lookup must report the purpose, or this is vacuous.");

		var byVersion = await freshProvider.GetKeyVersionAsync(keyId, byKey.Version, CancellationToken.None);

		byVersion.ShouldNotBeNull("the version that GetKeyAsync just reported must be resolvable.");
		byVersion.Purpose.ShouldBe(
			byKey.Purpose,
			"a version lookup that omits the purpose describes the same key differently from GetKeyAsync.");
	}

	private void RequireVault() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Vault must be reachable: these locks bind the purpose contract and are never skipped.");

	private static string NewKeyId() => $"purpose-{Guid.NewGuid():N}";

	private VaultKeyProvider CreateProvider(IMemoryCache? cache = null) =>
		new(
			Microsoft.Extensions.Options.Options.Create(_options),
			cache ?? _cache,
			A.Fake<ILogger<VaultKeyProvider>>());

	private IVaultClient RawClient() =>
		new VaultClient(new VaultClientSettings(_fixture.VaultAddress, new TokenAuthMethodInfo(_fixture.Token)));

	// Reads the KV document directly rather than through the provider, so the assertions are about what is
	// actually stored in Vault and cannot be satisfied by provider-side caching.
	private async Task<bool> PurposeDocumentExistsAsync(string keyId) =>
		await ReadPurposeFromVaultAsync(keyId) is not null;

	private async Task<string?> ReadPurposeFromVaultAsync(string keyId)
	{
		try
		{
			var secret = await RawClient().V1.Secrets.KeyValue.V2
				.ReadSecretAsync($"{_options.Suspension.PurposePath}/{keyId}", mountPoint: SuspensionMount);

			return secret?.Data?.Data is { } data && data.TryGetValue("purpose", out var value)
				? value?.ToString()
				: null;
		}
		catch (VaultSharp.Core.VaultApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}
	}
}
