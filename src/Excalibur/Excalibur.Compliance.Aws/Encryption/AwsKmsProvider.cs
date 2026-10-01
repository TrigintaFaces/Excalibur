// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AwsKeyMetadata = Amazon.KeyManagementService.Model.KeyMetadata;
using DispatchKeyMetadata = Excalibur.Compliance.KeyMetadata;

namespace Excalibur.Compliance.Aws;

/// <summary>
/// AWS KMS implementation of <see cref="IKeyManagementProvider"/> and <see cref="IKeyManagementAdmin"/>.
/// </summary>
/// <remarks>
/// <para>
/// This provider uses AWS Key Management Service for key lifecycle management.
/// Key material never leaves AWS KMS - only data keys or encrypted data are returned.
/// </para>
/// <para>
/// Features:
/// <list type="bullet">
/// <item>Key alias management with configurable prefix</item>
/// <item>Multi-region key support for disaster recovery</item>
/// <item>FIPS 140-2 endpoint support</item>
/// <item>Metadata caching to reduce API costs</item>
/// <item>Automatic key rotation via AWS KMS</item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification =
		"Deliberate, named trade rather than an oversight. This type sits at 97 against a threshold of 96, "
		+ "and the change that crossed it fixes data loss: rotation used to disable the superseded key, "
		+ "which AWS documents as unusable for every cryptographic operation including Decrypt, so all "
		+ "ciphertext under the previous key became unreadable. Reading version, purpose and superseded "
		+ "state back from KMS costs a few more SDK types. The threshold is a smell and the defect was "
		+ "real, so the defect wins and the coupling becomes tracked debt. The genuine remedy is splitting "
		+ "this class along the four interfaces it implements, which is a refactor rather than a one-line "
		+ "change and is tracked separately; the tag vocabulary has already been extracted to AwsKmsKeyTags "
		+ "as the first step.")]
public sealed partial class AwsKmsProvider : IKeyManagementProvider, IDurableKeyProvider, IKeyManagementAdmin, IKeyDestructionStatusProvider, IDisposable
{
	private readonly IAmazonKeyManagementService _kmsClient;
	private readonly IMemoryCache _metadataCache;
	private readonly AwsKmsOptions _options;
	private readonly ILogger<AwsKmsProvider> _logger;
	private readonly ConcurrentDictionary<string, string> _aliasToKeyIdMap = new();
	private readonly SemaphoreSlim _keyCreationLock = new(1, 1);

	// Upper bound on the alias->keyId resolution cache. .NET has no built-in bounded concurrent map, so
	// this composes ConcurrentDictionary with a count guard: at capacity new keys are skipped (a later
	// lookup simply re-resolves via KMS DescribeKey), while existing entries may still be refreshed. This
	// prevents unbounded growth across many distinct key ids in long-lived hosts.
	private const int MaxAliasCacheEntries = 1024;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsKmsProvider"/> class.
	/// </summary>
	/// <param name="kmsClient">The AWS KMS client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	/// <param name="metadataCache">Optional memory cache for key metadata.</param>
	public AwsKmsProvider(
		IAmazonKeyManagementService kmsClient,
		IOptions<AwsKmsOptions> options,
		ILogger<AwsKmsProvider> logger,
		IMemoryCache? metadataCache = null)
	{
		_kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metadataCache = metadataCache ?? new MemoryCache(new MemoryCacheOptions());
	}

	/// <inheritdoc/>
	public async Task<DispatchKeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = $"key:{keyId}";
		if (_metadataCache.TryGetValue(cacheKey, out DispatchKeyMetadata? cached))
		{
			return cached;
		}

