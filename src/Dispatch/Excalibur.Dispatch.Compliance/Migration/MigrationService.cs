// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for migrating encrypted data between encryption versions.
/// </summary>
/// <remarks>
/// <para>
/// This service provides both lazy re-encryption (on-access) and batch migration
/// capabilities. Version detection is performed by analyzing the ciphertext header.
/// </para>
/// <para>
/// The service maintains migration progress statistics that can be queried
/// for monitoring and reporting purposes.
/// </para>
/// </remarks>
public sealed partial class MigrationService : IMigrationService
{
	private const int MinCiphertextSize = 28; // 12-byte nonce + 16-byte tag minimum
	private const int V11HeaderSize = 7; // Includes version prefix + key version
	private const byte V11MagicByte = 0xED; // Magic byte for v1.1 format

	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IComplianceMetrics? _metrics;
	private readonly ILogger<MigrationService> _logger;
	private readonly MigrationOptions _options;
	private readonly ConcurrentDictionary<EncryptionVersion, long> _versionCounts = new();
	private long _totalDetected;
	private long _totalMigrated;
	private long _totalFailures;

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationService"/> class.
	/// </summary>
	/// <param name="encryptionProvider">The encryption provider for cryptographic operations.</param>
	/// <param name="metrics">Optional compliance metrics for tracking.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">Configuration options.</param>
	public MigrationService(
		IEncryptionProvider encryptionProvider,
		IComplianceMetrics? metrics,
		ILogger<MigrationService> logger,
		IOptions<MigrationOptions> options)
	{
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_metrics = metrics;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? new MigrationOptions();
	}

	/// <inheritdoc />
	public EncryptionVersion CurrentVersion => _options.TargetVersion;

	/// <inheritdoc />
	public EncryptionVersion DetectVersion(ReadOnlySpan<byte> ciphertext)
	{
		if (ciphertext.Length < MinCiphertextSize)
		{
			return EncryptionVersion.Unknown;
		}

		// Check for v1.1 magic byte header
		if (ciphertext.Length >= MinCiphertextSize + V11HeaderSize && ciphertext[0] == V11MagicByte)
		{
			// v1.1 format: [magic(1)] [version(2)] [key_version(4)] [nonce(12)] [ciphertext...] [tag(16)]
			var major = ciphertext[1];
			var minor = ciphertext[2];

			if (_options.TrackProgress)
			{
				IncrementVersionCount(new EncryptionVersion(major, minor));
			}

			return new EncryptionVersion(major, minor);
		}

		// Assume v1.0 format (no header)
		if (_options.TrackProgress)
		{
			IncrementVersionCount(EncryptionVersion.Version10);
		}

		return EncryptionVersion.Version10;
	}

	/// <inheritdoc />
	public bool RequiresMigration(ReadOnlySpan<byte> ciphertext)
	{
		if (!_options.EnableLazyReEncryption)
		{
			return false;
		}

		var version = DetectVersion(ciphertext);
		return version != EncryptionVersion.Unknown && version < _options.TargetVersion;
	}

