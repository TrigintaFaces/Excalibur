// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines.Transit;

using DispatchEncryptionAlgorithm = Excalibur.Compliance.EncryptionAlgorithm;

namespace Excalibur.Compliance.Vault;

/// <summary>
/// HashiCorp Vault implementation of <see cref="IKeyManagementProvider" /> and <see cref="IKeyManagementAdmin" />.
/// </summary>
/// <remarks>
/// <para> This provider integrates with HashiCorp Vault's Transit secrets engine:
/// <list type="bullet">
/// <item> Server-side encryption (key material never leaves Vault) </item>
/// <item> Automatic key versioning and rotation </item>
/// <item> Cross-datacenter replication support </item>
/// <item> Multiple authentication methods (Token, AppRole, Kubernetes) </item>
/// </list>
/// </para>
/// <para>
/// <strong> Important: </strong> The Transit secrets engine performs cryptographic operations server-side. Key material is never exposed to
/// the client.
/// </para>
/// </remarks>
public sealed partial class VaultKeyProvider : IKeyManagementProvider, IDurableKeyProvider, IKeyManagementAdmin, IKeyDestructionStatusProvider, IDisposable
{
	private static readonly CompositeFormat KubernetesJwtNotFoundFormat =
		CompositeFormat.Parse(Resources.VaultKeyProvider_KubernetesJwtNotFound);

	private static readonly CompositeFormat AuthMethodNotSupportedFormat =
		CompositeFormat.Parse(Resources.VaultKeyProvider_AuthMethodNotSupported);

	private readonly VaultClient _vaultClient;
	private readonly IMemoryCache _cache;
	private readonly ILogger<VaultKeyProvider> _logger;
	private readonly VaultOptions _options;
	private readonly SemaphoreSlim _rateLimitSemaphore = new(10, 10);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="VaultKeyProvider" /> class.
	/// </summary>
	/// <param name="options"> The Vault configuration options. </param>
	/// <param name="cache"> The memory cache for caching key metadata. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	/// <exception cref="ArgumentNullException"> Thrown when options, cache, or logger is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when VaultUri is not configured. </exception>
	public VaultKeyProvider(
		IOptions<VaultOptions> options,
		IMemoryCache cache,
		ILogger<VaultKeyProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_cache = cache;
		_logger = logger;

		if (_options.VaultUri is null)
		{
			throw new ArgumentException(Resources.VaultKeyProvider_VaultUriMustBeConfigured, nameof(options));
		}

		_vaultClient = CreateVaultClient();

		LogInitialized(_options.VaultUri, _options.Auth.AuthMethod);
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = GetCacheKey(keyId);
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			return await ApplySuspensionStatusAsync(keyId, cached, cancellationToken).ConfigureAwait(false);
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				keyName,
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			if (keyInfo?.Data is null)
			{
				LogKeyNotFoundTransit(keyId);
				return null;
			}

			// Purpose is resolved here too, so GetKeyAsync and the purpose-scoped resolution in
			// ListKeysAsync/GetActiveKeyAsync describe the same key identically. Leaving it null here
			// would let the two accessors disagree about the same key.
			var metadata = MapToKeyMetadata(keyId, keyInfo.Data) with
			{
				Purpose = await ReadKeyPurposeAsync(keyId, cancellationToken).ConfigureAwait(false),
				Generation = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false),
			};
			CacheMetadata(cacheKey, metadata);

			return await ApplySuspensionStatusAsync(keyId, metadata, cancellationToken).ConfigureAwait(false);
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
		{
			LogKeyNotFound(keyId);
			return null;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = GetCacheKey(keyId, version);
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			return await ApplySuspensionStatusAsync(keyId, cached, cancellationToken).ConfigureAwait(false);
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				keyName,
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			if (keyInfo?.Data is null)
			{
				LogKeyNotFound(keyId);
				return null;
			}

			// Check if the requested version exists
			if (keyInfo.Data.Keys is null || !keyInfo.Data.Keys.ContainsKey(version.ToString()))
			{
				LogKeyVersionNotFound(keyId, version);
				return null;
			}

			// Overlaid here for the same reason GetKeyAsync overlays it: MapToKeyMetadata cannot see the
			// purpose sidecar, so without this a version lookup would describe the key as having no
			// purpose while GetKeyAsync describes the same key as having one.
			var metadata = MapToKeyMetadata(keyId, keyInfo.Data, version) with
			{
				Purpose = await ReadKeyPurposeAsync(keyId, cancellationToken).ConfigureAwait(false),
				Generation = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false),
			};
			CacheMetadata(cacheKey, metadata);

			return await ApplySuspensionStatusAsync(keyId, metadata, cancellationToken).ConfigureAwait(false);
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
		{
			LogKeyNotFound(keyId);
			return null;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<KeyMetadata>> ListKeysAsync(
		KeyStatus? status,
		string? purpose,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<KeyMetadata>();

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keys = await _vaultClient.V1.Secrets.Transit.ReadAllEncryptionKeysAsync(
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			if (keys?.Data?.Keys is null)
			{
				return results;
			}

			foreach (var keyName in keys.Data.Keys)
			{
				// Only include keys with our prefix
				if (!keyName.StartsWith(_options.Keys.KeyNamePrefix, StringComparison.Ordinal))
				{
					continue;
				}

				var keyId = keyName[_options.Keys.KeyNamePrefix.Length..];

				try
				{
					var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
						keyName,
						_options.Keys.TransitMountPath).ConfigureAwait(false);

					if (keyInfo?.Data is null)
					{
						continue;
					}

					var metadata = MapToKeyMetadata(keyId, keyInfo.Data);

					// Surface a durably-suspended key as Suspended so status filtering (e.g. Active) excludes
					// it and an admin status filter can still list it.
					if (await IsKeySuspendedAsync(keyId, cancellationToken).ConfigureAwait(false))
					{
						metadata = metadata with { Status = KeyStatus.Suspended };
					}

					// Apply filters
					if (status.HasValue && metadata.Status != status.Value)
					{
						continue;
					}

					// Purpose lives in a KV sidecar, not on the Transit key, so it must be resolved before
					// filtering. Previously MapToKeyMetadata hardcoded Purpose = null and this comparison
					// therefore excluded EVERY key whenever a non-null purpose was requested, which made
					// GetActiveKeyAsync(purpose) incapable of returning anything.
					metadata = metadata with
					{
						Purpose = await ReadKeyPurposeAsync(keyId, cancellationToken).ConfigureAwait(false),
						Generation = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false),
					};

					if (purpose is not null && metadata.Purpose != purpose)
					{
						continue;
					}

					results.Add(metadata);
				}
				catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
				{
					continue;
				}
			}
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}