		try
		{
			var alias = _options.BuildKeyAlias(keyId);
			var response = await _kmsClient.DescribeKeyAsync(
				new DescribeKeyRequest { KeyId = alias },
				cancellationToken).ConfigureAwait(false);

			var tags = await AwsKmsKeyTags.ReadAsync(_kmsClient, response.KeyMetadata.KeyId, cancellationToken)
				.ConfigureAwait(false);
			var metadata = MapToKeyMetadata(keyId, response.KeyMetadata, tags);

			_ = _metadataCache.Set(cacheKey, metadata, TimeSpan.FromSeconds(_options.Cache.MetadataCacheDurationSeconds));
			CacheAliasMapping(keyId, response.KeyMetadata.KeyId);

			return metadata;
		}
		catch (NotFoundException)
		{
			LogKeyNotFound(keyId);
			return null;
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveKey(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<DispatchKeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		// Every version has a durable alias of its own, created at rotation, so a superseded version stays
		// reachable after the unversioned alias moves on. Previously only the current key was addressable
		// and every other version answered null.
		try
		{
			var response = await _kmsClient.DescribeKeyAsync(
				new DescribeKeyRequest { KeyId = _options.BuildVersionAlias(keyId, version) },
				cancellationToken).ConfigureAwait(false);

			var tags = await AwsKmsKeyTags.ReadAsync(_kmsClient, response.KeyMetadata.KeyId, cancellationToken)
				.ConfigureAwait(false);

			return MapToKeyMetadata(keyId, response.KeyMetadata, tags);
		}
		catch (NotFoundException)
		{
			// A key created before per-version aliases existed has only its unversioned alias, and that is
			// version 1 by definition. Fall back so an upgrade does not lose sight of an existing key.
			if (version == 1)
			{
				return await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
			}

			LogVersionsNotAvailable(version, keyId);
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<DispatchKeyMetadata>> ListKeysAsync(
		KeyStatus? status,
		string? purpose,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<DispatchKeyMetadata>();
		string? marker = null;

		try
		{
			do
			{
				var listRequest = new ListAliasesRequest { Limit = 100, Marker = marker };

				var aliasResponse = await _kmsClient.ListAliasesAsync(listRequest, cancellationToken)
					.ConfigureAwait(false);

				// A region holding no aliases answers with no Aliases collection at all, not an empty one:
				// this SDK major leaves collection properties null unless the service populated them. That
				// now reaches GetActiveKeyAsync, whose documented answer for "nothing here" is null -- so an
				// unguarded iteration would throw out of the getter on exactly the empty account it is meant
				// to answer null for.
				foreach (var alias in aliasResponse.Aliases ?? [])
				{
					// Filter to our aliases only
					if (!alias.AliasName.StartsWith($"alias/{_options.KeyAliasPrefix}", StringComparison.Ordinal))
					{
						continue;
					}

					if (string.IsNullOrEmpty(alias.TargetKeyId))
					{
						continue;
					}

					var keyId = ExtractKeyIdFromAlias(alias.AliasName);
					if (string.IsNullOrEmpty(keyId))
					{
						continue;
					}

					// Skip per-version aliases. They exist so a superseded version stays addressable after
					// the unversioned alias moves on; they are an addressing mechanism, not additional
					// logical keys. Enumerating them here would report one key per version, each under a
					// synthetic id ending in "-v<n>".
					if (VersionAliasSuffix().IsMatch(keyId))
					{
						continue;
					}

					try
					{
						var describeResponse = await _kmsClient.DescribeKeyAsync(
							new DescribeKeyRequest { KeyId = alias.TargetKeyId },
							cancellationToken).ConfigureAwait(false);

						var tags = await AwsKmsKeyTags.ReadAsync(_kmsClient, alias.TargetKeyId, cancellationToken)
							.ConfigureAwait(false);

						// Filter on the recorded purpose. This previously matched the purpose against a
						// SUBSTRING OF THE KEY ID, which both missed keys whose id did not contain the word
						// and matched keys whose id happened to -- a key named "payments-encryption" with
						// purpose "signing" satisfied a query for "encryption".
						if (purpose is not null
							&& !string.Equals(AwsKmsKeyTags.PurposeOf(tags), purpose, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						var metadata = MapToKeyMetadata(keyId, describeResponse.KeyMetadata, tags);

						// Filter by status if specified
						if (status.HasValue && metadata.Status != status.Value)
						{
							continue;
						}

						results.Add(metadata);
					}
					catch (NotFoundException)
					{
						// Key was deleted, skip
					}
				}

				marker = aliasResponse.Truncated == true ? aliasResponse.NextMarker : null;
			} while (marker is not null);
		}
		catch (Exception ex)
		{
			LogFailedToListKeys(ex);
			throw;
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		EncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _keyCreationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var alias = _options.BuildKeyAlias(keyId);
			var existingKey = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

			if (existingKey is not null)
			{
				// Key exists - trigger AWS KMS rotation
				return await RotateExistingKeyAsync(keyId, existingKey, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// Create new key
				return await CreateNewKeyAsync(keyId, alias, purpose, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogFailedToRotateKey(keyId, ex);
			return KeyRotationResult.Failed(ex.Message);
		}
		finally
		{
			_ = _keyCreationLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<KeyDestructionOutcome> DeleteKeyAsync(string keyId, int retentionDays, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		try
		{
			var alias = _options.BuildKeyAlias(keyId);

			// First, resolve the alias to the actual key ID
			string? kmsKeyId = null;
			if (_aliasToKeyIdMap.TryGetValue(keyId, out var cachedKeyId))
			{
				kmsKeyId = cachedKeyId;
			}
			else
			{
				var key = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
				if (key is not null)
				{
					kmsKeyId = _aliasToKeyIdMap.GetValueOrDefault(keyId);
				}
			}

			// A rotated key is several CMKs: rotation creates a new CMK, moves the unversioned alias to it, and
			// leaves each superseded CMK ENABLED (so it can still decrypt) behind its own per-version alias.
			// Destroying only the CMK the unversioned alias names left every earlier version able to decrypt
			// everything written before the rotation -- so every version is destroyed here, not just the current one.
			var targets = new List<string>();
			if (!string.IsNullOrEmpty(kmsKeyId))
			{
				targets.Add(kmsKeyId);
			}

			foreach (var target in await FindVersionAliasTargetsAsync(keyId, cancellationToken).ConfigureAwait(false))
			{
				if (!targets.Contains(target, StringComparer.Ordinal))
				{
					targets.Add(target);
				}
			}

			if (targets.Count == 0)
			{
				LogKeyIdResolutionFailed(keyId);
				return KeyDestructionOutcome.NotFound;
			}

			var effectiveWindow = retentionDays <= 0
				? MinPendingWindowDays
				: Math.Clamp(retentionDays, MinPendingWindowDays, MaxPendingWindowDays);

			var outcomes = new List<KeyDestructionOutcome>(targets.Count);
			foreach (var target in targets)
			{
				outcomes.Add(await DestroyCmkAsync(
					keyId, target, isCurrent: string.Equals(target, kmsKeyId, StringComparison.Ordinal), effectiveWindow, cancellationToken)
					.ConfigureAwait(false));
			}

			return CombineOutcomes(outcomes);
		}
		catch (NotFoundException)
		{
			return KeyDestructionOutcome.NotFound;
		}
		catch (Exception ex)
		{
			LogFailedToScheduleDeletion(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Destroyed only when no alias of this key -- the unversioned alias or any per-version alias -- still names a
	/// CMK that KMS can describe. A CMK pending deletion is recoverable with <c>CancelKeyDeletion</c>, and a
	/// superseded version left enabled by rotation can still decrypt, so either one means NOT destroyed. An
	/// imported-material CMK whose material has been deleted is irrecoverable and counts as destroyed. AWS removes
	/// a CMK's aliases when it deletes the CMK. The metadata cache is bypassed. A failure to ask is thrown.
	/// </remarks>
	public async Task<bool> IsKeyDestroyedAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		foreach (var target in await FindAliasTargetsAsync(keyId, includeUnversioned: true, cancellationToken).ConfigureAwait(false))
		{
			AwsKeyMetadata? metadata;
			try
			{
				metadata = (await _kmsClient.DescribeKeyAsync(new DescribeKeyRequest { KeyId = target }, cancellationToken)
					.ConfigureAwait(false)).KeyMetadata;
			}
			catch (NotFoundException)
			{
				continue;
			}

			var materialDeleted = metadata?.Origin == OriginType.EXTERNAL && metadata.KeyState == KeyState.PendingImport;
			if (!materialDeleted)
			{
				return false;
			}
		}

		return true;
	}

	private async Task<KeyDestructionOutcome> DestroyCmkAsync(
		string keyId,
		string kmsKeyId,
		bool isCurrent,
		int effectiveWindow,
		CancellationToken cancellationToken)
	{
		DescribeKeyResponse describe;
		try
		{
			// Determine the key's material origin: imported key material can be destroyed immediately
			// (irrecoverable on return, Vault parity); a KMS-generated symmetric CMK can only be SCHEDULED
			// behind AWS's mandatory 7-30 day window, so it stays recoverable until the window elapses.
			describe = await _kmsClient.DescribeKeyAsync(
				new DescribeKeyRequest { KeyId = kmsKeyId },
				cancellationToken).ConfigureAwait(false);
		}
		catch (NotFoundException)
		{
			return KeyDestructionOutcome.NotFound;
		}

		var origin = describe.KeyMetadata?.Origin;

		// Already scheduled -- by an earlier attempt at this same erasure, for example. KMS rejects scheduling
		// a key that is pending deletion, and the key is still CancelKeyDeletion-recoverable, so the truthful
		// answer is the existing schedule: not NotFound (the key exists), not a failure (nothing went wrong),
		// and not a second request to delete it.
		if (describe.KeyMetadata?.KeyState == KeyState.PendingDeletion)
		{
			var scheduledFor = describe.KeyMetadata.DeletionDate is { } deletionDate
				? new DateTimeOffset(DateTime.SpecifyKind(deletionDate, DateTimeKind.Utc))
				: DateTimeOffset.UtcNow.AddDays(MaxPendingWindowDays);
			LogKeyScheduledForDeletion(keyId, Math.Max(0, (int)Math.Ceiling((scheduledFor - DateTimeOffset.UtcNow).TotalDays)));
			return KeyDestructionOutcome.ScheduledAt(scheduledFor);
		}

		if (origin == OriginType.EXTERNAL)
		{
			// Imported key material already deleted: irrecoverable, nothing further to do.
			if (describe.KeyMetadata?.KeyState != KeyState.PendingImport)
			{
				// Imported key material — delete it immediately; the CMK becomes unusable at once and any data
				// encrypted under it is unrecoverable now (no pending window).
				_ = await _kmsClient.DeleteImportedKeyMaterialAsync(
					new DeleteImportedKeyMaterialRequest { KeyId = kmsKeyId },
					cancellationToken).ConfigureAwait(false);
			}

			if (isCurrent)
			{
				await DeleteAliasAndClearCacheAsync(_options.BuildKeyAlias(keyId), keyId, cancellationToken).ConfigureAwait(false);
			}

			LogKeyDestroyed(keyId);
			return KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow);
		}

		// KMS-generated CMK (or external key store): AWS enforces a mandatory pending-deletion window and the
		// key remains CancelKeyDeletion-recoverable until it elapses. Do NOT silently clamp: honor the request
		// where legal and DISCLOSE the effective irreversibility instant through the returned outcome.
		_ = await _kmsClient.ScheduleKeyDeletionAsync(
			new ScheduleKeyDeletionRequest { KeyId = kmsKeyId, PendingWindowInDays = effectiveWindow },
			cancellationToken).ConfigureAwait(false);

		// Keep the alias. A scheduled key remains recoverable until its window elapses, and the contract
		// is explicit that it MUST NOT be attested as erased before then -- which requires that it stay
		// observable. Deleting the alias here made the key vanish from this provider's view the instant
		// deletion was scheduled, so a compliance caller could not distinguish "scheduled, still
		// recoverable" from "gone", which is the distinction the outcome exists to report. The alias is
		// removed only when destruction has actually completed.
		_metadataCache.Remove($"key:{keyId}");

		LogKeyScheduledForDeletion(keyId, effectiveWindow);

		return KeyDestructionOutcome.ScheduledAt(DateTimeOffset.UtcNow.AddDays(effectiveWindow));
	}

	/// <summary>
	/// The weakest outcome across every CMK of one logical key: recoverable until the LAST scheduled one is
	/// deleted, so one scheduled CMK downgrades the whole key, and it is NotFound only when no CMK existed.
	/// </summary>
	private static KeyDestructionOutcome CombineOutcomes(List<KeyDestructionOutcome> outcomes)
	{
		if (outcomes.TrueForAll(static o => o.State == KeyDestructionState.NotFound))
		{
			return KeyDestructionOutcome.NotFound;
		}

		var scheduled = outcomes.Where(static o => o.State == KeyDestructionState.ScheduledIrreversible).ToList();
		if (scheduled.Count > 0)
		{
			return KeyDestructionOutcome.ScheduledAt(scheduled.Max(static o => o.IrreversibleAt ?? DateTimeOffset.MaxValue));
		}

		return KeyDestructionOutcome.CompletedAt(
			outcomes.Where(static o => o.State == KeyDestructionState.Completed).Max(static o => o.IrreversibleAt ?? DateTimeOffset.UtcNow));
	}

	private Task<IReadOnlyList<string>> FindVersionAliasTargetsAsync(string keyId, CancellationToken cancellationToken) =>
		FindAliasTargetsAsync(keyId, includeUnversioned: false, cancellationToken);

	/// <summary>
	/// The CMKs this logical key's aliases name: the per-version aliases, and optionally the unversioned one.
	/// Aliases are listed rather than guessed, so a gap in the version sequence cannot hide a CMK.
	/// </summary>
	private async Task<IReadOnlyList<string>> FindAliasTargetsAsync(
		string keyId,
		bool includeUnversioned,
		CancellationToken cancellationToken)
	{
		var baseAlias = _options.BuildKeyAlias(keyId);
		var versionPrefix = baseAlias + "-v";
		var targets = new List<string>();
		string? marker = null;

		do
		{
			var response = await _kmsClient.ListAliasesAsync(
				new ListAliasesRequest { Limit = 100, Marker = marker },
				cancellationToken).ConfigureAwait(false);

			foreach (var entry in response?.Aliases ?? [])
			{
				if (string.IsNullOrEmpty(entry.TargetKeyId) || string.IsNullOrEmpty(entry.AliasName))
				{
					continue;
				}

				var isVersion = entry.AliasName.StartsWith(versionPrefix, StringComparison.Ordinal)
					&& entry.AliasName.Length > versionPrefix.Length
					&& entry.AliasName.AsSpan(versionPrefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;
				var isBase = includeUnversioned && string.Equals(entry.AliasName, baseAlias, StringComparison.Ordinal);

				if ((isVersion || isBase) && !targets.Contains(entry.TargetKeyId, StringComparer.Ordinal))
				{
					targets.Add(entry.TargetKeyId);
				}
			}

			marker = response?.Truncated == true ? response.NextMarker : null;
		}
		while (!string.IsNullOrEmpty(marker));

		return targets;
	}

	/// <summary>AWS KMS mandatory minimum pending-deletion window for a symmetric CMK (days).</summary>
	private const int MinPendingWindowDays = 7;

	/// <summary>AWS KMS maximum pending-deletion window (days).</summary>
	private const int MaxPendingWindowDays = 30;

	private async Task DeleteAliasAndClearCacheAsync(string alias, string keyId, CancellationToken cancellationToken)
	{
		try
		{
			_ = await _kmsClient.DeleteAliasAsync(
				new DeleteAliasRequest { AliasName = alias },
				cancellationToken).ConfigureAwait(false);
		}
		catch (NotFoundException)
		{
			// Alias already deleted
		}

		_metadataCache.Remove($"key:{keyId}");
		_ = _aliasToKeyIdMap.TryRemove(keyId, out _);
	}

	/// <inheritdoc/>
	public async Task<bool> SuspendKeyAsync(string keyId, string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		try
		{
			var alias = _options.BuildKeyAlias(keyId);

			// Disable the current version.
			_ = await _kmsClient.DisableKeyAsync(
				new DisableKeyRequest { KeyId = alias },
				cancellationToken).ConfigureAwait(false);

			// And every earlier version. Suspension is a quarantine of the whole logical key -- a
			// compromised key is compromised in all its versions -- whereas the unversioned alias names only
			// the newest. Leaving prior versions enabled would let a suspended key keep decrypting through
			// its own version aliases, and would report those versions as merely rotated-out rather than
			// quarantined. Superseded versions stay enabled through ROTATION, deliberately, so they can
			// decrypt; suspension is the case where that must stop.
			var current = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

			for (var priorVersion = 1; priorVersion < (current?.Version ?? 1); priorVersion++)
			{
				try
				{
					_ = await _kmsClient.DisableKeyAsync(
						new DisableKeyRequest { KeyId = _options.BuildVersionAlias(keyId, priorVersion) },
						cancellationToken).ConfigureAwait(false);
				}
				catch (NotFoundException)
				{
					// A version created before per-version aliases existed has none. Nothing to disable.
				}
			}

			// Update tags with suspension reason
			string? kmsKeyId = null;
			if (_aliasToKeyIdMap.TryGetValue(keyId, out var cachedKeyId))
			{
				kmsKeyId = cachedKeyId;
			}

			if (!string.IsNullOrEmpty(kmsKeyId))
			{
				_ = await _kmsClient.TagResourceAsync(
					new TagResourceRequest
					{
						KeyId = kmsKeyId,
						Tags = new List<Tag>
						{
							new() { TagKey = "SuspensionReason", TagValue = reason },
							new() { TagKey = "SuspendedAt", TagValue = DateTimeOffset.UtcNow.ToString("O") }
						}
					},
					cancellationToken).ConfigureAwait(false);
			}

			// Clear cache
			_metadataCache.Remove($"key:{keyId}");

			LogKeySuspended(keyId, reason);

			return true;
		}
		catch (NotFoundException)
		{
			return false;
		}
		catch (Exception ex)
		{
			LogFailedToSuspendKey(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> ReactivateKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		try
		{
			var alias = _options.BuildKeyAlias(keyId);

			// Inverse of SuspendKeyAsync's DisableKeyAsync: re-enable the key natively via AWS KMS EnableKey.
			_ = await _kmsClient.EnableKeyAsync(
				new EnableKeyRequest { KeyId = alias },
				cancellationToken).ConfigureAwait(false);

			// Clear cache
			_metadataCache.Remove($"key:{keyId}");

			LogKeyReactivated(keyId);

			return true;
		}
		catch (NotFoundException)
		{
			return false;
		}
		catch (Exception ex)
		{
			LogFailedToReactivateKey(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<DispatchKeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Query by PURPOSE, which is what the caller asked about. This previously used the purpose string
		// as a key IDENTIFIER, so GetActiveKeyAsync("encryption") looked for a key literally named
		// "encryption" and returned null for every real deployment -- silently, because null is also the
		// documented answer for "no active key exists".
		var candidates = await ListKeysAsync(KeyStatus.Active, purpose, cancellationToken).ConfigureAwait(false);

		// Newest active key wins. Rotation marks the superseded key DecryptOnly, so it is already excluded
		// by the status filter rather than by ordering.
		// The creation instant is only the tiebreak here; an UNDATED key loses that tiebreak rather than
		// winning it, for the same reason as the other providers.
		return candidates
			.OrderByDescending(static k => k.Version)
			.ThenByDescending(static k => k.CreatedAt.HasValue)
			.ThenByDescending(static k => k.CreatedAt)
			.FirstOrDefault();

		// Deliberately NOT creating a key here. This is a getter, and it previously provisioned a KMS
		// customer master key when it found none -- a durable, billable, security-relevant side effect from
		// a query, and one that made the documented "or null" answer unreachable on that path. A caller who
		// wants a key created asks for that explicitly, through RotateKeyAsync.
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_keyCreationLock.Dispose();
		_aliasToKeyIdMap.Clear();

		// Don't dispose _kmsClient or _metadataCache - they may be shared/injected
		_disposed = true;
		LogDisposed();
	}

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} not found in AWS KMS")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to retrieve key {KeyId} from AWS KMS")]
	private partial void LogFailedToRetrieveKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"AWS KMS does not expose historical key versions. Requested v{Version} for key {KeyId}")]
	private partial void LogVersionsNotAvailable(int version, string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to list keys from AWS KMS")]
	private partial void LogFailedToListKeys(Exception ex);

	[LoggerMessage(LogLevel.Error, "Failed to rotate key {KeyId}")]
	private partial void LogFailedToRotateKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning, "Could not resolve KMS key ID for {KeyId}")]
	private partial void LogKeyIdResolutionFailed(string keyId);

	[LoggerMessage(LogLevel.Warning,
		"Key {KeyId} scheduled for deletion in {RetentionDays} days (crypto-shredding)")]
	private partial void LogKeyScheduledForDeletion(string keyId, int retentionDays);

	[LoggerMessage(LogLevel.Warning,
		"Key {KeyId} imported material deleted — irrecoverable now (crypto-shredding)")]
	private partial void LogKeyDestroyed(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to schedule deletion for key {KeyId}")]
	private partial void LogFailedToScheduleDeletion(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} suspended: {Reason}")]
	private partial void LogKeySuspended(string keyId, string reason);

	[LoggerMessage(LogLevel.Error, "Failed to suspend key {KeyId}")]
	private partial void LogFailedToSuspendKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Information, "Key {KeyId} reactivated")]
	private partial void LogKeyReactivated(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to reactivate key {KeyId}")]
	private partial void LogFailedToReactivateKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Debug, "AwsKmsProvider disposed")]
	private partial void LogDisposed();

	[LoggerMessage(LogLevel.Information, "Enabled automatic key rotation for {KeyId}")]
	private partial void LogEnabledAutoRotation(string keyId);

	[LoggerMessage(LogLevel.Information, "Rotated key {KeyId} to new KMS key {KmsKeyId}")]
	private partial void LogRotatedKey(string keyId, string kmsKeyId);

	[LoggerMessage(LogLevel.Information, "Created new key {KeyId} with KMS key {KmsKeyId}")]
	private partial void LogCreatedKey(string keyId, string kmsKeyId);

	[LoggerMessage(
		LogLevel.Error,
		"Lost the provisioning race for key {KeyId} and could not schedule deletion of the CMK {KmsKeyId} this "
		+ "instance had already created. That CMK carries no alias, so no erasure can discover it: delete it by "
		+ "its key id.")]
	private partial void LogOrphanedCmkNotScheduled(string keyId, string kmsKeyId, Exception exception);

	private async Task<KeyRotationResult> RotateExistingKeyAsync(
		string keyId,
		DispatchKeyMetadata existingKey,
		CancellationToken cancellationToken)
	{
		var alias = _options.BuildKeyAlias(keyId);

		// AWS KMS supports automatic rotation for symmetric keys
		// EnableKeyRotation triggers rotation within the next year
		// For immediate rotation, we need to create a new key and update the alias
		// Resolve the current key from KMS rather than from the in-process cache. The cache is bounded, so
		// an eviction previously turned a rotation of a key that exists into a flat "could not resolve"
		// failure -- a cache size reporting a missing key. The alias is the durable name; ask for it.
		string kmsKeyId;

		try
		{
			var described = await _kmsClient.DescribeKeyAsync(
				new DescribeKeyRequest { KeyId = alias },
				cancellationToken).ConfigureAwait(false);

			kmsKeyId = described.KeyMetadata.KeyId;
		}
		catch (NotFoundException)
		{
			return KeyRotationResult.Failed("Could not resolve KMS key ID");
		}

		// Check if automatic rotation is enabled
		var rotationStatus = await _kmsClient.GetKeyRotationStatusAsync(
			new GetKeyRotationStatusRequest { KeyId = kmsKeyId },
			cancellationToken).ConfigureAwait(false);

		if (rotationStatus.KeyRotationEnabled != true && _options.KeyPolicy.EnableAutoRotation)
		{
			// Enable automatic rotation
			_ = await _kmsClient.EnableKeyRotationAsync(
				new EnableKeyRotationRequest { KeyId = kmsKeyId },
				cancellationToken).ConfigureAwait(false);

			LogEnabledAutoRotation(keyId);
		}

		var newVersion = existingKey.Version + 1;

		// THE LINEAGE IDENTITY SURVIVES THE ROTATION. A rotation extends the material lineage, so every
		// envelope already written under this handle keeps naming the same generation. Minting a new one here
		// would leave all of them naming an identifier no ledger row will ever hold, and -- because the
		// erasure records only the generation it finds at destruction time -- their reads would fail forever
		// with the material gone and nothing able to record it.
		//
		// An absent tag means a CMK provisioned before this lineage identifier existed. One is minted now,
		// which is honest: that handle has no recorded identity to preserve.
		var lineageGeneration =
			AwsKmsKeyTags.GenerationOf(
				await AwsKmsKeyTags.ReadAsync(_kmsClient, kmsKeyId, cancellationToken)
					.ConfigureAwait(false))
			?? MintGeneration();

		// Rotation creates a NEW CMK and repoints the alias, so every version is a distinct key with its
		// own ARN. That is what makes versions representable here at all.
		var newKey = await CreateKmsKeyAsync(keyId, existingKey.Purpose, newVersion, lineageGeneration, cancellationToken)
			.ConfigureAwait(false);

		// Give the new version a durable name of its own before anything points at it. Without this the
		// superseded key becomes unreachable the moment the alias moves.
		_ = await _kmsClient.CreateAliasAsync(
			new CreateAliasRequest
			{
				AliasName = _options.BuildVersionAlias(keyId, newVersion),
				TargetKeyId = newKey.KeyId,
			},
			cancellationToken).ConfigureAwait(false);

		// New encryptions now resolve to the new key.
		_ = await _kmsClient.UpdateAliasAsync(
			new UpdateAliasRequest { AliasName = alias, TargetKeyId = newKey.KeyId },
			cancellationToken).ConfigureAwait(false);

		// Mark the superseded key rather than disabling it. AWS documents a disabled key as unusable for
		// EVERY cryptographic operation, Decrypt included, so disabling here made all ciphertext under the
		// previous key undecryptable until an operator manually re-enabled it. The key stays enabled so it
		// can still decrypt, and the tag records that it must not receive new encryptions -- which is what
		// DecryptOnly means.
		await AwsKmsKeyTags.MarkSupersededAsync(_kmsClient, kmsKeyId, cancellationToken).ConfigureAwait(false);

		// Clear cache and update mapping
		_metadataCache.Remove($"key:{keyId}");
		CacheAliasMapping(keyId, newKey.KeyId);

		var newMetadata = MapToKeyMetadata(keyId, newKey) with { Version = newVersion, Purpose = existingKey.Purpose };
		var previousMetadata = existingKey with { Status = KeyStatus.DecryptOnly };

		LogRotatedKey(keyId, newKey.KeyId);

		return KeyRotationResult.Succeeded(newMetadata, previousMetadata);
	}

	// Records an alias->keyId resolution, bounded to avoid unbounded growth. At capacity a new key is
	// skipped (a later lookup re-resolves via KMS DescribeKey); an already-cached key is still refreshed.
	private void CacheAliasMapping(string keyId, string resolvedKeyId)
	{
		if (_aliasToKeyIdMap.Count < MaxAliasCacheEntries || _aliasToKeyIdMap.ContainsKey(keyId))
		{
			_aliasToKeyIdMap[keyId] = resolvedKeyId;
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// KMS has no create-if-absent primitive: <c>CreateKey</c> always makes a new CMK and does not take a name.
	/// The conditional insert is <c>CreateAlias</c>, which KMS rejects with
	/// <see cref="AlreadyExistsException"/> for a name that is taken -- so the alias, not the key, is what
	/// serialises two racing provisioners.
	/// </para>
	/// <para>
	/// <b>The loser must destroy the CMK it made, and that is not tidiness.</b> A CMK created here has no alias
	/// until the alias call succeeds, and destruction enumerates a key's CMKs THROUGH its aliases -- so an
	/// alias-less CMK is one no erasure can ever reach, holding live material for a subject whose erasure
	/// reports complete. Scheduling it for deletion on the losing path is what keeps the crypto-shred
	/// guarantee true in the presence of ordinary write concurrency.
	/// </para>
	/// <para>
	/// The winner's key is read back through the version alias rather than the unversioned one, because the
	/// winner may not have created the unversioned alias yet.
	/// </para>
	/// </remarks>
	public async Task<DispatchKeyMetadata> CreateKeyIfAbsentAsync(
		string keyId,
		EncryptionAlgorithm algorithm,
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

		var versionAlias = _options.BuildVersionAlias(keyId, 1);

		// A handle that holds nothing is new material, including one re-occupied after an erasure -- which MUST
		// get a different generation from the one that was destroyed, or a read of the erased subject's old
		// ciphertext would find the destroyed generation in the ledger and tombstone data that is live.
		var kmsKey = await CreateKmsKeyAsync(keyId, purpose, version: 1, MintGeneration(), cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// The version-1 alias is claimed FIRST, so it is the guard both racers contend on. Claiming the
			// unversioned alias first would leave a window in which version 1 is unaddressable.
			_ = await _kmsClient.CreateAliasAsync(
				new CreateAliasRequest { AliasName = versionAlias, TargetKeyId = kmsKey.KeyId },
				cancellationToken).ConfigureAwait(false);
		}
		catch (AlreadyExistsException)
		{
			await AbandonUnaliasedCmkAsync(keyId, kmsKey.KeyId, cancellationToken).ConfigureAwait(false);

			var winner = await DescribeByAliasAsync(keyId, versionAlias, cancellationToken).ConfigureAwait(false);

			return winner
				?? throw new EncryptionException(
					$"Another writer claimed the key alias for '{keyId}' but no key is resolvable there.")
				{
					ErrorCode = EncryptionErrorCode.KeyNotFound
				};
		}

		// Won. Finish provisioning: the unversioned alias is what new encryptions resolve through.
		_ = await _kmsClient.CreateAliasAsync(
			new CreateAliasRequest { AliasName = _options.BuildKeyAlias(keyId), TargetKeyId = kmsKey.KeyId },
			cancellationToken).ConfigureAwait(false);

		if (_options.KeyPolicy.EnableAutoRotation)
		{
			_ = await _kmsClient.EnableKeyRotationAsync(
				new EnableKeyRotationRequest { KeyId = kmsKey.KeyId },
				cancellationToken).ConfigureAwait(false);
		}

		CacheAliasMapping(keyId, kmsKey.KeyId);

		var metadata = MapToKeyMetadata(keyId, kmsKey);
		_ = _metadataCache.Set($"key:{keyId}", metadata, TimeSpan.FromSeconds(_options.Cache.MetadataCacheDurationSeconds));

		LogCreatedKey(keyId, kmsKey.KeyId);

		return metadata;
	}

	/// <summary>
	/// Schedules a CMK this provider created but could not name, so it cannot outlive the attempt.
	/// </summary>
	private async Task AbandonUnaliasedCmkAsync(string keyId, string kmsKeyId, CancellationToken cancellationToken)
	{
		try
		{
			_ = await _kmsClient.ScheduleKeyDeletionAsync(
				new ScheduleKeyDeletionRequest { KeyId = kmsKeyId, PendingWindowInDays = MinPendingWindowDays },
				cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// Reported, never rethrown: the caller's provisioning SUCCEEDED -- another writer's key is at this
			// handle -- and failing the call would turn a benign lost race into an error. What must not happen
			// is the orphan going unrecorded, because no later erasure can discover it.
			LogOrphanedCmkNotScheduled(keyId, kmsKeyId, ex);
		}
	}

	/// <summary>
	/// Resolves a key through one of its aliases, bypassing the unversioned alias and the metadata cache.
	/// </summary>
	private async Task<DispatchKeyMetadata?> DescribeByAliasAsync(
		string keyId,
		string aliasName,
		CancellationToken cancellationToken)
	{
		try
		{
			var described = await _kmsClient.DescribeKeyAsync(
				new DescribeKeyRequest { KeyId = aliasName },
				cancellationToken).ConfigureAwait(false);

			if (described.KeyMetadata is null)
			{
				return null;
			}

			CacheAliasMapping(keyId, described.KeyMetadata.KeyId);

			return MapToKeyMetadata(keyId, described.KeyMetadata);
		}
		catch (NotFoundException)
		{
			return null;
		}
	}

	private async Task<KeyRotationResult> CreateNewKeyAsync(
		string keyId,
		string alias,
		string? purpose,
		CancellationToken cancellationToken)
	{
		// A first provisioning, so a fresh material lineage.
		var kmsKey = await CreateKmsKeyAsync(keyId, purpose, version: 1, MintGeneration(), cancellationToken)
			.ConfigureAwait(false);

		// Version 1 needs its own alias too, not only versions produced by rotation. Without it the first
		// version becomes unaddressable the moment a rotation moves the unversioned alias, and a request for
		// version 1 silently resolves to whatever is current.
		_ = await _kmsClient.CreateAliasAsync(
			new CreateAliasRequest
			{
				AliasName = _options.BuildVersionAlias(keyId, 1),
				TargetKeyId = kmsKey.KeyId,
			},
			cancellationToken).ConfigureAwait(false);


		// Create alias
		_ = await _kmsClient.CreateAliasAsync(
			new CreateAliasRequest { AliasName = alias, TargetKeyId = kmsKey.KeyId },
			cancellationToken).ConfigureAwait(false);

		// Enable auto-rotation if configured
		if (_options.KeyPolicy.EnableAutoRotation)
		{
			_ = await _kmsClient.EnableKeyRotationAsync(
				new EnableKeyRotationRequest { KeyId = kmsKey.KeyId },
				cancellationToken).ConfigureAwait(false);
		}

		CacheAliasMapping(keyId, kmsKey.KeyId);

		var metadata = MapToKeyMetadata(keyId, kmsKey);
		_ = _metadataCache.Set($"key:{keyId}", metadata, TimeSpan.FromSeconds(_options.Cache.MetadataCacheDurationSeconds));

		LogCreatedKey(keyId, kmsKey.KeyId);

		return KeyRotationResult.Succeeded(metadata);
	}

	/// <summary>
	/// Mints an identifier for a newly provisioned material lineage.
	/// </summary>
	/// <remarks>
	/// The CSPRNG lives in <see cref="KeyGeneration.Mint"/> rather than here, so every provider mints the same
	/// way and none can drift to a weaker source. This only renders it for the tag.
	/// </remarks>
	private static string MintGeneration() => KeyGeneration.Mint().ToString();

	/// <summary>
	/// Creates the CMK backing one version of a logical key, tagged as belonging to a material lineage.
	/// </summary>
	/// <param name="keyId">The logical key identifier.</param>
	/// <param name="purpose">The key's purpose, if any.</param>
	/// <param name="version">The version this CMK is.</param>
	/// <param name="generation">
	/// The identifier of the material lineage this CMK belongs to: minted by the caller for a first
	/// provisioning, carried from the superseded CMK for a rotation.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// The lineage identifier is a PARAMETER rather than something minted here, and that is the whole point:
	/// this method is called by both a first provisioning and a rotation, and the two must differ. Minting
	/// inside would give every rotation a new identity and strand every envelope written before it.
	/// </remarks>
	private async Task<AwsKeyMetadata> CreateKmsKeyAsync(
		string keyId,
		string? purpose,
		int version,
		string generation,
		CancellationToken cancellationToken)
	{
		var request = new CreateKeyRequest
		{
			KeySpec = _options.KeyPolicy.DefaultKeySpec,
			KeyUsage = KeyUsageType.ENCRYPT_DECRYPT,
			Description = $"Excalibur Dispatch encryption key: {keyId}",
			MultiRegion = _options.KeyPolicy.CreateMultiRegionKeys,
			Tags = new List<Tag>
			{
				new() { TagKey = "Application", TagValue = "Excalibur.Dispatch" },
				new() { TagKey = "KeyId", TagValue = keyId },
				new() { TagKey = "CreatedAt", TagValue = DateTimeOffset.UtcNow.ToString("O") },

				// The version lives on the key. AWS symmetric CMKs carry no consumer-visible version, and
				// this provider rotates by creating a NEW CMK and repointing the alias -- so the version is
				// ours to record, somewhere that survives a restart and is visible to every instance.
				new() { TagKey = AwsKmsKeyTags.Version, TagValue = version.ToString(CultureInfo.InvariantCulture) },
				new() { TagKey = AwsKmsKeyTags.Generation, TagValue = generation },
			}
		};

		if (!string.IsNullOrEmpty(purpose))
		{
			request.Tags.Add(new Tag { TagKey = "Purpose", TagValue = purpose });
		}

		if (!string.IsNullOrEmpty(_options.Environment))
		{
			request.Tags.Add(new Tag { TagKey = "Environment", TagValue = _options.Environment });
		}

		var response = await _kmsClient.CreateKeyAsync(request, cancellationToken).ConfigureAwait(false);
		return response.KeyMetadata;
	}

	private DispatchKeyMetadata MapToKeyMetadata(
		string keyId,
		AwsKeyMetadata kmsMetadata,
		IReadOnlyDictionary<string, string>? tags = null)
	{
		// AWS SDK KeyState is a ConstantClass, not an enum - use Value comparison
		var superseded = AwsKmsKeyTags.IsSuperseded(tags);
		var stateValue = kmsMetadata.KeyState?.Value;
		var status = stateValue switch
		{
			// An enabled key that has been rotated out is DecryptOnly, not Active: it must still decrypt
			// existing ciphertext and must not receive new encryptions.
			"Enabled" => superseded ? KeyStatus.DecryptOnly : KeyStatus.Active,

			// Disabled now means only what an operator did deliberately. Rotation no longer disables, so a
			// routinely superseded key is no longer indistinguishable from a quarantined one.
			"Disabled" => KeyStatus.Suspended,
			"PendingDeletion" => KeyStatus.PendingDestruction,
			"PendingImport" => superseded ? KeyStatus.DecryptOnly : KeyStatus.Active,
			"PendingReplicaDeletion" => KeyStatus.PendingDestruction,
			"Unavailable" => KeyStatus.Suspended,
			_ => superseded ? KeyStatus.DecryptOnly : KeyStatus.Active
		};

		return new DispatchKeyMetadata
		{
			KeyId = keyId,
			Version = AwsKmsKeyTags.VersionOf(tags),

			// THE CMK ID IS THE GENERATION, and KMS gives it to us for free. Rotation here mints a whole new
			// CMK and repoints the alias, and provisioning at a handle whose key was destroyed mints another
			// one, so the CMK id names exactly one piece of material and is never reused. The handle (an alias)
			// and the version tag are both re-occupiable; this is not.
			// THE LINEAGE TAG, NOT THE CMK ID. Every version of a logical key is its own CMK, so the CMK id
			// identifies a VERSION: a rotation moves it within one lineage, and an erasure that records only
			// the current generation leaves every earlier envelope naming an identifier no ledger row holds.
			// Absent is reported as absent rather than falling back to the CMK id -- a CMK this framework did
			// not provision has no lineage identity we can state, and the read path refuses such an envelope
			// instead of tombstoning it.
			// PARSED, never trusted as-is. A CMK tag is consumer-writable, so a value that is not a generation
			// must read as ABSENT rather than becoming one: the ledger keys on it, and a value nothing minted
			// could collide with another subject's.
			Generation = KeyGeneration.TryParse(AwsKmsKeyTags.GenerationOf(tags), out var parsedGeneration)
				? parsedGeneration
				: null,

			Status = status,
			Algorithm = EncryptionAlgorithm.Aes256Gcm, // SYMMETRIC_DEFAULT is AES-256-GCM
			// Null when KMS reports no creation date. This is an ordinary describe-the-key read and must not
			// fail over a field the caller may not need; the point-in-time version provider, where ordering IS
			// the operation, still refuses rather than answering a question it cannot answer.
			CreatedAt = kmsMetadata.CreationDate is { } creationDate
				? new DateTimeOffset(creationDate)
				: null,
			ExpiresAt = kmsMetadata.DeletionDate is { } deletionDate
				? new DateTimeOffset(deletionDate)
				: null,
			LastRotatedAt = null, // AWS KMS doesn't expose this directly
			// Read back from the tag this provider writes at creation. Returning null meant a consumer
			// could not filter by purpose even client-side, on data we had already stored.
			Purpose = AwsKmsKeyTags.PurposeOf(tags),
			IsFipsCompliant = _options.UseFipsEndpoint
		};
	}

	/// <summary>Matches the "-v&lt;n&gt;" suffix that marks a per-version alias.</summary>
	/// <returns>The compiled matcher.</returns>
	[System.Text.RegularExpressions.GeneratedRegex(@"-v\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
	private static partial System.Text.RegularExpressions.Regex VersionAliasSuffix();

	private string? ExtractKeyIdFromAlias(string aliasName)
	{
		// alias/excalibur-dispatch-{environment}-{keyId} -> {keyId}
		var prefix = $"alias/{_options.KeyAliasPrefix}";
		if (!string.IsNullOrEmpty(_options.Environment))
		{
			prefix += $"-{_options.Environment}";
		}

		prefix += "-";

		if (!aliasName.StartsWith(prefix, StringComparison.Ordinal))
		{
			return null;
		}

		return aliasName[prefix.Length..];
	}
}
