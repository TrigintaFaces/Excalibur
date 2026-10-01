// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchEncryptionAlgorithm = Excalibur.Compliance.EncryptionAlgorithm;

namespace Excalibur.Compliance.Azure;

/// <summary>
/// Azure Key Vault implementation of <see cref="IKeyManagementProvider" /> and <see cref="IKeyManagementAdmin" />.
/// </summary>
/// <remarks>
/// <para> This provider integrates with Azure Key Vault for enterprise-grade key management:
/// <list type="bullet">
/// <item> HSM-backed keys with FIPS 140-2 Level 2 validation (Premium tier) </item>
/// <item> Automatic key rotation support </item>
/// <item> Multi-region disaster recovery </item>
/// <item> RBAC-based access control </item>
/// </list>
/// </para>
/// <para>
/// <strong> Important: </strong> This provider performs server-side cryptographic operations. Key material never leaves Azure Key Vault,
/// providing maximum security.
/// </para>
/// <para>
/// <strong>Key versions are DERIVED here, not observed, and the difference is observable.</strong> Azure Key
/// Vault versions are opaque identifiers with no ordinal — the service exposes no "version 3" — while
/// <see cref="KeyMetadata.Version"/> is an integer, so this provider derives one from each opaque identifier.
/// The derivation is deterministic and stable across processes, but it is NOT injective: two versions of one
/// key can derive the same number. When that happens <see cref="GetKeyVersionAsync"/> RAISES rather than
/// resolving one of the candidates, because returning one would describe a version the caller did not ask
/// for, and the number forms part of the data that authenticates an encrypted value — so the mismatch would
/// surface later as a failed authentication tag on a value whose key is healthy.
/// </para>
/// <para>
/// <strong>Deleting a key here removes every version of it.</strong> Azure Key Vault has no per-version
/// delete, so destruction is whole-key and a question about one version's destruction is answered by the
/// state of the key. That is a property of the service, not a simplification by this provider.
/// </para>
/// </remarks>
public sealed partial class AzureKeyVaultProvider : IKeyManagementProvider, IDurableKeyProvider, IKeyManagementAdmin, IKeyDestructionStatusProvider, IDisposable
{
	private readonly KeyClient _keyClient;
	private readonly ConcurrentDictionary<string, CryptographyClient> _cryptoClients = new();
	private readonly IMemoryCache _cache;
	private readonly ILogger<AzureKeyVaultProvider> _logger;
	private readonly AzureKeyVaultOptions _options;
	private readonly SemaphoreSlim _rateLimitSemaphore = new(10, 10); // Limit concurrent operations
	private volatile bool _disposed;

	/// <summary>
	/// The tag carrying the identifier of the material LINEAGE at a key name.
	/// </summary>
	/// <remarks>
	/// A named constant rather than an inline literal, unlike the other tags in this file, because a
	/// misspelling here does not fail: the read simply finds nothing, the generation reads as absent, and a
	/// crypto-shredded field becomes unreadable rather than throwing anywhere a test would see.
	/// </remarks>
	private const string GenerationTag = "excalibur:generation";