	/// <inheritdoc />
	public async Task<VersionMigrationResult> MigrateAsync(byte[] ciphertext, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(ciphertext);

		var stopwatch = ValueStopwatch.StartNew();
		var sourceVersion = DetectVersion(ciphertext);

		if (sourceVersion == EncryptionVersion.Unknown)
		{
			return VersionMigrationResult.Failed(
				ciphertext,
				sourceVersion,
				_options.TargetVersion,
				"Unable to detect encryption version",
				stopwatch.Elapsed);
		}

		if (sourceVersion >= _options.TargetVersion)
		{
			return VersionMigrationResult.NotRequired(ciphertext, sourceVersion);
		}

		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(_options.MigrationTimeout);

			// Decrypt with old format - create EncryptedData from raw bytes
			var encryptedData = CreateEncryptedData(ciphertext, sourceVersion);
			var plaintext = await _encryptionProvider.DecryptAsync(encryptedData, EncryptionContext.Default, cts.Token)
				.ConfigureAwait(false);

			// Re-encrypt with new format
			var newEncrypted = await _encryptionProvider.EncryptAsync(plaintext, EncryptionContext.Default, cts.Token)
				.ConfigureAwait(false);
			var newCiphertext = CreateVersionedCiphertext(newEncrypted);

			_ = Interlocked.Increment(ref _totalMigrated);

			_metrics?.RecordEncryptionOperation(
				"ReEncrypt",
				"Migration",
				ciphertext.Length);

			LogMigrationSucceeded(sourceVersion, _options.TargetVersion, (long)stopwatch.ElapsedMilliseconds);

			return VersionMigrationResult.Succeeded(
				ciphertext,
				newCiphertext,
				sourceVersion,
				_options.TargetVersion,
				stopwatch.Elapsed);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_ = Interlocked.Increment(ref _totalFailures);

			LogMigrationFailed(sourceVersion, _options.TargetVersion, ex);

			return VersionMigrationResult.Failed(
				ciphertext,
				sourceVersion,
				_options.TargetVersion,
				ex.Message,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<VersionBatchMigrationResult> MigrateBatchAsync(
		IEnumerable<MigrationItem> items,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(items);

		var itemList = items.ToList();
		var stopwatch = ValueStopwatch.StartNew();
		var results = new List<(string Id, VersionMigrationResult Result)>();
		using var semaphore = new SemaphoreSlim(_options.MaxConcurrentMigrations);
		var successCount = 0;
		var failureCount = 0;
		var skippedCount = 0;

		_ = Interlocked.Add(ref _totalDetected, itemList.Count);

		var tasks = itemList.Select(async item =>
		{
			await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var result = await MigrateAsync(item.Ciphertext, cancellationToken).ConfigureAwait(false);

				lock (results)
				{
					results.Add((item.Id, result));
				}

				if (result.Success && result.SourceVersion < result.TargetVersion)
				{
					_ = Interlocked.Increment(ref successCount);
				}
				else if (result.Success)
				{
					_ = Interlocked.Increment(ref skippedCount);
				}
				else
				{
					_ = Interlocked.Increment(ref failureCount);

					if (_options.FailFast)
					{
						throw new MigrationException($"Migration failed for item {item.Id}: {result.ErrorMessage}");
					}
				}
			}
			finally
			{
				_ = semaphore.Release();
			}
		});

		try
		{
			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
		catch (MigrationException) when (_options.FailFast)
		{
			// Expected when FailFast is enabled
		}

		LogMigrationBatchCompleted(
			successCount,
			failureCount,
			skippedCount,
			(long)stopwatch.ElapsedMilliseconds);

		return new VersionBatchMigrationResult(
			itemList.Count,
			successCount,
			failureCount,
			skippedCount,
			results,
			stopwatch.Elapsed);
	}

	/// <inheritdoc />
	public VersionMigrationProgress GetProgress()
	{
		var versionDistribution = _versionCounts.ToDictionary(kv => kv.Key, kv => kv.Value);

		return new VersionMigrationProgress(
			Interlocked.Read(ref _totalDetected),
			Interlocked.Read(ref _totalMigrated),
			Interlocked.Read(ref _totalFailures),
			versionDistribution,
			DateTimeOffset.UtcNow);
	}

	private static EncryptedData CreateEncryptedData(byte[] ciphertext, EncryptionVersion version)
	{
		// For v1.0, the ciphertext is the raw encrypted data
		// For v1.1, strip the header and extract key version
		if (version >= EncryptionVersion.Version11 && ciphertext.Length > V11HeaderSize && ciphertext[0] == V11MagicByte)
		{
			var keyVersion = BitConverter.ToInt32(ciphertext, 3);
			var actualCiphertext = ciphertext[V11HeaderSize..];

			// Extract IV from the start of the ciphertext (12 bytes for GCM)
			var iv = actualCiphertext[..12];
			var encryptedPayload = actualCiphertext[12..];

			return new EncryptedData
			{
				Ciphertext = encryptedPayload,
				KeyId = "migration-key",
				KeyVersion = keyVersion,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = iv
			};
		}

		// V1.0 format - raw ciphertext (IV is prepended)
		var legacyIv = ciphertext[..12];
		var legacyPayload = ciphertext[12..];

		return new EncryptedData
		{
			Ciphertext = legacyPayload,
			KeyId = "legacy-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = legacyIv
		};
	}

	private void IncrementVersionCount(EncryptionVersion version)
	{
		_ = _versionCounts.AddOrUpdate(version, 1, (_, count) => count + 1);
		_ = Interlocked.Increment(ref _totalDetected);
	}

	[LoggerMessage(
		ComplianceEventId.EncryptionMigrationSucceeded,
		LogLevel.Debug,
		"Migrated data from {SourceVersion} to {TargetVersion} in {Duration}ms")]
	private partial void LogMigrationSucceeded(
		EncryptionVersion sourceVersion,
		EncryptionVersion targetVersion,
		long duration);

	[LoggerMessage(
		ComplianceEventId.EncryptionMigrationFailed,
		LogLevel.Error,
		"Failed to migrate data from {SourceVersion} to {TargetVersion}")]
	private partial void LogMigrationFailed(
		EncryptionVersion sourceVersion,
		EncryptionVersion targetVersion,
		Exception exception);

	[LoggerMessage(
		ComplianceEventId.EncryptionMigrationBatchCompleted,
		LogLevel.Information,
		"Batch migration completed: {Success} succeeded, {Failed} failed, {Skipped} skipped in {Duration}ms")]
	private partial void LogMigrationBatchCompleted(
		int success,
		int failed,
		int skipped,
		long duration);

	private byte[] CreateVersionedCiphertext(EncryptedData encryptedData)
	{
		// V1.1 format: [magic(1)] [version(2)] [key_version(4)] [iv(12)] [ciphertext...]
		var header = new byte[V11HeaderSize];
		header[0] = V11MagicByte;
		header[1] = (byte)_options.TargetVersion.Major;
		header[2] = (byte)_options.TargetVersion.Minor;

		// Encode key version (4 bytes, little-endian)
		_ = BitConverter.TryWriteBytes(header.AsSpan(3, 4), encryptedData.KeyVersion);

		// Combine header + IV + ciphertext
		var result = new byte[header.Length + encryptedData.Iv.Length + encryptedData.Ciphertext.Length];
		header.CopyTo(result, 0);
		encryptedData.Iv.CopyTo(result, header.Length);
		encryptedData.Ciphertext.CopyTo(result, header.Length + encryptedData.Iv.Length);

		return result;
	}
}

/// <summary>
/// Exception thrown when a migration operation fails.
/// </summary>
public sealed class MigrationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationException"/> class.
	/// </summary>
	/// <param name="message">The error message.</param>
	public MigrationException(string message) : base(message) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationException"/> class.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MigrationException(string message, Exception innerException) : base(message, innerException) { }
}
