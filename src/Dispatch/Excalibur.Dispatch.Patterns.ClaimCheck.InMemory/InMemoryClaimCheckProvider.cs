// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// In-memory implementation of the Claim Check pattern provider.
/// Thread-safe, zero-dependency implementation with TTL expiration, compression, and checksum validation.
/// Ideal for testing and local development scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This provider stores claim check payloads in memory using a thread-safe <see cref="ConcurrentDictionary{TKey, TValue}" />.
/// It is NOT intended for production use with large-scale distributed systems due to memory constraints.
/// </para>
/// <para>
/// For production scenarios, use cloud provider implementations:
/// - Azure Blob Storage: Excalibur.Dispatch.Patterns.ClaimCheck.Azure
/// - AWS S3: Excalibur.Dispatch.Patterns.ClaimCheck.Aws [planned]
/// - Google Cloud Storage: Excalibur.Dispatch.Patterns.ClaimCheck.Gcp [planned]
/// </para>
/// </remarks>
public sealed class InMemoryClaimCheckProvider : IClaimCheckProvider, IClaimCheckCleanupProvider
{
	// Thread-safe storage for claim check entries
	private readonly ConcurrentDictionary<string, InMemoryClaimCheckEntry> _storage = new();

	private readonly ClaimCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryClaimCheckProvider" /> class.
	/// </summary>
	/// <param name="options">The claim check options.</param>
	public InMemoryClaimCheckProvider(IOptions<ClaimCheckOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <summary>
	/// Gets the current number of stored claim checks (for testing/monitoring).
	/// </summary>
	internal int EntryCount => _storage.Count;

	/// <inheritdoc />
	public Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null)
	{
		ArgumentNullException.ThrowIfNull(payload);

		// Generate unique ID for this claim check
		var claimId = $"{_options.IdPrefix}{Guid.NewGuid()}";
		var now = DateTimeOffset.UtcNow;
		var expiresAt = _options.DefaultTtl == TimeSpan.Zero
			? null
			: (DateTimeOffset?)now.Add(_options.DefaultTtl);

		// Apply compression if enabled and payload meets threshold
		var processedPayload = payload;
		var isCompressed = false;

		if (_options.EnableCompression && payload.Length >= _options.CompressionThreshold)
		{
			processedPayload = CompressPayload(payload);

			// Only keep compressed version if compression ratio is acceptable
			var compressionRatio = (double)processedPayload.Length / payload.Length;
			if (compressionRatio <= _options.MinCompressionRatio)
			{
				isCompressed = true;
			}
			else
			{
				// Compression not effective, use original payload
				processedPayload = payload;
			}
		}

		// Compute checksum if validation enabled
		string? checksum = null;
		if (_options.ValidateChecksum)
		{
			checksum = ComputeChecksum(processedPayload);
		}

		// Create storage entry
		var entry = new InMemoryClaimCheckEntry
		{
			Id = claimId,
			Payload = processedPayload,
			Metadata = metadata,
			StoredAt = now,
			ExpiresAt = expiresAt,
			Size = payload.Length, // Original size, not compressed size
			IsCompressed = isCompressed,
			Checksum = checksum
		};

		// Store entry (ConcurrentDictionary handles thread-safety)
		if (!_storage.TryAdd(claimId, entry))
		{
			throw new InvalidOperationException($"Failed to store claim check with ID '{claimId}'. ID already exists.");
		}

		// Build reference to return
		var reference = new ClaimCheckReference
		{
			Id = claimId,
			BlobName = $"{_options.BlobNamePrefix}/{claimId}",
			Location = $"inmemory://{_options.ContainerName}/{claimId}",
			Size = payload.Length,
			StoredAt = now,
			ExpiresAt = expiresAt,
			Metadata = metadata
		};

		return Task.FromResult(reference);
	}

	/// <inheritdoc />
	public Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		// Retrieve entry from storage
		if (!_storage.TryGetValue(reference.Id, out var entry))
		{
			throw new KeyNotFoundException($"Claim check with ID '{reference.Id}' not found.");
		}

		// Check expiration (lazy deletion)
		if (entry.IsExpired)
		{
			// Remove expired entry
			_ = _storage.TryRemove(reference.Id, out _);
			throw new InvalidOperationException($"Claim check with ID '{reference.Id}' has expired.");
		}

		// Validate checksum if enabled
		if (_options.ValidateChecksum && entry.Checksum != null)
		{
			var computedChecksum = ComputeChecksum(entry.Payload);
			if (computedChecksum != entry.Checksum)
			{
				throw new InvalidOperationException(
					$"Checksum validation failed for claim check '{reference.Id}'. Payload may be corrupted.");
			}
		}

		// Decompress if needed
		var payload = entry.Payload;
		if (entry.IsCompressed)
		{
			payload = DecompressPayload(entry.Payload);
		}

		return Task.FromResult(payload);
	}

	/// <inheritdoc />
	public Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		// Remove entry from storage (ConcurrentDictionary.TryRemove is thread-safe)
		var removed = _storage.TryRemove(reference.Id, out _);
		return Task.FromResult(removed);
	}

	/// <inheritdoc />
	public bool ShouldUseClaimCheck(byte[] payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		return payload.Length >= _options.PayloadThreshold;
	}

	/// <inheritdoc />
	public Task<int> CleanupExpiredAsync(int batchSize, CancellationToken cancellationToken)
	{
		var removedCount = 0;
		var now = DateTimeOffset.UtcNow;

		// Snapshot keys to avoid enumeration issues during concurrent modifications
		var keys = _storage.Keys.ToArray();

		foreach (var key in keys)
		{
			if (removedCount >= batchSize)
			{
				break;
			}

			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			if (_storage.TryGetValue(key, out var entry) && entry.ExpiresAt.HasValue && now >= entry.ExpiresAt.Value)
			{
				if (_storage.TryRemove(key, out _))
				{
					removedCount++;
				}
			}
		}

		return Task.FromResult(removedCount);
	}

	/// <summary>
	/// Removes all expired entries from storage (internal for cleanup service).
	/// </summary>
	/// <returns>The number of entries removed.</returns>
	internal int RemoveExpiredEntries()
	{
		var removedCount = 0;
		var now = DateTimeOffset.UtcNow;

		// Snapshot keys to avoid enumeration issues during concurrent modifications
		var keys = _storage.Keys.ToArray();

		foreach (var key in keys)
		{
			if (_storage.TryGetValue(key, out var entry) && entry.ExpiresAt.HasValue && now >= entry.ExpiresAt.Value)
			{
				if (_storage.TryRemove(key, out _))
				{
					removedCount++;
				}
			}
		}

		return removedCount;
	}

	/// <summary>
	/// Clears all entries from storage (internal for testing).
	/// </summary>
	internal void ClearAll()
	{
		_storage.Clear();
	}

	// Decompression helper
	private static byte[] DecompressPayload(byte[] compressedPayload)
	{
		using var inputStream = new MemoryStream(compressedPayload);
		using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
		using var outputStream = new MemoryStream();
		gzipStream.CopyTo(outputStream);
		return outputStream.ToArray();
	}

	// Checksum computation helper
	private static string ComputeChecksum(byte[] data)
	{
		var hash = SHA256.HashData(data);
		return Convert.ToBase64String(hash);
	}

	// Compression helper
	private byte[] CompressPayload(byte[] payload)
	{
		using var outputStream = new MemoryStream();
		using (var gzipStream = new GZipStream(outputStream, _options.CompressionLevel, leaveOpen: true))
		{
			gzipStream.Write(payload, 0, payload.Length);
		}

		return outputStream.ToArray();
	}
}