	/// <summary>
	/// Mints an identifier for a newly provisioned material lineage, as the tag value that carries it.
	/// </summary>
	/// <remarks>
	/// The CSPRNG lives in <see cref="KeyGeneration.Mint"/> rather than here, so every provider mints the same
	/// way and none can drift to a weaker source. This only renders it for the tag.
	/// </remarks>
	private static string MintGeneration() => KeyGeneration.Mint().ToString();

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultProvider" /> class.
	/// </summary>
	/// <param name="options"> The Azure Key Vault configuration options. </param>
	/// <param name="cache"> The memory cache for caching key metadata. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	/// <exception cref="ArgumentNullException"> Thrown when options, cache, or logger is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when VaultUri is not configured. </exception>
	public AzureKeyVaultProvider(
		IOptions<AzureKeyVaultOptions> options,
		IMemoryCache cache,
		ILogger<AzureKeyVaultProvider> logger)
		: this(options, cache, logger, keyClient: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultProvider" /> class over a caller-supplied
	/// <see cref="KeyClient" />.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this when the vault client needs configuration this provider does not model -- a custom retry or
	/// transport policy, a proxy, a sovereign or air-gapped cloud, or a credential assembled elsewhere. Register
	/// a <see cref="KeyClient" /> in the container and the registration extension passes it here; otherwise this
	/// provider builds one from <see cref="AzureKeyVaultOptions" /> as before.
	/// </para>
	/// <para>
	/// It is also the seam tests substitute the vault through. Before it existed the only way in was to reflect
	/// on the private field holding the client, which is not a seam a consumer can use and which left this
	/// provider's own behaviour unreachable from a unit test -- that is how a silent defect on the field-decrypt
	/// path came to ship with no arm covering it.
	/// </para>
	/// </remarks>
	/// <param name="options"> The Azure Key Vault configuration options. </param>
	/// <param name="cache"> The memory cache for caching key metadata. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	/// <param name="keyClient">
	/// The vault client to use, or <see langword="null" /> to build one from <paramref name="options" />.
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown when options, cache, or logger is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when VaultUri is not configured. </exception>
	public AzureKeyVaultProvider(
		IOptions<AzureKeyVaultOptions> options,
		IMemoryCache cache,
		ILogger<AzureKeyVaultProvider> logger,
		KeyClient? keyClient)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_cache = cache;
		_logger = logger;

		// Required even when a client is supplied: the vault URI identifies this provider in its diagnostics, and
		// a provider that cannot say which vault it is talking to is not a provider a consumer can audit.
		if (_options.VaultUri is null)
		{
			throw new ArgumentException(Resources.AzureKeyVaultProvider_VaultUriRequired, nameof(options));
		}

		_keyClient = keyClient ?? new KeyClient(_options.VaultUri, _options.Credential ?? new DefaultAzureCredential());

		LogProviderInitialized(_options.VaultUri);
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = GetCacheKey(keyId);
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			return cached;
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);

			var metadata = MapToKeyMetadata(keyId, response.Value);
			CacheMetadata(cacheKey, metadata);

			return metadata;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
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
			return cached;
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			// Azure Key Vault versions are OPAQUE STRINGS with no ordinal, so an integer version is matched by
			// mapping each of the key's versions through the same derivation. That mapping is not injective,
			// and this loop must not resolve an ordinal that more than one version maps to.
			//
			// EVERY match is collected rather than returning the first, because returning the first silently
			// answers about a DIFFERENT version than the caller asked for: the metadata would carry the
			// requested ordinal while naming another version's material. A decrypt then fails its
			// authentication tag with nothing saying why, for a value whose key is present and healthy.
			string? resolvedVersion = null;
			List<string>? ambiguousVersions = null;

			await foreach (var keyProperties in _keyClient.GetPropertiesOfKeyVersionsAsync(keyName, cancellationToken))
			{
				if (ExtractVersionNumber(keyProperties) != version)
				{
					continue;
				}

				if (resolvedVersion is null)
				{
					resolvedVersion = keyProperties.Version;
					continue;
				}

				(ambiguousVersions ??= [resolvedVersion]).Add(keyProperties.Version);
			}

			if (ambiguousVersions is not null)
			{
				// Refused rather than guessed. The caller asked about one version and this provider cannot say
				// which of these it meant, so it says that instead of choosing.
				throw new EncryptionException(
					$"Key '{keyId}' has {ambiguousVersions.Count} Azure Key Vault versions that map to version "
					+ $"number {version}, so the version cannot be resolved unambiguously. Azure Key Vault "
					+ "versions are opaque identifiers with no ordinal, and this provider derives an integer "
					+ "from each; the derivation is not injective. Resolving one of them would answer about a "
					+ "version the caller did not ask for. Ambiguous versions: "
					+ string.Join(", ", ambiguousVersions))
				{
					ErrorCode = EncryptionErrorCode.Unknown
				};
			}

			if (resolvedVersion is null)
			{
				LogKeyVersionNotFound(keyId, version);
				return null;
			}