		return results;
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// The <paramref name="purpose"/> argument is three-valued here. <see langword="null"/> leaves any
	/// previously recorded purpose untouched — a rotation that does not mention the purpose must not
	/// erase one. An empty or whitespace string removes it. Any other value records it.
	/// </para>
	/// <para>
	/// The empty-string case exists so a purpose is not write-once: without a spelling that means
	/// "no purpose", a value recorded on a key could never be taken off it through the public API.
	/// </para>
	/// </remarks>
	public async Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		DispatchEncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			KeyMetadata? previousKeyMetadata = null;

			// Check if key exists
			var keyExists = false;
			try
			{
				var existingKey = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				if (existingKey?.Data is not null)
				{
					keyExists = true;
					previousKeyMetadata = MapToKeyMetadata(keyId, existingKey.Data);
				}
			}
			catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
			{
				// Key doesn't exist
			}

			if (keyExists)
			{
				// Rotate existing key
				await _vaultClient.V1.Secrets.Transit.RotateEncryptionKeyAsync(
					keyName,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				// Get updated key info
				var rotatedKey = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				// Fence the superseded versions out of NEW encryption, which is what makes them
				// decrypt-only in fact rather than merely in our reporting. Vault governs this natively:
				// min_encryption_version bars older versions from encrypting while min_decryption_version
				// leaves them able to decrypt. Until this was installed, rotation left version 1 fully
				// encryption-capable and the provider was truthfully reporting a state it had never changed
				// -- so patching only the status mapper would have reported a fence that did not exist.
				//
				// The value is the NEW LATEST version, not latest + 1. Vault caps min_encryption_version at
				// the latest version and rejects anything above it with a 400; an earlier attempt in this
				// provider used latest + 1 and threw on the happy path, which is worse than doing nothing.
				if (rotatedKey?.Data is not null)
				{
					await UpdateKeyConfigPreservingRestAsync(
						keyName,
						rotatedKey.Data,
						minimumEncryptionVersion: rotatedKey.Data.LatestVersion,
						deletionAllowed: null).ConfigureAwait(false);
				}

				// Persist BEFORE invalidating, so the cache cannot be repopulated from a stale sidecar
				// between the two. The purpose argument is three-valued: null leaves any previously
				// recorded purpose untouched, an empty string removes it, and a non-empty value records it.
				await WriteKeyPurposeAsync(keyId, purpose, cancellationToken).ConfigureAwait(false);

				var newMetadata = MapToKeyMetadata(keyId, rotatedKey.Data) with
				{
					Purpose = await ReadKeyPurposeAsync(keyId, cancellationToken).ConfigureAwait(false),
				Generation = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false),
				};

				InvalidateCache(keyId);

				LogRotatedKey(keyId, newMetadata.Version);

				// Report the superseded version as DecryptOnly. Its metadata was read BEFORE the encryption
				// floor was installed, so it still describes the key as it was a moment ago -- reporting that
				// verbatim would tell the caller the old version is still encryption-capable when rotation
				// has just fenced it.
				var supersededMetadata = previousKeyMetadata is null
					? null
					: previousKeyMetadata with { Status = KeyStatus.DecryptOnly };

				return KeyRotationResult.Succeeded(newMetadata, supersededMetadata);
			}
			else
			{
				// Create new key with the specified key type
				var createRequest = new CreateKeyRequestOptions
				{
					Exportable = _options.Keys.AllowKeyExport,
					AllowPlaintextBackup = _options.Keys.AllowPlaintextBackup,
					Type = MapToVaultKeyType(algorithm),
					ConvergentEncryption = _options.Keys.EnableConvergentEncryption,
					Derived = _options.Keys.EnableKeyDerivation
				};

				await _vaultClient.V1.Secrets.Transit.CreateEncryptionKeyAsync(
					keyName,
					createRequest,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				// Get the newly created key info
				var newKey = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				// Same on the create path: the purpose the caller supplied is recorded here or it is lost,
				// since Vault Transit has nowhere to carry it.
				await WriteKeyPurposeAsync(keyId, purpose, cancellationToken).ConfigureAwait(false);

				var newMetadata = MapToKeyMetadata(keyId, newKey.Data) with
				{
					Purpose = await ReadKeyPurposeAsync(keyId, cancellationToken).ConfigureAwait(false),
				Generation = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false),
				};

				// Record the identity of the material just provisioned, if this handle has none. Transit offers no
				// per-generation value of its own -- its ordinals restart at 1 after a delete-and-create -- so the
				// identifier is minted here and kept in a sidecar of its own. Write-if-absent: a rotation keeps the
				// identity of the lineage it extends.
				_ = await EnsureKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false);

				LogCreatedKey(keyId);

