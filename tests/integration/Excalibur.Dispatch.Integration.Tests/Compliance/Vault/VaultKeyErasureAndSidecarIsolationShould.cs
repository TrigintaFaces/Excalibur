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
/// Real-Vault locks for two defects in the key provider's KV sidecars: erasure that left descriptive
/// metadata behind, and a purpose sidecar whose path could be addressed by another key's identifier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never skipped.</b> These bind a crypto-shredding guarantee and a key-status guarantee, and both were
/// invisible to mocked tests: a fake KV client returns whatever it was told, so it cannot show that a real
/// document survived a delete or that two paths collided on the real server. Docker being unavailable is a
/// failure here, not a reason to pass.
/// </para>
/// <para>
/// <b>Erasure (pre-fix):</b> <c>DeleteKeyAsync</c> destroyed the Transit key material and invalidated the
/// cache, but never removed the KV documents describing the key -- the purpose sidecar (whose intended
/// values describe what the key protected, such as <c>customer-pii-eu</c>) or the suspension marker (which
/// carries a caller-supplied free-text reason). Delete is the crypto-shredding path, so an orphan surviving
/// it is an erasure that did not erase, on a provider chosen for compliance.
/// </para>
/// <para>
/// <b>Collision (pre-fix):</b> suspension was stored at <c>{Path}/{keyId}</c> and purpose at
/// <c>{Path}/purpose/{keyId}</c> -- the purpose namespace nested inside the suspension namespace. Key status
/// treats "a document exists at the suspension path" as "suspended", so recording a purpose for key
/// <c>foo</c> wrote a document at exactly the suspension path of a key named <c>purpose/foo</c>, making that
/// key resolve as Suspended. The fix gives purpose its own disjoint root, so no key identifier composes into
/// the other namespace.
/// </para>
/// <para>
/// <b>Liveness matters unusually much here.</b> A <c>DeleteKeyAsync</c> that erased every key's metadata
/// would satisfy every erasure arm below, and a key-status path that never reported Suspended would satisfy
/// the collision arm. <see cref="LeaveAnUnrelatedKeysMetadataIntactWhenOneKeyIsErased"/> and
/// <see cref="StillReportAGenuinelySuspendedKeyAsSuspended"/> are what make those cheats fail.
/// </para>
/// </remarks>
[Collection(VaultTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class VaultKeyErasureAndSidecarIsolationShould : IDisposable
{
	private const string SuspensionMount = "secret";

	private readonly VaultContainerFixture _fixture;
	private readonly IMemoryCache _cache;
	private readonly VaultOptions _options;

	public VaultKeyErasureAndSidecarIsolationShould(VaultContainerFixture fixture)
	{
		_fixture = fixture;
		_cache = new MemoryCache(new MemoryCacheOptions());
		_options = new VaultOptions
		{
			VaultUri = new Uri(_fixture.VaultAddress),
			Auth = { AuthMethod = VaultAuthMethod.Token, Token = _fixture.Token },
			Keys = new() { TransitMountPath = "transit", KeyNamePrefix = "excalibur-erasure-test-" },
			MetadataCacheDuration = TimeSpan.FromMinutes(5),
		};
	}

	public void Dispose() => _cache.Dispose();

	/// <summary>
	/// Safety, and the reported defect. The purpose sidecar must not survive the erasure of the key it
	/// describes.
	/// </summary>
	[Fact]
	public async Task EraseThePurposeSidecarWhenTheKeyIsDeleted()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);

		// Guard: the sidecar must actually exist, or the assertion after the delete proves nothing.
		(await PurposeDocumentExistsAsync(keyId))
			.ShouldBeTrue("the purpose sidecar must exist before deletion, or this lock is vacuous.");

		_ = await provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);

		(await PurposeDocumentExistsAsync(keyId)).ShouldBeFalse(
			"the purpose sidecar describes what the erased key protected; surviving erasure makes "
			+ "crypto-shredding incomplete.");
	}

	/// <summary>
	/// Safety. The suspension marker carries a caller-supplied reason string and must not survive erasure
	/// either -- the same defect, on the sidecar nobody named.
	/// </summary>
	[Fact]
	public async Task EraseTheSuspensionMarkerWhenTheKeyIsDeleted()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		(await provider.SuspendKeyAsync(keyId, "erasure requested by data subject", CancellationToken.None))
			.ShouldBeTrue();

		(await SuspensionDocumentExistsAsync(keyId))
			.ShouldBeTrue("the suspension marker must exist before deletion, or this lock is vacuous.");

		_ = await provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);

		(await SuspensionDocumentExistsAsync(keyId)).ShouldBeFalse(
			"the suspension marker records a free-text reason about the erased key and must not outlive it.");
	}

	/// <summary>
	/// Liveness. Erasure must be confined to the key being erased. Without this, a delete that wiped every
	/// key's metadata would pass both safety arms above.
	/// </summary>
	[Fact]
	public async Task LeaveAnUnrelatedKeysMetadataIntactWhenOneKeyIsErased()
	{
		RequireVault();

		var erased = NewKeyId();
		var survivor = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			erased, EncryptionAlgorithm.Aes256Gcm, "doomed", null, CancellationToken.None);
		_ = await provider.RotateKeyAsync(
			survivor, EncryptionAlgorithm.Aes256Gcm, "still-in-use", null, CancellationToken.None);

		_ = await provider.DeleteKeyAsync(erased, 30, CancellationToken.None);

		(await PurposeDocumentExistsAsync(survivor)).ShouldBeTrue(
			"erasing one key must not erase another key's metadata.");

		// And the survivor must still resolve as a usable key, not merely have a document lying around.
		using var freshProvider = CreateProvider(new MemoryCache(new MemoryCacheOptions()));
		var metadata = await freshProvider.GetKeyAsync(survivor, CancellationToken.None);

		metadata.ShouldNotBeNull("the surviving key must still resolve.");
		metadata.Purpose.ShouldBe("still-in-use", "the surviving key's purpose must still be readable.");
		metadata.Status.ShouldBe(KeyStatus.Active, "the surviving key must not have been collaterally affected.");
	}

	/// <summary>
	/// Safety. Recording a purpose must never change a key's status. The purpose sidecar is written to KV,
	/// and key status is resolved by "does a document exist at the suspension path", so a purpose document
	/// reachable through the suspension prefix would suspend a key merely by describing it.
	/// </summary>
	/// <remarks>
	/// This is the reachable half of the sidecar-collision defect. The originally reported form -- a key
	/// named <c>purpose/foo</c> resolving as suspended once a purpose was recorded for <c>foo</c> -- is NOT
	/// reachable through this provider: Vault Transit rejects a key name containing a path separator with
	/// <c>404 unsupported path</c>, so such a key cannot be created, read, or suspended. Measured against
	/// Vault 1.15: creating <c>transit/keys/x-foo</c> succeeds and <c>transit/keys/x-purpose/foo</c> is
	/// refused. The disjoint-roots invariant that removes the collision by construction is asserted
	/// structurally in the unit suite, where it can fail; here we bind the behaviour a consumer can observe.
	/// </remarks>
	[Fact]
	public async Task NotChangeAKeysStatusWhenItsPurposeIsRecorded()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		var created = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);

		created.Success.ShouldBeTrue($"the key must be creatable: {created.ErrorMessage}");

		// A fresh instance, so the answer comes from Vault rather than from this provider's cache.
		using var freshProvider = CreateProvider(new MemoryCache(new MemoryCacheOptions()));
		var metadata = await freshProvider.GetKeyAsync(keyId, CancellationToken.None);

		metadata.ShouldNotBeNull();
		metadata.Purpose.ShouldBe("customer-pii-eu", "the purpose must round-trip through its sidecar.");
		metadata.Status.ShouldBe(
			KeyStatus.Active,
			"recording a purpose must not suspend the key it describes; if it does, the purpose sidecar is "
			+ "being written where the suspension predicate reads.");
	}

	/// <summary>
	/// Liveness for the collision arm. Suspension must still work -- otherwise the arm above would pass
	/// simply because nothing is ever reported as suspended.
	/// </summary>
	[Fact]
	public async Task StillReportAGenuinelySuspendedKeyAsSuspended()
	{
		RequireVault();

		var keyId = NewKeyId();
		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, "customer-pii-eu", null, CancellationToken.None);
		(await provider.SuspendKeyAsync(keyId, "security incident", CancellationToken.None)).ShouldBeTrue();

		using var freshProvider = CreateProvider(new MemoryCache(new MemoryCacheOptions()));
		var metadata = await freshProvider.GetKeyAsync(keyId, CancellationToken.None);

		metadata.ShouldNotBeNull();
		metadata.Status.ShouldBe(
			KeyStatus.Suspended,
			"a key that really was suspended must still read Suspended, or the isolation arm is vacuous.");
	}

	private void RequireVault() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Vault must be reachable: these locks bind a crypto-shredding guarantee and are never skipped.");

	private static string NewKeyId() => $"erase-{Guid.NewGuid():N}";

	private VaultKeyProvider CreateProvider(IMemoryCache? cache = null) =>
		new(
			Microsoft.Extensions.Options.Options.Create(_options),
			cache ?? _cache,
			A.Fake<ILogger<VaultKeyProvider>>());

	private IVaultClient RawClient() =>
		new VaultClient(new VaultClientSettings(_fixture.VaultAddress, new TokenAuthMethodInfo(_fixture.Token)));

	private Task<bool> PurposeDocumentExistsAsync(string keyId) =>
		DocumentExistsAsync($"{_options.Suspension.PurposePath}/{keyId}");

	private Task<bool> SuspensionDocumentExistsAsync(string keyId) =>
		DocumentExistsAsync($"{_options.Suspension.Path}/{keyId}");

	// Reads the KV document directly rather than through the provider, so the assertion is about what is
	// actually stored in Vault and cannot be satisfied by provider-side caching or filtering.
	private async Task<bool> DocumentExistsAsync(string path)
	{
		try
		{
			var secret = await RawClient().V1.Secrets.KeyValue.V2
				.ReadSecretAsync(path, mountPoint: SuspensionMount);

			return secret?.Data?.Data is { Count: > 0 };
		}
		catch (VaultSharp.Core.VaultApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
	}
}