			var response = await _keyClient.GetKeyAsync(keyName, resolvedVersion, cancellationToken).ConfigureAwait(false);
			var metadata = MapToKeyMetadata(keyId, response.Value, version);
			CacheMetadata(cacheKey, metadata);
			return metadata;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
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
			await foreach (var keyProperties in _keyClient.GetPropertiesOfKeysAsync(cancellationToken))
			{
				// Only include keys with our prefix
				if (!keyProperties.Name.StartsWith(_options.KeyNamePrefix, StringComparison.Ordinal))
				{
					continue;
				}

				var keyId = keyProperties.Name[_options.KeyNamePrefix.Length..];

				// Get full key details
				try
				{
					var response = await _keyClient.GetKeyAsync(keyProperties.Name, cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					var metadata = MapToKeyMetadata(keyId, response.Value);

					// Apply filters
					if (status.HasValue && metadata.Status != status.Value)
					{
						continue;
					}

					if (purpose is not null && metadata.Purpose != purpose)
					{
						continue;
					}

					results.Add(metadata);
				}
				catch (RequestFailedException ex) when (ex.Status == 404)
				{
					// Key was deleted between listing and getting details
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
			KeyVaultKey? existingKey = null;
			try
			{
				var existingResponse = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);
				existingKey = existingResponse.Value;
				previousKeyMetadata = MapToKeyMetadata(keyId, existingKey);
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				// Key doesn't exist, will create new
			}

			// Create new key version
			var keyType = _options.UseSoftwareKeys ? KeyType.Rsa : KeyType.RsaHsm;
			if (algorithm == DispatchEncryptionAlgorithm.Aes256Gcm)
			{
				// For AES operations, we use RSA keys to wrap/unwrap symmetric keys Azure Key Vault doesn't directly support AES key storage
				keyType = _options.UseSoftwareKeys ? KeyType.Rsa : KeyType.RsaHsm;
			}

			var createOptions = new CreateRsaKeyOptions(keyName, hardwareProtected: !_options.UseSoftwareKeys)
			{
				KeySize = 2048, // RSA key size for wrapping
				ExpiresOn = expiresAt,
				Enabled = true
			};

			// Set tags for metadata
			createOptions.Tags["excalibur:purpose"] = purpose ?? "general";
			createOptions.Tags["excalibur:algorithm"] = algorithm.ToString();
			createOptions.Tags["excalibur:created"] = DateTimeOffset.UtcNow.ToString("O");
			createOptions.Tags[GenerationTag] = MintGeneration();

			if (existingKey is not null)
			{
				// THE LINEAGE IDENTITY MUST SURVIVE THE ROTATION, and it is written explicitly rather than
				// relied upon. A rotation extends the material lineage, so every envelope already written under
				// this handle keeps naming the same generation; losing it here would strand all of them.
				//
				// Key Vault tags live on the VERSION, and this code does not depend on whether the service-side
				// rotate copies them to the new one: the tag is read from the version that exists now and
				// written to the version that results, so the carry-forward holds either way. An absent tag is
				// an older key provisioned before the lineage identifier existed; it is minted now, which is
				// honest -- that handle has no recorded identity to preserve.
				var carriedGeneration =
					existingKey.Properties.Tags.TryGetValue(GenerationTag, out var priorGeneration)
					&& !string.IsNullOrEmpty(priorGeneration)
						? priorGeneration
						: MintGeneration();

				// Rotate existing key by creating new version
				var rotateResponse = await _keyClient.RotateKeyAsync(keyName, cancellationToken).ConfigureAwait(false);

				var rotatedProperties = rotateResponse.Value.Properties;
				_ = rotatedProperties.Tags.TryGetValue(GenerationTag, out var rotatedGeneration);
				if (!string.Equals(rotatedGeneration, carriedGeneration, StringComparison.Ordinal))
				{
					rotatedProperties.Tags[GenerationTag] = carriedGeneration;
					_ = await _keyClient.UpdateKeyPropertiesAsync(rotatedProperties, cancellationToken: cancellationToken)
						.ConfigureAwait(false);
				}

				var newMetadata = MapToKeyMetadata(keyId, rotateResponse.Value);

				// Invalidate cache
				InvalidateCache(keyId);

				LogKeyRotated(keyId, rotateResponse.Value.Properties.Version);

				return KeyRotationResult.Succeeded(newMetadata, previousKeyMetadata);
			}
			else
			{
				// Create new key
				var createResponse = await _keyClient.CreateRsaKeyAsync(createOptions, cancellationToken).ConfigureAwait(false);
				var newMetadata = MapToKeyMetadata(keyId, createResponse.Value);

				LogKeyCreated(keyId);

				return KeyRotationResult.Succeeded(newMetadata);
			}
		}
		catch (RequestFailedException ex)
		{
			LogKeyRotationFailed(ex, keyId);
			return KeyRotationResult.Failed($"Azure Key Vault error: {ex.Message}");
		}
		catch (Exception ex)
		{
			LogKeyRotationUnexpectedError(ex, keyId);
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
	/// <b>Key Vault cannot do this atomically, and that is a property of the backend rather than of this
	/// provider.</b> Creating a key and adding a version to one are the SAME request there -- the create
	/// endpoint adds a version when the name is taken, and it has no conditional form, no
	/// <c>If-None-Match</c> and no create-only flag. So this reads, then creates, and two genuinely
	/// concurrent callers that both read "absent" can both create, leaving the key with two versions.
	/// </para>
	/// <para>
	/// <b>What this does guarantee, on every backend, is the part that matters:</b> it never rotates and never
	/// demotes. Key Vault treats the newest version as the usable one and never marks a superseded version
	/// decrypt-only, so both versions of a raced create remain usable and no ciphertext is left naming a
	/// version the vault has fenced. The extra version costs a wasted key; it does not lose a write.
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

			// Re-read behind the gate. This does not make the operation atomic across processes -- nothing
			// available here can -- but it does stop two callers inside ONE process from both creating.
			try
			{
				var found = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken)
					.ConfigureAwait(false);

				if (found?.Value is not null)
				{
					return MapToKeyMetadata(keyId, found.Value);
				}
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				// Absent, as expected on this path.
			}

			var createOptions = new CreateRsaKeyOptions(keyName, hardwareProtected: !_options.UseSoftwareKeys)
			{
				KeySize = 2048,
				Enabled = true
			};

			createOptions.Tags["excalibur:purpose"] = purpose ?? "general";
			createOptions.Tags["excalibur:algorithm"] = algorithm.ToString();
			createOptions.Tags["excalibur:created"] = DateTimeOffset.UtcNow.ToString("O");

			// This path reaches here only when the handle holds nothing, so the material is new and so is its
			// lineage identity. A handle re-occupied after an erasure takes this path and MUST get a different
			// generation from the one that was destroyed, or a read of the erased subject's old ciphertext would
			// find the new key's generation in no ledger row -- or worse, find the destroyed one and tombstone
			// data that is live.
			createOptions.Tags[GenerationTag] = MintGeneration();

			var created = await _keyClient.CreateRsaKeyAsync(createOptions, cancellationToken).ConfigureAwait(false);

			LogKeyCreated(keyId);
			InvalidateCache(keyId);

			return MapToKeyMetadata(keyId, created.Value);
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Key Vault soft-deletes: a deleted key stays recoverable until the vault purges it at the end of its
	/// retention period. When <paramref name="retentionDays"/> is <c>0</c> -- immediate destruction, which is what
	/// an erasure asks for -- the deleted key is also PURGED, and only a purge the vault accepts is reported as
	/// <see cref="KeyDestructionState.Completed"/>. A vault with purge protection, or a credential without the
	/// purge permission, refuses; the key is then reported as
	/// <see cref="KeyDestructionState.ScheduledIrreversible"/> at the vault's scheduled purge date. Any failure of
	/// the purge falls back to that same outcome: it never reports a completion the vault did not perform, and it
	/// never turns a key that was deleted into an exception.
	/// </para>
	/// <para>
	/// A key that is already soft-deleted is NOT reported as <see cref="KeyDestructionState.NotFound"/>: it still
	/// exists and can still be recovered, so it is handled exactly as a key deleted by this call.
	/// </para>
	/// </remarks>
	public async Task<KeyDestructionOutcome> DeleteKeyAsync(string keyId, int retentionDays, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			DeletedKey? deleted;
			try
			{
				var operation = await _keyClient.StartDeleteKeyAsync(keyName, cancellationToken).ConfigureAwait(false);

				// Wait for deletion to complete
				deleted = (await operation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false)).Value;
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				// Not a live key -- but "not live" is not "did not exist". A key soft-deleted earlier (by a previous
				// attempt at this same erasure, for example) answers 404 here while it is still recoverable, and
				// reporting it as NotFound would let an erasure attest it as nothing-to-destroy.
				deleted = await GetDeletedKeyOrNullAsync(keyName, cancellationToken).ConfigureAwait(false);
				if (deleted is null)
				{
					LogKeyNotFoundForDeletion(keyId);
					return KeyDestructionOutcome.NotFound;
				}
			}

			// Invalidate cache
			InvalidateCache(keyId);

			// Remove crypto client
			_ = _cryptoClients.TryRemove(keyId, out _);

			if (retentionDays <= 0 && await TryPurgeDeletedKeyAsync(keyName, keyId, cancellationToken).ConfigureAwait(false))
			{
				return KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow);
			}

