// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Vault;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Vault;

/// <summary>
/// Real-Vault locks binding <c>deletion_allowed</c> to what it actually is: a permission to delete, never a
/// record that a destruction was scheduled.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms detect.</b> The provider mapped <c>deletion_allowed</c> to
/// <see cref="KeyStatus.PendingDestruction"/>. Every status other than <see cref="KeyStatus.Active"/> throws on
/// the encrypt path, so the flag being set meant a subject's personal data could never be written again. Two
/// ordinary situations reached it. A delete sets the flag and then deletes; if the delete half fails -- network,
/// permission, rate limit, cancellation -- the flag is left set forever, and a half-completed erasure is exactly
/// when that happens. And an operator who sets the flag on their own keys, a reasonable posture for a
/// crypto-shred deployment, wedged encryption for every subject at once.
/// </para>
/// <para>
/// <b>Never skipped, and it has to be real Vault.</b> The flag is server state, read back through
/// <c>ReadEncryptionKeyAsync</c>. A fake client returns whatever the test told it to, so it cannot show that the
/// provider read a genuinely-set flag and still reported the key usable. Docker being unavailable is a failure
/// here, not a reason to pass.
/// </para>
/// <para>
/// <b>Safety and liveness are both here on purpose.</b> "Ignore <c>deletion_allowed</c>" is satisfiable by a
/// mapper that returns <see cref="KeyStatus.Active"/> unconditionally, which would be a worse defect than the
/// one being fixed -- it would report a rotated-out version as encryptable.
/// <see cref="StillFenceAVersionBelowTheEncryptionFloorWhenDeletionIsPermitted"/> is the arm that fails for such
/// a mapper, and <see cref="ReportAFreshKeyActiveWhenDeletionIsNotPermitted"/> is the twin that fails for a
/// mapper that reports nothing usable.
/// </para>
/// </remarks>
[Collection(VaultTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class VaultDeletionPermissionIsNotADestructionStateShould : IDisposable
{
	private const string KeyNamePrefix = "excalibur-delperm-";
	private const string TransitMount = "transit";

	private readonly VaultContainerFixture _fixture;
	private readonly List<IMemoryCache> _caches = [];

	public VaultDeletionPermissionIsNotADestructionStateShould(VaultContainerFixture fixture) => _fixture = fixture;

	public void Dispose()
	{
		foreach (var cache in _caches)
		{
			cache.Dispose();
		}
	}

	/// <summary>
	/// SAFETY, and the RED arm. A live key whose <c>deletion_allowed</c> is set is still reported
	/// <see cref="KeyStatus.Active"/>. RED input against the previous mapping: the flag being
	/// <see langword="true"/> on a key that exists and holds material, which is the state
	/// <c>UpdateEncryptionKeyConfigAsync</c> leaves behind when the delete that follows it fails.
	/// </summary>
	[Fact]
	public async Task ReportAKeyActiveWhenDeletionIsPermittedButNothingWasDestroyed()
	{
		RequireVault();

		var keyId = NewKeyId();

		using (var creator = CreateProvider())
		{
			var created = await creator.RotateKeyAsync(
				keyId,
				EncryptionAlgorithm.Aes256Gcm,
				purpose: null,
				expiresAt: null,
				TestContext.Current.CancellationToken).ConfigureAwait(true);

			created.Success.ShouldBeTrue(created.ErrorMessage ?? "the key under test must exist before the flag is set.");
		}

		await PermitDeletionAsync(keyId).ConfigureAwait(true);

		// Read back through Vault directly, so the arm is about server state rather than about what the test
		// believes it set. If this is false the arm proves nothing and must fail here rather than below.
		(await DeletionIsPermittedInVaultAsync(keyId).ConfigureAwait(true)).ShouldBeTrue(
			"deletion_allowed must really be set on the server, or this arm cannot detect the mapping.");

		// A fresh provider, so the status is computed from Vault rather than served from the cache populated
		// while the flag was still unset.
		using var reader = CreateProvider();

		var metadata = await reader.GetKeyAsync(keyId, TestContext.Current.CancellationToken).ConfigureAwait(true);

		metadata.ShouldNotBeNull("a key whose deletion is merely permitted has not been deleted.");
		metadata.Status.ShouldBe(
			KeyStatus.Active,
			"deletion_allowed is a permission, not a destruction schedule. Reporting anything else here wedges "
			+ "encryption for this subject, because the encrypt guard refuses every status but Active.");
	}

	/// <summary>
	/// SAFETY, on the seam that actually harms a consumer. With <c>deletion_allowed</c> set, a real encryption
	/// through the production provider still succeeds and still round-trips. This is the arm that binds the
	/// consequence rather than the status field: the previous mapping made this throw
	/// <see cref="EncryptionException"/> with <see cref="EncryptionErrorCode.KeyExpired"/>.
	/// </summary>
	[Fact]
	public async Task StillEncryptAndDecryptWhenDeletionIsPermitted()
	{
		RequireVault();

		var keyId = NewKeyId();
		var plaintext = "personal data written while deletion is merely permitted"u8.ToArray();
		var context = new EncryptionContext { KeyId = keyId };

		using (var creator = CreateProvider())
		{
			_ = await creator.RotateKeyAsync(
				keyId,
				EncryptionAlgorithm.Aes256Gcm,
				purpose: null,
				expiresAt: null,
				TestContext.Current.CancellationToken).ConfigureAwait(true);
		}

		await PermitDeletionAsync(keyId).ConfigureAwait(true);

		EncryptedData encrypted;
		using (var writingKeys = CreateProvider())
		using (var writer = new AesGcmEncryptionProvider(writingKeys, NullLogger<AesGcmEncryptionProvider>.Instance))
		{
			encrypted = await writer.EncryptAsync(plaintext, context, TestContext.Current.CancellationToken)
				.ConfigureAwait(true);
		}

		using var readingKeys = CreateProvider();
		using var reader = new AesGcmEncryptionProvider(readingKeys, NullLogger<AesGcmEncryptionProvider>.Instance);

		var decrypted = await reader.DecryptAsync(encrypted, context, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		decrypted.ShouldBe(
			plaintext,
			"a key whose deletion is permitted still protects live data; refusing to encrypt under it loses the write.");
	}

	/// <summary>
	/// SAFETY twin against over-correction. Permitting deletion must not suppress the fencing the encryption
	/// floor genuinely does express. A mapper that returned <see cref="KeyStatus.Active"/> unconditionally would
	/// satisfy the two arms above and report a rotated-out version as encryptable; this arm fails for it.
	/// </summary>
	[Fact]
	public async Task StillFenceAVersionBelowTheEncryptionFloorWhenDeletionIsPermitted()
	{
		RequireVault();

		var keyId = NewKeyId();

		using (var creator = CreateProvider())
		{
			_ = await creator.RotateKeyAsync(
				keyId,
				EncryptionAlgorithm.Aes256Gcm,
				purpose: null,
				expiresAt: null,
				TestContext.Current.CancellationToken).ConfigureAwait(true);

			// Rotate once so there is a superseded version to fence; the rotation installs the floor.
			var rotated = await creator.RotateKeyAsync(
				keyId,
				EncryptionAlgorithm.Aes256Gcm,
				purpose: null,
				expiresAt: null,
				TestContext.Current.CancellationToken).ConfigureAwait(true);

			rotated.Success.ShouldBeTrue(rotated.ErrorMessage ?? "a second version is needed to have a fenced one.");
			rotated.NewKey.ShouldNotBeNull();
			rotated.NewKey.Version.ShouldBeGreaterThan(1, "the rotation must have produced a later version.");
		}

		await PermitDeletionAsync(keyId).ConfigureAwait(true);

		using var reader = CreateProvider();

		var superseded = await reader.GetKeyVersionAsync(keyId, 1, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		superseded.ShouldNotBeNull("version 1 still exists after a rotation and must be describable.");
		superseded.Status.ShouldBe(
			KeyStatus.DecryptOnly,
			"the encryption floor still fences a superseded version. Ignoring deletion_allowed must not become "
			+ "reporting every version Active.");
	}

	/// <summary>
	/// LIVENESS. Without the flag a fresh key is <see cref="KeyStatus.Active"/>. Without this, a provider that
	/// reported nothing usable at all would satisfy every safety arm above.
	/// </summary>
	[Fact]
	public async Task ReportAFreshKeyActiveWhenDeletionIsNotPermitted()
	{
		RequireVault();

		var keyId = NewKeyId();

		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId,
			EncryptionAlgorithm.Aes256Gcm,
			purpose: null,
			expiresAt: null,
			TestContext.Current.CancellationToken).ConfigureAwait(true);

		(await DeletionIsPermittedInVaultAsync(keyId).ConfigureAwait(true)).ShouldBeFalse(
			"deletion_allowed defaults to false; this arm is only a liveness twin while that holds.");

		var metadata = await provider.GetKeyAsync(keyId, TestContext.Current.CancellationToken).ConfigureAwait(true);

		metadata.ShouldNotBeNull();
		metadata.Status.ShouldBe(KeyStatus.Active);
	}

	/// <summary>
	/// SAFETY, on the sibling of the same mechanism. A rotation installs the encryption floor through a
	/// <c>/config</c> write, and that write must not silently discard the rest of the key's configuration. RED
	/// input: an operator's <c>auto_rotate_period</c>, which the previous write reset from 259200 to 0 -- so
	/// the first rotation the framework performed disabled Vault-native auto-rotation the operator had
	/// configured, with nothing reported.
	/// </summary>
	/// <remarks>
	/// Kept in this class because it is the same defect as the one above, not a different subject: a
	/// <c>/config</c> write replacing state it never meant to touch. The two writes are the provider's only
	/// two, and each was destroying a different field.
	/// </remarks>
	[Fact]
	public async Task PreserveAnOperatorsAutoRotationPeriodAcrossARotation()
	{
		RequireVault();

		const long OperatorPeriodSeconds = 259200;

		var keyId = NewKeyId();
		var keyName = $"{KeyNamePrefix}{keyId}";

		using var provider = CreateProvider();

		_ = await provider.RotateKeyAsync(
			keyId,
			EncryptionAlgorithm.Aes256Gcm,
			purpose: null,
			expiresAt: null,
			TestContext.Current.CancellationToken).ConfigureAwait(true);

		// The operator configures Vault-native auto-rotation on their own key, outside the framework.
		await RawClient().V1.Secrets.Transit.UpdateEncryptionKeyConfigAsync(
			keyName,
			new UpdateKeyRequestOptions { AutoRotatePeriod = OperatorPeriodSeconds, MinimumDecryptionVersion = 1 },
			TransitMount).ConfigureAwait(true);

		var configured = await RawClient().V1.Secrets.Transit.ReadEncryptionKeyAsync(keyName, TransitMount)
			.ConfigureAwait(true);

		configured!.Data.AutoRotatePeriod.ShouldBe(
			OperatorPeriodSeconds,
			"the period must really be set on the server, or this arm cannot detect its loss.");

		// A framework rotation, whose own /config write installs the encryption floor.
		var rotated = await provider.RotateKeyAsync(
			keyId,
			EncryptionAlgorithm.Aes256Gcm,
			purpose: null,
			expiresAt: null,
			TestContext.Current.CancellationToken).ConfigureAwait(true);

		rotated.Success.ShouldBeTrue(rotated.ErrorMessage ?? "the rotation under test must have happened.");

		var after = await RawClient().V1.Secrets.Transit.ReadEncryptionKeyAsync(keyName, TransitMount)
			.ConfigureAwait(true);

		after!.Data.AutoRotatePeriod.ShouldBe(
			OperatorPeriodSeconds,
			"a rotation must not disable auto-rotation the operator configured; the /config write that installs "
			+ "the encryption floor has to carry the rest of the key's settings forward.");

		// LIVENESS in the same arm: the floor the write exists to install must still be installed, so
		// "preserve everything" cannot be satisfied by writing nothing at all.
		after.Data.MinimumEncryptionVersion.ShouldBe(
			after.Data.LatestVersion,
			"the rotation must still fence superseded versions out of new encryption.");
	}

	private void RequireVault() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Vault must be reachable: these locks bind what deletion_allowed means and are never skipped.");

	private static string NewKeyId() => $"delperm-{Guid.NewGuid():N}";

	// Sets the flag through Vault itself rather than through the provider, so the arms cannot be satisfied by
	// provider-side state.
	//
	// The read-then-merge is load-bearing and was learned the hard way: a /config write carrying ONLY
	// DeletionAllowed resets min_encryption_version to zero, so the first version of this helper un-fenced the
	// very version StillFenceAVersionBelowTheEncryptionFloorWhenDeletionIsPermitted was checking, and that arm
	// failed against a correct provider. Setting only the flag here is the whole point of these arms, so the
	// write must change only the flag.
	private async Task PermitDeletionAsync(string keyId)
	{
		var keyName = $"{KeyNamePrefix}{keyId}";
		var client = RawClient();

		var current = await client.V1.Secrets.Transit.ReadEncryptionKeyAsync(keyName, TransitMount)
			.ConfigureAwait(false);

		var request = new UpdateKeyRequestOptions { DeletionAllowed = true };

		if (current?.Data is { } data)
		{
			request.MinimumEncryptionVersion = data.MinimumEncryptionVersion;
			request.MinimumDecryptionVersion = data.MinimumDecryptionVersion;
			request.AutoRotatePeriod = data.AutoRotatePeriod;
			request.Exportable = data.Exportable;
			request.AllowPlaintextBackup = data.AllowPlaintextBackup;
		}

		await client.V1.Secrets.Transit.UpdateEncryptionKeyConfigAsync(keyName, request, TransitMount)
			.ConfigureAwait(false);
	}

	private async Task<bool> DeletionIsPermittedInVaultAsync(string keyId)
	{
		var info = await RawClient().V1.Secrets.Transit.ReadEncryptionKeyAsync(
			$"{KeyNamePrefix}{keyId}",
			TransitMount).ConfigureAwait(false);

		return info?.Data?.DeletionAllowed == true;
	}

	private IVaultClient RawClient() =>
		new VaultClient(new VaultClientSettings(_fixture.VaultAddress, new TokenAuthMethodInfo(_fixture.Token)));

	// A fresh cache per instance on purpose: a provider answering from a cache populated before the flag was
	// set would pass the safety arms without ever reading the flag.
	private VaultKeyProvider CreateProvider()
	{
		var cache = new MemoryCache(new MemoryCacheOptions());
		_caches.Add(cache);

		var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = new Uri(_fixture.VaultAddress),
			Auth = { AuthMethod = VaultAuthMethod.Token, Token = _fixture.Token },
			Keys = new() { TransitMountPath = TransitMount, KeyNamePrefix = KeyNamePrefix },
			MetadataCacheDuration = TimeSpan.FromMinutes(5)
		});

		return new VaultKeyProvider(options, cache, NullLogger<VaultKeyProvider>.Instance);
	}
}