				return KeyRotationResult.Succeeded(newMetadata);
			}
		}
		catch (VaultSharp.Core.VaultApiException ex)
		{
			LogRotateFailed(keyId, ex);
			return KeyRotationResult.Failed($"Vault error: {ex.Message}");
		}
		catch (Exception ex)
		{
			LogRotateUnexpected(keyId, ex);
			return KeyRotationResult.Failed(ex.Message);
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// Transit separates the two operations that matter here: creating a key and rotating one are different
	/// endpoints, so this never reaches the rotate path and never installs an encryption floor. An existing key
	/// is returned as <see cref="GetKeyAsync"/> would return it, untouched.
	/// </para>
	/// <para>
	/// The create is attempted only after a read reports the key absent, and a create that then fails because
	/// the key appeared in between is treated as the no-op it is: the winner's key is read back and returned.
	/// The read-back is not an optimisation -- it is what makes the returned metadata describe what Vault
	/// actually holds rather than what this call attempted.
	/// </para>
	/// </remarks>
	public async Task<KeyMetadata> CreateKeyIfAbsentAsync(
		string keyId,
		DispatchEncryptionAlgorithm algorithm,
		string? purpose,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var existing = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (existing is not null)
		{
			return existing;
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			try
			{
				await _vaultClient.V1.Secrets.Transit.CreateEncryptionKeyAsync(
					keyName,
					new CreateKeyRequestOptions
					{
						Exportable = _options.Keys.AllowKeyExport,
						AllowPlaintextBackup = _options.Keys.AllowPlaintextBackup,
						Type = MapToVaultKeyType(algorithm),
						ConvergentEncryption = _options.Keys.EnableConvergentEncryption,
						Derived = _options.Keys.EnableKeyDerivation
					},
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				// The purpose has nowhere to live on a Transit key, so it is recorded in the sidecar or lost.
				await WriteKeyPurposeAsync(keyId, purpose, cancellationToken).ConfigureAwait(false);

				// Record the identity of the material just provisioned, if this handle has none. Transit offers no
				// per-generation value of its own -- its ordinals restart at 1 after a delete-and-create -- so the
				// identifier is minted here and kept in a sidecar of its own. Write-if-absent: a rotation keeps the
				// identity of the lineage it extends.
				_ = await EnsureKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false);

				LogCreatedKey(keyId);
			}
			catch (VaultSharp.Core.VaultApiException)
			{
				// Either another writer created the key between the read above and this create, or the create
				// itself failed. Which one it was is decided by whether a key is there now, not by the
				// exception: a lost race is a no-op and must not surface as an error, while a genuine failure
				// must not surface as a key.
				InvalidateCache(keyId);

				var winner = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

				return winner
					?? throw new EncryptionException(
						$"Vault could not provision a key at '{keyId}', and no key is present there.")
					{
						ErrorCode = EncryptionErrorCode.KeyNotFound
					};
			}
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}

		InvalidateCache(keyId);

		var created = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		return created
			?? throw new EncryptionException(
				$"Vault reported a key created at '{keyId}' but does not hold one.")
			{
				ErrorCode = EncryptionErrorCode.KeyNotFound
			};
	}

	/// <inheritdoc />
	/// <remarks>
	/// A generation other than the one recorded at this handle is destroyed: Transit holds one lineage of
	/// material per key name, so a handle recording a different identity has had its material replaced, and a
	/// handle recording none has nothing this caller's payload could have been written under. The handle looking
	/// alive is exactly the case this answers, because a key deleted and created again reports as healthy.
	/// </remarks>
	public async Task<bool> IsKeyDestroyedAsync(string keyId, string generation, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(generation);

		// The handle being gone settles it without needing the marker, and it is also the state in which the
		// marker has been erased alongside the material.
		if (await IsKeyDestroyedAsync(keyId, cancellationToken).ConfigureAwait(false))
		{
			return true;
		}

		var recorded = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false);

		return !string.Equals(recorded, generation, StringComparison.Ordinal);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Transit has no soft-delete: deleting a key removes its material permanently, so a key Transit does not hold
	/// is a destroyed key. The cache is bypassed, because a cached entry would describe a key that may since have
	/// been deleted. Any other failure to read the key is thrown, never reported as either answer.
	/// </remarks>
	public async Task<bool> IsKeyDestroyedAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				GetKeyName(keyId),
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			return keyInfo?.Data is null;
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
		{
			return true;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Transit retires individual versions as well as whole keys: trimming a key removes the material of every
	/// version below its minimum, permanently, and leaves the later versions live. A key holding one trimmed version
	/// and one live version is therefore not a destroyed key while an envelope naming the trimmed version has
	/// nothing left to decrypt with, which is why a read asks this overload. A version Transit no longer lists is
	/// destroyed rather than merely absent, because Transit has no soft-delete and nothing can restore it. Raising
	/// the minimum decryption version alone does not trim: the material stays listed and is reported live, since
	/// lowering the minimum again makes it decryptable. The cache is bypassed and any other read failure is thrown.
	/// </remarks>
	public async Task<bool> IsKeyDestroyedAsync(string keyId, int version, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				GetKeyName(keyId),
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			return keyInfo?.Data?.Keys is null
				|| !keyInfo.Data.Keys.ContainsKey(version.ToString(CultureInfo.InvariantCulture));
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
		{
			return true;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyDestructionOutcome> DeleteKeyAsync(string keyId, int retentionDays, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			bool keyExisted;

			try
			{
				// First, update the key to allow deletion -- carrying the rest of its configuration forward,
				// because this write does not only set the flag. See UpdateKeyConfigPreservingRestAsync: a
				// bare DeletionAllowed write resets min_encryption_version to zero, which un-fences every
				// superseded version. That is invisible when the delete below succeeds and permanent when it
				// does not, and a delete that fails after this point is exactly the case this path must leave
				// safe.
				var current = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				await UpdateKeyConfigPreservingRestAsync(
					keyName,
					current?.Data,
					minimumEncryptionVersion: null,
					deletionAllowed: true).ConfigureAwait(false);

				// Now delete the key
				await _vaultClient.V1.Secrets.Transit.DeleteEncryptionKeyAsync(
					keyName,
					_options.Keys.TransitMountPath).ConfigureAwait(false);

				keyExisted = true;
			}
			catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
			{
				LogKeyNotFoundForDeletion(keyId);
				keyExisted = false;
			}

			// Erase the KV sidecars whether or not the Transit key was present. This is the crypto-shredding
			// path, so it must leave nothing behind that describes the erased key: the purpose sidecar records
			// what the key protected (values such as "customer-pii-eu" are the intended use), and the
			// suspension marker records a caller-supplied free-text reason. Destroying the key material while
			// leaving either of those in place is an erasure that did not erase.
			//
			// Running this even when the key was absent makes the operation repairable: a delete that
			// previously destroyed the key material and then failed part-way leaves orphans that a later call
			// clears, rather than orphans no code path can ever reach again.
			//
			// Failures are NOT swallowed. A sidecar that survived means erasure is incomplete, and reporting
			// Completed for it would be exactly the false assurance this path exists to avoid.
			await EraseKeyMetadataAsync(keyId).ConfigureAwait(false);

			InvalidateCache(keyId);

			if (!keyExisted)
			{
				return KeyDestructionOutcome.NotFound;
			}

			LogDeletedKey(keyId);

			// Vault Transit deletes the key material immediately — irrecoverable on return.
			return KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow);
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <summary>
	/// Permanently removes every KV document describing a key: its purpose sidecar and its suspension marker.
	/// </summary>
	/// <remarks>
	/// <c>DeleteMetadataAsync</c> removes the document and all of its versions, which is what erasure requires
	/// — a KV&#160;v2 soft delete would leave the prior version readable. Deleting an absent document is
	/// idempotent (Vault answers 204), so this is safe for a key that was never suspended or never given a
	/// purpose.
	/// </remarks>
	/// <param name="keyId">The key whose descriptive metadata is being erased.</param>
	private async Task EraseKeyMetadataAsync(string keyId)
	{
		await _vaultClient.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(
			GetPurposeMarkerPath(keyId),
			mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

		await _vaultClient.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(
			GetSuspensionMarkerPath(keyId),
			mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

		// THE GENERATION MARKER DIES WITH THE KEY, and this deletion is load-bearing rather than tidiness.
		// The handle is derived from the subject, so an ordinary write after an erasure provisions new material
		// at the SAME path. If this document survived, the mint would find it and hand the new material the
		// destroyed generation's identifier -- and every envelope written before the erasure would then match
		// the live generation and be reported NOT destroyed. That is the precise confusion the identifier
		// exists to remove, so leaving the marker behind would reintroduce it at this provider.
		await _vaultClient.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(
			GetGenerationMarkerPath(keyId),
			mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Suspension is enforced at the Excalibur provider boundary, not at the Vault server. A suspended key is
	/// recorded as a DURABLE KV marker and surfaced as <see cref="KeyStatus.Suspended"/> from this provider's
	/// retrieval path, so the framework's encryption provider refuses it for both encryption and decryption.
	/// It does NOT revoke the key at the Vault server for a raw, non-framework Vault client (Vault Transit has
	/// no native key-disable primitive — <c>min_encryption_version</c> is capped at the latest version). For
	/// server-side, any-client enforcement, an operator can additionally apply a Vault ACL policy denying
	/// <c>transit/encrypt/&lt;key&gt;</c> as defense-in-depth. Suspension survives process restarts.
	/// </para>
	/// <para>
	/// <strong>Availability coupling (fail-closed):</strong> because the marker is persisted in the Vault
	/// KV&#160;v2 mount named by <c>VaultSuspensionOptions.MountPath</c>, that mount is a HARD PREREQUISITE for
	/// suspension. If the mount is absent or unreachable, the marker can be neither written nor read back — a
	/// key could then appear active despite being suspended (a fail-open security hole). To prevent that, the
	/// mount is validated at host startup and startup fails fast when it is not reachable, rather than letting
	/// suspension become silently inert at runtime.
	/// </para>
	/// </remarks>
	public async Task<bool> SuspendKeyAsync(string keyId, string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Verify the key exists before suspending it (the contract returns false for an unknown key).
			EncryptionKeyInfo? keyData;
			try
			{
				var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					GetKeyName(keyId),
					_options.Keys.TransitMountPath).ConfigureAwait(false);
				keyData = keyInfo?.Data;
			}
			catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
			{
				LogKeyNotFoundForSuspension(keyId);
				return false;
			}

			if (keyData is null)
			{
				LogKeyNotFoundForSuspension(keyId);
				return false;
			}

			// Durably record the suspension. Vault Transit has NO native "disable key
			// encryption" primitive — min_encryption_version is CAPPED at the latest version, so the previous
			// "LatestVersion + 1" was rejected by Vault (400) and threw on the happy path (worse than a no-op).
			// Suspension is enforced at the provider boundary: persist a DURABLE marker in Vault KV (NOT an
			// in-memory set, which would lift on restart) keyed by keyId. The enforcement gate is the
			// GetKey*/key-status resolution path — it reads this marker (via IsKeySuspendedAsync) and surfaces
			// the key as KeyStatus.Suspended, so every caller that resolves key status before encrypt/decrypt
			// gets a Suspended key and refuses it. Genuine KV failures propagate (no broad swallow); only
			// key-not-found returns false.
			var marker = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["reason"] = reason,
				["suspendedAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
			};

			_ = await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
				GetSuspensionMarkerPath(keyId),
				marker,
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

			InvalidateCache(keyId);

			LogKeySuspended(keyId, reason);

			return true;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> ReactivateKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Verify the key exists before reactivating it (the contract returns false for an unknown key).
			EncryptionKeyInfo? keyData;
			try
			{
				var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					GetKeyName(keyId),
					_options.Keys.TransitMountPath).ConfigureAwait(false);
				keyData = keyInfo?.Data;
			}
			catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
			{
				LogKeyNotFoundForReactivation(keyId);
				return false;
			}

			if (keyData is null)
			{
				LogKeyNotFoundForReactivation(keyId);
				return false;
			}

			// Inverse of SuspendKeyAsync: suspension persists a DURABLE marker in Vault KV; reactivation
			// removes it. DeleteMetadataAsync permanently deletes the marker and all its versions so key-status
			// resolution surfaces the key as Active again. Deleting an absent marker is idempotent (Vault returns
			// 204), so reactivating a never-suspended existing key is a safe no-op. Genuine KV failures propagate.
			await _vaultClient.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(
				GetSuspensionMarkerPath(keyId),
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

			InvalidateCache(keyId);

			LogKeyReactivated(keyId);

			return true;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cacheKey = $"active:{purpose ?? "default"}";
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached)
			&& cached is not null
			&& !await IsKeySuspendedAsync(cached.KeyId, cancellationToken).ConfigureAwait(false))
		{
			// Bypass the cache if the cached active key has since been suspended — re-resolve via
			// ListKeysAsync, which excludes suspended keys, so a suspended key is never returned as active.
			return cached;
		}

		var keys = await ListKeysAsync(KeyStatus.Active, purpose, cancellationToken).ConfigureAwait(false);

		// Return the most recently created active key
		var activeKey = keys
			.Where(k => !k.ExpiresAt.HasValue || k.ExpiresAt.Value > DateTimeOffset.UtcNow)
			// An UNDATED key sorts OLDEST, so it is never chosen as the most recent while any dated key is a
			// candidate. Written as two keys rather than relying on the default comparer ranking null below
			// every value: the direction is a safety property, and it should take a visible edit to reverse.
			.OrderByDescending(k => k.CreatedAt.HasValue)
			.ThenByDescending(k => k.CreatedAt)
			.FirstOrDefault();

		if (activeKey is not null)
		{
			_ = _cache.Set(cacheKey, activeKey, _options.MetadataCacheDuration);
		}

		return activeKey;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_rateLimitSemaphore.Dispose();
		_disposed = true;

		LogDisposed();
	}

	private static TransitKeyType MapToVaultKeyType(DispatchEncryptionAlgorithm algorithm)
	{
		return algorithm switch
		{
			DispatchEncryptionAlgorithm.Aes256Gcm => TransitKeyType.aes256_gcm96,
			_ => TransitKeyType.aes256_gcm96
		};
	}

	/// <summary>
	/// Writes a Transit key's <c>/config</c> while carrying forward every setting the caller did not ask to
	/// change.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <c>/config</c> write is not a patch. The fields the client serializes REPLACE what the server holds,
	/// so a write that means to set one field silently resets others to their default -- and which fields
	/// those are is a property of the client, not something to be reasoned about. MEASURED against a real
	/// Vault: a write setting only <c>deletion_allowed</c> reset <c>min_encryption_version</c> from 2 to 0,
	/// and a write setting only <c>min_encryption_version</c> reset an operator's
	/// <c>auto_rotate_period</c> from 259200 to 0. <c>exportable</c> and <c>min_decryption_version</c>
	/// survived both, so the damage is field-specific rather than uniform.
	/// </para>
	/// <para>
	/// Both losses are consumer-visible. The reset encryption floor un-fences every superseded version, and
	/// the reset rotation period disables Vault-native auto-rotation an operator configured, on the first
	/// rotation the framework performs. Carrying the read-back state forward is what makes either write mean
	/// only what it says.
	/// </para>
	/// </remarks>
	/// <param name="keyName">The Transit key name.</param>
	/// <param name="current">
	/// The key's configuration as just read from Vault, or <see langword="null"/> when it could not be read.
	/// A null here writes only what the caller asked for, which is the best available behaviour: the
	/// alternative is refusing to proceed on a read failure, and the delete path must still be able to erase.
	/// </param>
	/// <param name="minimumEncryptionVersion">The new encryption floor, or <see langword="null"/> to keep the current one.</param>
	/// <param name="deletionAllowed">The new deletion permission, or <see langword="null"/> to keep the current one.</param>
	private async Task UpdateKeyConfigPreservingRestAsync(
		string keyName,
		EncryptionKeyInfo? current,
		int? minimumEncryptionVersion,
		bool? deletionAllowed)
	{
		var request = new UpdateKeyRequestOptions
		{
			MinimumEncryptionVersion = minimumEncryptionVersion ?? current?.MinimumEncryptionVersion ?? 0,
			DeletionAllowed = deletionAllowed ?? current?.DeletionAllowed ?? false,
		};

		if (current is not null)
		{
			request.MinimumDecryptionVersion = current.MinimumDecryptionVersion;
			request.AutoRotatePeriod = current.AutoRotatePeriod;
			request.Exportable = current.Exportable;
			request.AllowPlaintextBackup = current.AllowPlaintextBackup;
		}

		await _vaultClient.V1.Secrets.Transit.UpdateEncryptionKeyConfigAsync(
			keyName,
			request,
			_options.Keys.TransitMountPath).ConfigureAwait(false);
	}

	private static KeyStatus DetermineKeyStatus(EncryptionKeyInfo keyInfo, int version)
	{
		// PendingDestruction IS NOT REACHABLE ON TRANSIT, and deletion_allowed must never be read as evidence
		// of one. It is a CONFIGURATION FLAG meaning the key MAY be deleted -- default false, set on the key's
		// /config endpoint as a precondition of DELETE succeeding. It is a permission, not a schedule, and
		// Transit has no soft-delete or pending state for it to describe: deleting a key removes its material
		// permanently, which is what IsKeyDestroyedAsync states in its own remarks.
		//
		// Mapping it to PendingDestruction wedged encryption, because every status other than Active throws on
		// the encrypt path. Two ordinary situations reached it. A delete that set the flag and then failed --
		// network, permission, rate limit, cancellation -- left the flag set forever, so a subject whose
		// erasure half-completed could never have their personal data written again. And an operator who sets
		// deletion_allowed on their own keys, a reasonable posture for a crypto-shred deployment, broke
		// encryption for every subject at once.
		//
		// The related hazard, recorded so it is not reintroduced: never read PendingDestruction as evidence of
		// destruction anywhere else either. Degrading a read open on it would return a null "lawfully erased"
		// value for fully recoverable data, silently, on any deployment with the flag set.

		// A version below the encryption floor can still decrypt but may no longer encrypt, which is
		// exactly DecryptOnly. Rotation installs that floor; reading it back here is what makes the two
		// halves agree. Reporting the floor without installing it would be a worse defect than the one
		// this replaces, so the two changes belong together.
		if (keyInfo.MinimumEncryptionVersion > 0 && version < keyInfo.MinimumEncryptionVersion)
		{
			return KeyStatus.DecryptOnly;
		}

		return KeyStatus.Active;
	}

	[LoggerMessage(LogLevel.Information,
		"VaultKeyProvider initialized for vault {VaultUri} using {AuthMethod} authentication")]
	private partial void LogInitialized(Uri vaultUri, VaultAuthMethod authMethod);

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} not found in Vault Transit engine")]
	private partial void LogKeyNotFoundTransit(string keyId);

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} not found in Vault")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} version {Version} not found")]
	private partial void LogKeyVersionNotFound(string keyId, int version);

	[LoggerMessage(LogLevel.Information, "Rotated Vault Transit key {KeyId} to version {Version}")]
	private partial void LogRotatedKey(string keyId, int version);

	[LoggerMessage(LogLevel.Information, "Created new Vault Transit key {KeyId}")]
	private partial void LogCreatedKey(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to rotate key {KeyId} in Vault")]
	private partial void LogRotateFailed(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Error, "Unexpected error rotating key {KeyId}")]
	private partial void LogRotateUnexpected(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"Deleted Vault Transit key {KeyId} (crypto-shredding). This operation is irreversible.")]
	private partial void LogDeletedKey(string keyId);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} not found for deletion")]
	private partial void LogKeyNotFoundForDeletion(string keyId);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} not found for suspension")]
	private partial void LogKeyNotFoundForSuspension(string keyId);

	[LoggerMessage(LogLevel.Warning, "Suspended Vault Transit key {KeyId}: {Reason}")]
	private partial void LogKeySuspended(string keyId, string reason);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} not found for reactivation")]
	private partial void LogKeyNotFoundForReactivation(string keyId);

	[LoggerMessage(LogLevel.Information, "Reactivated Vault Transit key {KeyId}")]
	private partial void LogKeyReactivated(string keyId);

	[LoggerMessage(LogLevel.Information,
		"Vault KV v2 suspension-marker mount {MountPath} is reachable; key suspension is durably enforceable")]
	private partial void LogSuspensionMountReachable(string mountPath);

	[LoggerMessage(LogLevel.Debug, "VaultKeyProvider disposed")]
	private partial void LogDisposed();

	private VaultClient CreateVaultClient()
	{
		IAuthMethodInfo authMethod = _options.Auth.AuthMethod switch
		{
			VaultAuthMethod.Token => new TokenAuthMethodInfo(_options.Auth.Token),
			VaultAuthMethod.AppRole => new AppRoleAuthMethodInfo(
				_options.Auth.AppRoleMountPath,
				_options.Auth.AppRoleId,
				_options.Auth.AppRoleSecretId),
			VaultAuthMethod.Kubernetes => new KubernetesAuthMethodInfo(
				_options.Auth.KubernetesMountPath,
				_options.Auth.KubernetesRole,
				File.Exists(_options.Auth.KubernetesJwtPath)
					? File.ReadAllText(_options.Auth.KubernetesJwtPath)
					: throw new InvalidOperationException(string.Format(
						CultureInfo.InvariantCulture,
						KubernetesJwtNotFoundFormat,
						_options.Auth.KubernetesJwtPath))),
			_ => throw new NotSupportedException(string.Format(
				CultureInfo.InvariantCulture,
				AuthMethodNotSupportedFormat,
				_options.Auth.AuthMethod))
		};

		var vaultClientSettings = new VaultClientSettings(_options.VaultUri!.ToString(), authMethod)
		{
			Namespace = _options.Namespace,
			ContinueAsyncTasksOnCapturedContext = false
		};

		return new VaultClient(vaultClientSettings);
	}

	private string GetKeyName(string keyId) => $"{_options.Keys.KeyNamePrefix}{keyId}";

	private string GetCacheKey(string keyId, int? version = null) =>
		version.HasValue ? $"vault:{keyId}:v{version}" : $"vault:{keyId}:latest";

	private void CacheMetadata(string cacheKey, KeyMetadata metadata) =>
		_cache.Set(cacheKey, metadata, _options.MetadataCacheDuration);

	private void InvalidateCache(string keyId)
	{
		_cache.Remove(GetCacheKey(keyId));
		_cache.Remove($"active:default");
		_cache.Remove(GetSuspensionCacheKey(keyId));
		_cache.Remove(GetPurposeCacheKey(keyId));
		_cache.Remove(GetGenerationCacheKey(keyId));
	}

	private string GetSuspensionMarkerPath(string keyId) => $"{_options.Suspension.Path}/{keyId}";

	/// <summary>
	/// Path of the per-key PURPOSE sidecar.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Vault Transit keys carry no user-defined metadata, so a purpose supplied to
	/// <c>RotateKeyAsync</c> has nowhere to live on the key itself. It is kept in the same KV v2 mount
	/// this provider already uses for suspension markers, one document per key.
	/// </para>
	/// <para>
	/// Deliberately a SEPARATE path from the suspension marker rather than another field inside it.
	/// <see cref="IsKeySuspendedAsync"/> treats "document exists" as "suspended", so writing purpose into
	/// that same document would make every key with a purpose read as Suspended — a security-relevant
	/// regression. Keeping them apart leaves the suspension predicate untouched.
	/// </para>
	/// <para>
	/// The two live under DISJOINT roots -- suspension reads <c>{Path}/{keyId}</c>, purpose reads
	/// <c>{PurposePath}/{keyId}</c> -- so no key identifier can make one address the other. An earlier
	/// layout nested purpose at <c>{Path}/purpose/{keyId}</c>, where a key identifier of <c>purpose/foo</c>
	/// addressed the purpose sidecar of the key <c>foo</c> and so read back as suspended.
	/// </para>
	/// </remarks>
	/// <param name="keyId">The key identifier.</param>
	/// <returns>The KV path holding the key's purpose.</returns>
	private string GetPurposeMarkerPath(string keyId) => $"{_options.Suspension.PurposePath}/{keyId}";

	private static string GetPurposeCacheKey(string keyId) => $"purpose:{keyId}";

	// A document of its own rather than a field on the purpose marker, because clearing a purpose DELETES that
	// document and all its versions -- so sharing it would make "clear this key's purpose" silently destroy the
	// identity of the material, and every payload written under it would become unreadable.
	private string GetGenerationMarkerPath(string keyId) => $"{_options.Suspension.PurposePath}-generation/{keyId}";

	private static string GetGenerationCacheKey(string keyId) => $"generation:{keyId}";

	/// <summary>
	/// Records the identifier of the material provisioned at a handle, if one is not already recorded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Transit gives us nothing to use here, which is why the value is ours.</b> Its version numbers are
	/// ordinals that restart at 1 when a key is deleted and created again, so they identify a position in a
	/// lineage rather than the material itself; the only per-version datum it exposes is a creation timestamp,
	/// and two provisionings inside one resolution window share it. Minting the identifier ourselves makes it
	/// unique by construction rather than by hoping the backend's is.
	/// </para>
	/// <para>
	/// <b>Write-if-absent, never overwrite.</b> A rotation adds a version to the same material lineage and must
	/// keep the same identity; only a provisioning at a handle that holds nothing is new material. Overwriting
	/// on every call would change the identity under payloads already written and make them unreadable.
	/// </para>
	/// </remarks>
	private async Task<string> EnsureKeyGenerationAsync(string keyId, CancellationToken cancellationToken)
	{
		var existing = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (!string.IsNullOrEmpty(existing))
		{
			return existing;
		}

		cancellationToken.ThrowIfCancellationRequested();

		// A CSPRNG rather than a GUID. The identifier is not secret -- it travels in cleartext on every payload
		// -- but reaching for a GUID beside key material is the habit worth not having.
		var generation = RandomNumberGenerator.GetHexString(32);

		var marker = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["generation"] = generation,
			["recordedAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
		};

		await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
			GetGenerationMarkerPath(keyId),
			marker,
			mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

		_ = _cache.Set(GetGenerationCacheKey(keyId), generation, _options.MetadataCacheDuration);

		return generation;
	}

	/// <summary>
	/// Reads the recorded identifier of the material at a handle, or <see langword="null"/> when none is recorded.
	/// </summary>
	private async Task<string?> ReadKeyGenerationAsync(string keyId, CancellationToken cancellationToken)
	{
		var cacheKey = GetGenerationCacheKey(keyId);
		if (_cache.TryGetValue(cacheKey, out string? cached))
		{
			return cached;
		}

		cancellationToken.ThrowIfCancellationRequested();

		string? generation = null;
		try
		{
			var marker = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
				GetGenerationMarkerPath(keyId),
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

			if (marker?.Data?.Data is { } data && data.TryGetValue("generation", out var value))
			{
				generation = value?.ToString();
			}
		}
		catch (VaultSharp.Core.VaultApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsMountMissing(ex))
		{
			// Absent marker on a mounted engine means this handle has no recorded identity. A mount-missing 404
			// is excluded so it propagates: an unreachable mount must never be read as "no identity", or a read
			// would refuse a payload whose key is perfectly intact.
		}

		_ = _cache.Set(cacheKey, generation, _options.MetadataCacheDuration);

		return generation;
	}

	/// <summary>
	/// Persists, preserves, or removes a key's purpose so purpose-scoped resolution can find it later.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="purpose">
	/// The purpose to record; <see langword="null"/> to leave any recorded purpose untouched; an empty or
	/// whitespace string to remove it.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// The three cases are distinct on purpose. "Not supplied" and "supplied as empty" are different
	/// intentions, and collapsing them leaves a purpose that can be set and never cleared: a rotation that
	/// omits the purpose must not erase one recorded earlier, but a caller who explicitly asks for no
	/// purpose must be able to get it removed.
	/// </remarks>
	private async Task WriteKeyPurposeAsync(string keyId, string? purpose, CancellationToken cancellationToken)
	{
		if (purpose is null)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(purpose))
		{
			cancellationToken.ThrowIfCancellationRequested();

			// DeleteMetadataAsync removes the document and all of its versions, so a purpose-scoped
			// lookup cannot read a prior version back. Deleting an absent document is idempotent
			// (Vault answers 204), so clearing a key that never had a purpose is a safe no-op.
			await _vaultClient.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(
				GetPurposeMarkerPath(keyId),
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

			_cache.Remove(GetPurposeCacheKey(keyId));

			return;
		}

		cancellationToken.ThrowIfCancellationRequested();

		var marker = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["purpose"] = purpose,
			["recordedAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
		};

		_ = await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
			GetPurposeMarkerPath(keyId),
			marker,
			mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

		_ = _cache.Set(GetPurposeCacheKey(keyId), purpose, _options.MetadataCacheDuration);
	}

	/// <summary>
	/// Reads a key's recorded purpose, or <see langword="null"/> when it has none.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The recorded purpose, or <see langword="null"/>.</returns>
	private async Task<string?> ReadKeyPurposeAsync(string keyId, CancellationToken cancellationToken)
	{
		var cacheKey = GetPurposeCacheKey(keyId);
		if (_cache.TryGetValue(cacheKey, out string? cachedPurpose))
		{
			return cachedPurpose;
		}

		// VaultSharp KV calls take no CancellationToken; observe it before the read, mirroring
		// IsKeySuspendedAsync.
		cancellationToken.ThrowIfCancellationRequested();

		string? purpose = null;
		try
		{
			var marker = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
				GetPurposeMarkerPath(keyId),
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);

			if (marker?.Data?.Data is { } data
				&& data.TryGetValue("purpose", out var value))
			{
				purpose = value?.ToString();
			}
		}
		catch (VaultSharp.Core.VaultApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsMountMissing(ex))
		{
			// Absent marker on a mounted engine = the key simply has no purpose. A mount-missing 404 is
			// excluded so it propagates, matching IsKeySuspendedAsync: purpose absence must never be
			// silently manufactured from an unreachable mount, or a purpose-scoped lookup would quietly
			// return nothing instead of failing.
			purpose = null;
		}

		_ = _cache.Set(cacheKey, purpose, _options.MetadataCacheDuration);
		return purpose;
	}

	private static string GetSuspensionCacheKey(string keyId) => $"suspended:{keyId}";

	/// <summary>
	/// Returns whether the key carries a durable suspension marker in Vault KV. The result is cached for
	/// <see cref="VaultOptions.MetadataCacheDuration"/> and invalidated on suspend; a missing marker means
	/// "not suspended", while a genuine Vault error propagates (an error is never silently treated as
	/// "not suspended").
	/// </summary>
	private async Task<bool> IsKeySuspendedAsync(string keyId, CancellationToken cancellationToken)
	{
		var cacheKey = GetSuspensionCacheKey(keyId);
		if (_cache.TryGetValue(cacheKey, out bool cachedSuspended))
		{
			return cachedSuspended;
		}

		// VaultSharp KV calls take no CancellationToken; observe it before the read (mirrors the
		// reachability probe) and honour it via the rate-limit semaphore so this KV read is subject to
		// the same throttling contract as every other Vault operation on this provider.
		cancellationToken.ThrowIfCancellationRequested();

		bool suspended;
		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var marker = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
				GetSuspensionMarkerPath(keyId),
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);
			suspended = marker?.Data?.Data is not null;
		}
		catch (VaultSharp.Core.VaultApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsMountMissing(ex))
		{
			// A 404 on a MOUNTED KV v2 engine means the marker is simply absent → not suspended. A
			// mount-missing 404 (engine unmounted or read ACL lost after startup) is EXCLUDED by the filter
			// so it propagates — fail-closed, mirroring ValidateSuspensionMountReachableAsync. Never treat an
			// unreachable suspension mount as "not suspended", which would hand back a suspended key as Active.
			suspended = false;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}

		_ = _cache.Set(cacheKey, suspended, _options.MetadataCacheDuration);
		return suspended;
	}

	/// <summary>
	/// Verifies, with a read-only probe, that the Vault KV&#160;v2 mount required to persist key-suspension
	/// markers is reachable, and throws when it is not. This is the fail-closed startup guard for the
	/// suspension availability coupling: suspension records/reads a durable marker in
	/// <see cref="VaultSuspensionOptions.MountPath"/>, so an absent or unreachable mount would let a suspended
	/// key appear active. The probe reads a well-known non-existent path within the mount: a "secret not found"
	/// response proves the mount is reachable (the healthy case), whereas a missing/unmounted engine, an auth
	/// or permission failure, or a connectivity failure all propagate — so anything short of a positively
	/// reachable mount fails closed. It NEVER creates the mount or writes any data.
	/// </summary>
	/// <param name="cancellationToken">A token to observe while probing.</param>
	/// <exception cref="ObjectDisposedException">Thrown when the provider has been disposed.</exception>
	/// <exception cref="VaultSharp.Core.VaultApiException">
	/// Propagated when the mount is missing/unmounted or access is denied.
	/// </exception>
	internal async Task ValidateSuspensionMountReachableAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

		var probePath = $"{_options.Suspension.Path}/__startup-availability-probe__";

		try
		{
			// A read of a non-existent secret on a MOUNTED KV v2 engine returns 404 with empty errors; the
			// filtered catch below treats that as "reachable". A read against a MISSING mount returns 404 whose
			// body carries "no handler for route ..." — excluded by the filter, so it propagates (fail-closed).
			_ = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
				probePath,
				mountPoint: _options.Suspension.MountPath).ConfigureAwait(false);
		}
		catch (VaultSharp.Core.VaultApiException ex)
			when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsMountMissing(ex))
		{
			// Mount reachable, probe secret simply absent — the expected healthy outcome.
		}

		LogSuspensionMountReachable(_options.Suspension.MountPath);
	}

	// A missing/unmounted secrets engine surfaces as a 404 whose Vault error body names an unhandled route,
	// distinct from a 404 for a merely-absent secret on a mounted engine. Match those markers so a missing
	// mount is never mistaken for a healthy one (fail-closed on ambiguity — anything unrecognized propagates).
	private static bool IsMountMissing(VaultSharp.Core.VaultApiException ex) =>
		ex.Message.Contains("no handler for route", StringComparison.OrdinalIgnoreCase) ||
		ex.Message.Contains("unsupported path", StringComparison.OrdinalIgnoreCase) ||
		ex.Message.Contains("preflight capability check", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Surfaces a durably-suspended key as <see cref="KeyStatus.Suspended"/> regardless of its
	/// Transit-derived status, so the framework encryption provider (<c>AesGcmEncryptionProvider</c>) refuses
	/// it for both encrypt and decrypt. Applied AFTER (possibly cached) retrieval and gated on the separately-invalidated
	/// suspension cache, so a stale metadata cache cannot resurrect a suspended key as usable.
	/// </summary>
	private async Task<KeyMetadata?> ApplySuspensionStatusAsync(
		string keyId,
		KeyMetadata? metadata,
		CancellationToken cancellationToken)
	{
		if (metadata is null)
		{
			return null;
		}

		return await IsKeySuspendedAsync(keyId, cancellationToken).ConfigureAwait(false)
			? metadata with { Status = KeyStatus.Suspended }
			: metadata;
	}

	private static bool IsKeyNotFoundException(VaultSharp.Core.VaultApiException ex) =>
		(ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsMountMissing(ex)) ||
		ex.Message.Contains("no existing key named", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Reads the creation instant Transit reports for one specific key version.
	/// </summary>
	/// <param name="keyId"> The key handle, for the failure message. </param>
	/// <param name="keyInfo"> The Transit key response. </param>
	/// <param name="version"> The version whose creation instant is wanted. </param>
	/// <returns> The instant Transit reported for that version. </returns>
	/// <remarks>
	/// <para>
	/// REPORTS THE ABSENCE RATHER THAN SUBSTITUTING THE LOCAL CLOCK. A fabricated instant is
	/// indistinguishable from a measured one to every caller, and it is wrong in the unsafe direction: "now"
	/// makes the oldest version look like the newest, so an ordering built on it inverts rather than degrades.
	/// </para>
	/// <para>
	/// It does NOT refuse. Real Transit does not date every version it holds, and this is reached from
	/// ordinary metadata reads -- so refusing here fails <c>GetKeyAsync</c> for any key Vault has not dated,
	/// which breaks the normal path to protect an ordering almost nobody performs. The absence travels to the
	/// caller instead, where <see cref="KeyMetadata.CreatedAt"/> documents that unknown counts as stale.
	/// </para>
	/// </remarks>
	private static DateTimeOffset? ReadVersionCreationTime(string keyId, EncryptionKeyInfo keyInfo, int version)
	{
		var versionKey = version.ToString(CultureInfo.InvariantCulture);

		if (keyInfo.Keys is not null
			&& keyInfo.Keys.TryGetValue(versionKey, out var versionEntry)
			&& versionEntry is Dictionary<string, object> versionDict
			&& versionDict.TryGetValue("creation_time", out var creationTime))
		{
			if (creationTime is DateTimeOffset reported)
			{
				return reported;
			}

			if (DateTimeOffset.TryParse(
					creationTime?.ToString(),
					CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
					out var parsed))
			{
				return parsed;
			}
		}

		// Not known. Not invented, and not an error: Transit does not date every version, and a read of such
		// a key must still succeed.
		return null;
	}

	private KeyMetadata MapToKeyMetadata(string keyId, EncryptionKeyInfo keyInfo, int? overrideVersion = null)
	{
		var version = overrideVersion ?? keyInfo.LatestVersion;

		// Status is per VERSION, not per key. This previously computed it from the key alone and ignored the
		// version argument entirely, which made DecryptOnly unreachable however the key was configured: a
		// caller asking about version 1 of a rotated key was told about the key's newest version instead.
		var status = DetermineKeyStatus(keyInfo, version);

		// Determine algorithm from key type - use Type property (TransitKeyType enum)
		var algorithm = keyInfo.Type switch
		{
			TransitKeyType.aes256_gcm96 => DispatchEncryptionAlgorithm.Aes256Gcm,
			_ => DispatchEncryptionAlgorithm.Aes256Gcm
		};

		// The creation time of THE VERSION BEING DESCRIBED, which is the same correction the status above
		// already carries. Transit reports a creation time per version where it reports one at all, and reading
		// version 1's for every version made the field report one instant for a whole handle -- so it could not
		// order versions, and any caller comparing two versions of a handle by it got equality. Null where
		// Transit dates nothing; see KeyMetadata.CreatedAt for what a caller owes an unknown instant.
		var createdAt = ReadVersionCreationTime(keyId, keyInfo, version);

		return new KeyMetadata
		{
			KeyId = keyId,
			Version = version,
			Status = status,
			Algorithm = algorithm,
			CreatedAt = createdAt,
			// Vault Transit doesn't have built-in expiration
			ExpiresAt = null,
			// Would need to track separately
			LastRotatedAt = null,
			// Could be stored in metadata if Vault supports custom metadata
			Purpose = null,
			// Vault can be deployed in FIPS mode
			IsFipsCompliant = true
		};
	}
}