			LogKeyScheduledForDeletion(keyId);

			// Prefer the vault's disclosed ScheduledPurgeDate; fall back to the requested/90-day window if unavailable.
			var irreversibleAt = deleted?.ScheduledPurgeDate
				?? DateTimeOffset.UtcNow.AddDays(retentionDays > 0 ? retentionDays : 90);
			return KeyDestructionOutcome.ScheduledAt(irreversibleAt);
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Asks the vault directly, bypassing the metadata cache: a live key and a soft-deleted key are both
	/// reported as NOT destroyed, and only a key the vault holds neither live nor in its deleted-keys collection
	/// is reported as destroyed. The two collections cannot be read atomically, and a recovery moves a key from
	/// the deleted collection to the live one, so a single pass of "live, then deleted" could miss a key recovered
	/// between the reads. The deleted collection is therefore read on both sides of the live read: a key is
	/// destroyed only when it is absent from deleted, live and deleted again, in that order. A soft-deleted key is invisible to <see cref="GetKeyAsync"/>, which is why that
	/// lookup cannot answer this question for Key Vault. Reading the deleted-keys collection requires the
	/// <c>keys/get</c> permission; a refusal is thrown, never reported as destroyed.
	/// </remarks>
	public async Task<bool> IsKeyDestroyedAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			if (await GetDeletedKeyOrNullAsync(keyName, cancellationToken).ConfigureAwait(false) is not null)
			{
				return false;
			}

			try
			{
				_ = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);
				return false;
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				// Not live either -- unless it was deleted again, or is mid-recovery, since the first read.
			}

			return await GetDeletedKeyOrNullAsync(keyName, cancellationToken).ConfigureAwait(false) is null;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> SuspendKeyAsync(string keyId, string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			// Get current key
			var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);
			var key = response.Value;

			// Disable the key
			var properties = key.Properties;
			properties.Enabled = false;
			properties.Tags["excalibur:suspended"] = "true";
			properties.Tags["excalibur:suspension_reason"] = reason;
			properties.Tags["excalibur:suspended_at"] = DateTimeOffset.UtcNow.ToString("O");

			_ = await _keyClient.UpdateKeyPropertiesAsync(properties, cancellationToken: cancellationToken).ConfigureAwait(false);

			// Invalidate cache
			InvalidateCache(keyId);

			// Remove crypto client
			_ = _cryptoClients.TryRemove(keyId, out _);

			LogKeySuspended(keyId, reason);

			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogKeyNotFoundForSuspension(keyId);
			return false;
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
			var keyName = GetKeyName(keyId);

			// Get current key
			var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);
			var key = response.Value;

			// Inverse of SuspendKeyAsync: re-enable the key natively and clear the suspension tags.
			var properties = key.Properties;
			properties.Enabled = true;
			_ = properties.Tags.Remove("excalibur:suspended");
			_ = properties.Tags.Remove("excalibur:suspension_reason");
			_ = properties.Tags.Remove("excalibur:suspended_at");

			_ = await _keyClient.UpdateKeyPropertiesAsync(properties, cancellationToken: cancellationToken).ConfigureAwait(false);

			// Invalidate cache
			InvalidateCache(keyId);

			LogKeyReactivated(keyId);

			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogKeyNotFoundForReactivation(keyId);
			return false;
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
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
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

	/// <summary>
	/// Gets a cryptography client for performing operations with a specific key.
	/// </summary>
	/// <param name="keyId"> The key identifier. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A cryptography client for the specified key. </returns>
	public async Task<CryptographyClient> GetCryptographyClientAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		if (_cryptoClients.TryGetValue(keyId, out var existingClient))
		{
			return existingClient;
		}

		var keyName = GetKeyName(keyId);
		var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);

		var credential = _options.Credential ?? new DefaultAzureCredential();
		var client = new CryptographyClient(response.Value.Id, credential);

		_ = _cryptoClients.TryAdd(keyId, client);

		return client;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_rateLimitSemaphore.Dispose();
		_cryptoClients.Clear();
		_disposed = true;

		LogProviderDisposed();
	}

	/// <summary>
	/// Derives the integer version number this provider reports for an Azure Key Vault key version.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is a DERIVED value, not a measurement, and it is load-bearing.</b> Azure Key Vault versions are
	/// opaque identifiers with no ordinal — the service exposes no "version 3" — while the framework's key
	/// metadata carries an <see cref="int"/>. So a number has to come from somewhere, and this is where.
	/// </para>
	/// <para>
	/// <b>It cannot be changed compatibly, which is why it is still here rather than deleted.</b> The number
	/// this returns is written onto every envelope encrypted under an Azure-backed key, is the value a decrypt
	/// uses to resolve the key again, and is BOUND INTO THE AES-GCM ASSOCIATED DATA. Changing the derivation —
	/// to an ordering-based index, a wider hash, or a constant — makes every value already encrypted under an
	/// Azure key undecryptable twice over: the lookup no longer finds the version, and even if it did the
	/// associated data would differ and the authentication tag would not verify. A migration would have to
	/// re-encrypt, and that is a disposition decision rather than a code change.
	/// </para>
	/// <para>
	/// <b>It is NOT injective, and the caller is protected rather than the collision hidden.</b> The codomain
	/// is 100000 buckets, so two of a key's versions can derive the same number. Version lookup refuses an
	/// ambiguous number instead of resolving one of the candidates, so a collision is reported rather than
	/// answered about the wrong version. That refusal is the guard; this function is deliberately unchanged.
	/// </para>
	/// </remarks>
	private static int ExtractVersionNumber(KeyProperties properties)
	{
		if (string.IsNullOrEmpty(properties.Version))
		{
			return 1;
		}

		// Use a DETERMINISTIC hash (FNV-1a), not String.GetHashCode: since .NET Core, String.GetHashCode is
		// randomized per process, so the same Key Vault version mapped to different integers across processes —
		// GetKeyVersionAsync(keyId, version) could then fail to find a version another process returned.
		return (int)((StableHash(properties.Version) % 100000) + 1);
	}

	/// <summary>
	/// Computes a deterministic, process-stable 32-bit FNV-1a hash of a string (ordinal). Unlike
	/// <see cref="string.GetHashCode()"/>, the result is identical across runtimes and processes, so it is
	/// safe to use as a stable version key.
	/// </summary>
	private static uint StableHash(string value)
	{
		const uint OffsetBasis = 2166136261;
		const uint Prime = 16777619;

		var hash = OffsetBasis;
		foreach (var c in value)
		{
			hash = (hash ^ (byte)(c & 0xFF)) * Prime;
			hash = (hash ^ (byte)(c >> 8)) * Prime;
		}

		return hash;
	}

	private static KeyStatus DetermineKeyStatus(KeyProperties properties)
	{
		if (properties.Tags.TryGetValue("excalibur:suspended", out var suspended) && suspended == "true")
		{
			return KeyStatus.Suspended;
		}

		if (properties.Enabled != true)
		{
			return KeyStatus.Suspended;
		}

		if (properties.ExpiresOn.HasValue && properties.ExpiresOn.Value <= DateTimeOffset.UtcNow)
		{
			return KeyStatus.DecryptOnly;
		}

		// Check if there's a newer version (this would make current version decrypt-only) For simplicity, we assume the latest version is
		// always active
		return KeyStatus.Active;
	}

	private string GetKeyName(string keyId) => $"{_options.KeyNamePrefix}{keyId}";

	private async Task<DeletedKey?> GetDeletedKeyOrNullAsync(string keyName, CancellationToken cancellationToken)
	{
		try
		{
			return (await _keyClient.GetDeletedKeyAsync(keyName, cancellationToken).ConfigureAwait(false)).Value;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return null;
		}
	}

	/// <summary>
	/// Purges a soft-deleted key. Returns <see langword="true"/> only when the vault accepted the purge. Every
	/// refusal or failure -- purge protection, a missing purge permission, anything else -- returns
	/// <see langword="false"/>, so the caller reports the key as scheduled rather than destroyed.
	/// </summary>
	private async Task<bool> TryPurgeDeletedKeyAsync(string keyName, string keyId, CancellationToken cancellationToken)
	{
		try
		{
			_ = await _keyClient.PurgeDeletedKeyAsync(keyName, cancellationToken).ConfigureAwait(false);
			LogKeyPurged(keyId);
			return true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
#pragma warning disable CA1031 // Any purge failure must degrade to "scheduled", never to "destroyed" or to a thrown erasure.
		catch (Exception ex)
#pragma warning restore CA1031
		{
			LogKeyPurgeRefused(keyId, ex);
			return false;
		}
	}

	private string GetCacheKey(string keyId, int? version = null) =>
		version.HasValue ? $"akv:{keyId}:v{version}" : $"akv:{keyId}:latest";

	private void CacheMetadata(string cacheKey, KeyMetadata metadata) =>
		_cache.Set(cacheKey, metadata, _options.MetadataCacheDuration);

	private void InvalidateCache(string keyId)
	{
		// Remove all cached versions for this key IMemoryCache doesn't support pattern removal, so we rely on expiration
		_cache.Remove(GetCacheKey(keyId));
		_cache.Remove($"active:default");
	}

	private KeyMetadata MapToKeyMetadata(string keyId, KeyVaultKey key, int? overrideVersion = null)
	{
		var status = DetermineKeyStatus(key.Properties);
		var version = overrideVersion ?? ExtractVersionNumber(key.Properties);

		// Extract purpose from tags
		string? purpose = null;
		if (key.Properties.Tags.TryGetValue("excalibur:purpose", out var purposeTag))
		{
			purpose = purposeTag;
		}

		// Determine algorithm from tags or key type
		var algorithm = DispatchEncryptionAlgorithm.Aes256Gcm;
		if (key.Properties.Tags.TryGetValue("excalibur:algorithm", out var algoTag) &&
			Enum.TryParse<DispatchEncryptionAlgorithm>(algoTag, out var parsedAlgo))
		{
			algorithm = parsedAlgo;
		}

		// Check for FIPS compliance (HSM-backed keys are FIPS compliant)
		var isFipsCompliant = key.KeyType == KeyType.RsaHsm ||
							  key.KeyType == KeyType.EcHsm ||
							  key.KeyType == KeyType.OctHsm;

		// Log warning for Standard tier in production
		if (_options.WarnOnStandardTierInProduction && !isFipsCompliant)
		{
			LogStandardTierWarning(keyId);
		}

		return new KeyMetadata
		{
			KeyId = keyId,
			Version = version,

			// THE GENERATION IS OURS, AND IT IDENTIFIES THE LINEAGE RATHER THAN THE VERSION.
			//
			// Key Vault's opaque version id was used here, and it is correct on ONE axis and wrong on the
			// other. Correct: a delete takes the key with every version it holds, and a key created again at
			// the same name gets entirely new opaque versions, so a version id from before an erasure never
			// names material that exists afterwards. Wrong: a ROTATION also mints a new opaque version, so the
			// identifier moved WITHIN one lineage -- and an erasure records the generation it finds now, which
			// is the post-rotation one. Every envelope written before that rotation then names a generation no
			// ledger row will ever hold, and its read fails permanently with no repair available, because the
			// material is gone and the identifier that would have recorded it is unreadable.
			//
			// So the value is minted once per lineage and carried across rotations. An absent tag is reported
			// as absent rather than invented: a key this provider did not provision has no lineage identity we
			// can honestly state, and the read path refuses such an envelope instead of tombstoning it.
			// PARSED, never trusted as-is. A tag is consumer-writable, so a hand-edited or legacy value that is
			// not a generation must read as ABSENT rather than becoming one: the ledger keys on this value, and
			// a value nothing minted could collide with another subject's.
			Generation = key.Properties.Tags.TryGetValue(GenerationTag, out var generationTag)
				&& KeyGeneration.TryParse(generationTag, out var parsedGeneration)
					? parsedGeneration
					: null,

			Status = status,
			Algorithm = algorithm,
			// REPORTED, NOT FABRICATED. Substituting the local clock makes a key of unknown age sort as the
			// newest, and the active-key resolution below orders by this field -- so an invented instant does
			// not degrade the choice, it inverts it. The absence is passed through instead, and the ordering
			// sorts an undated key oldest.
			CreatedAt = key.Properties.CreatedOn,
			ExpiresAt = key.Properties.ExpiresOn,
			LastRotatedAt = key.Properties.UpdatedOn,
			Purpose = purpose,
			IsFipsCompliant = isFipsCompliant
		};
	}

	[LoggerMessage(AzureKeyVaultEventId.ProviderInitialized, LogLevel.Information,
		"AzureKeyVaultProvider initialized for vault {VaultUri}")]
	private partial void LogProviderInitialized(Uri vaultUri);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFound, LogLevel.Debug,
		"Key {KeyId} not found in Azure Key Vault")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyVersionNotFound, LogLevel.Debug,
		"Key {KeyId} version {Version} not found")]
	private partial void LogKeyVersionNotFound(string keyId, int version);

	[LoggerMessage(AzureKeyVaultEventId.KeyRotated, LogLevel.Information,
		"Rotated Azure Key Vault key {KeyId} to version {Version}")]
	private partial void LogKeyRotated(string keyId, string? version);

	[LoggerMessage(AzureKeyVaultEventId.KeyCreated, LogLevel.Information,
		"Created new Azure Key Vault key {KeyId}")]
	private partial void LogKeyCreated(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyRotationFailed, LogLevel.Error,
		"Failed to rotate key {KeyId} in Azure Key Vault")]
	private partial void LogKeyRotationFailed(Exception exception, string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyRotationUnexpectedError, LogLevel.Error,
		"Unexpected error rotating key {KeyId}")]
	private partial void LogKeyRotationUnexpectedError(Exception exception, string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyScheduledForDeletion, LogLevel.Warning,
		"Scheduled Azure Key Vault key {KeyId} for deletion (crypto-shredding). Recoverable during soft-delete period.")]
	private partial void LogKeyScheduledForDeletion(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyPurged, LogLevel.Warning,
		"Purged Azure Key Vault key {KeyId}; its material is irrecoverable")]
	private partial void LogKeyPurged(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyPurgeRefused, LogLevel.Warning,
		"Azure Key Vault did not purge deleted key {KeyId} (purge protection, or no purge permission); it stays recoverable until the vault purges it")]
	private partial void LogKeyPurgeRefused(string keyId, Exception exception);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFoundForDeletion, LogLevel.Warning,
		"Key {KeyId} not found for deletion")]
	private partial void LogKeyNotFoundForDeletion(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeySuspended, LogLevel.Warning,
		"Suspended Azure Key Vault key {KeyId}: {Reason}")]
	private partial void LogKeySuspended(string keyId, string reason);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFoundForSuspension, LogLevel.Warning,
		"Key {KeyId} not found for suspension")]
	private partial void LogKeyNotFoundForSuspension(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyReactivated, LogLevel.Information,
		"Reactivated Azure Key Vault key {KeyId}")]
	private partial void LogKeyReactivated(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFoundForReactivation, LogLevel.Warning,
		"Key {KeyId} not found for reactivation")]
	private partial void LogKeyNotFoundForReactivation(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.ProviderDisposed, LogLevel.Debug,
		"AzureKeyVaultProvider disposed")]
	private partial void LogProviderDisposed();

	[LoggerMessage(AzureKeyVaultEventId.StandardTierWarning, LogLevel.Warning,
		"Key {KeyId} is using software-protected keys (Standard tier). Consider using HSM-backed keys (Premium tier) for production compliance workloads.")]
	private partial void LogStandardTierWarning(string keyId);
}
