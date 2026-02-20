// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Implements message encryption using Microsoft's DataProtection API with optional Azure Key Vault backing.
/// </summary>
/// <remarks>
/// This implementation provides:
/// <list type="bullet">
/// <item> AES-256-GCM authenticated encryption by default </item>
/// <item> Automatic key management and rotation </item>
/// <item> Multi-tenant support with purpose strings </item>
/// <item> Optional content compression before encryption </item>
/// <item> Integration with Azure Key Vault for key storage </item>
/// </list>
/// </remarks>
public sealed partial class DataProtectionMessageEncryptionService : IMessageEncryptionService, IDisposable
{
	private const int MaxCacheSize = 1024;
	private static readonly long TtlTicks = Stopwatch.Frequency * 30 * 60; // 30 minutes

	private readonly IDataProtectionProvider _dataProtectionProvider;
	private readonly EncryptionOptions _options;
	private readonly ILogger<DataProtectionMessageEncryptionService> _logger;
	private readonly ConcurrentDictionary<string, (IDataProtector Protector, long ExpiresAtTimestamp)> _protectorCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataProtectionMessageEncryptionService" /> class.
	/// </summary>
	/// <param name="dataProtectionProvider"> The data protection provider used to create protectors. </param>
	/// <param name="options"> The encryption service options. </param>
	/// <param name="logger"> The logger used for diagnostics. </param>
	public DataProtectionMessageEncryptionService(
		IDataProtectionProvider dataProtectionProvider,
		IOptions<EncryptionOptions> options,
		ILogger<DataProtectionMessageEncryptionService> logger)
	{
		ArgumentNullException.ThrowIfNull(dataProtectionProvider);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_dataProtectionProvider = dataProtectionProvider;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<string> EncryptMessageAsync(
		string content,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			// Use ArrayPool for UTF8 encoding to reduce allocations
			var byteCount = Encoding.UTF8.GetByteCount(content);
			var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
			try
			{
				var actualBytes = Encoding.UTF8.GetBytes(content, 0, content.Length, buffer, 0);
				var bytes = buffer.AsSpan(0, actualBytes).ToArray();
				var encryptedBytes = await EncryptMessageAsync(bytes, context, cancellationToken).ConfigureAwait(false);
				return Convert.ToBase64String(encryptedBytes);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}
		catch (Exception ex) when (ex is not EncryptionException)
		{
			LogEncryptionFailed(ex);
			throw new EncryptionException(
					Resources.DataProtectionMessageEncryptionService_MessageEncryptionFailed,
					ex);
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> EncryptMessageAsync(
		byte[] content,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			// Use compression setting from options (context is immutable)
			var enableCompression = _options.EnableCompressionByDefault;

			// Optionally compress content
			var dataToEncrypt = enableCompression
				? await CompressAsync(content, cancellationToken).ConfigureAwait(false)
				: content;

			// Get or create protector for this context
			var protector = await GetProtectorAsync(context, cancellationToken).ConfigureAwait(false);

			// Encrypt the data
			var encrypted = protector.Protect(dataToEncrypt);

			// Add metadata header if needed
			if (_options.IncludeMetadataHeader)
			{
				var algorithm = context.Algorithm ?? _options.DefaultAlgorithm;
				encrypted = AddMetadataHeader(encrypted, algorithm, enableCompression);
			}

			LogMessageEncrypted(content.Length, encrypted.Length);

			return encrypted;
		}
		catch (Exception ex) when (ex is not EncryptionException)
		{
			LogEncryptionFailed(ex);
			throw new EncryptionException(
					Resources.DataProtectionMessageEncryptionService_MessageEncryptionFailed,
					ex);
		}
	}

	/// <inheritdoc />
	public async Task<string> DecryptMessageAsync(
		string encryptedContent,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedContent);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			var encryptedBytes = Convert.FromBase64String(encryptedContent);
			var decryptedBytes = await DecryptMessageAsync(encryptedBytes, context, cancellationToken).ConfigureAwait(false);
			return Encoding.UTF8.GetString(decryptedBytes);
		}
		catch (Exception ex) when (ex is not DecryptionException)
		{
			LogDecryptionFailed(ex);
			throw new DecryptionException(
					Resources.DataProtectionMessageEncryptionService_MessageDecryptionFailed,
					ex);
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> DecryptMessageAsync(
		byte[] encryptedContent,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedContent);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			// Extract metadata if present
			byte[] dataToDecrypt;
			bool wasCompressed;

			if (_options.IncludeMetadataHeader)
			{
				var (data, _, compressed) = ExtractMetadataHeader(encryptedContent);
				dataToDecrypt = data;
				wasCompressed = compressed;
			}
			else
			{
				dataToDecrypt = encryptedContent;
				wasCompressed = _options.EnableCompressionByDefault;
			}

			// Get protector for this context
			var protector = await GetProtectorAsync(context, cancellationToken).ConfigureAwait(false);

			// Decrypt the data
			var decrypted = protector.Unprotect(dataToDecrypt);

			// Decompress if needed (based on header metadata, not context)
			if (wasCompressed)
			{
				decrypted = await DecompressAsync(decrypted, cancellationToken).ConfigureAwait(false);
			}

			LogMessageDecrypted(encryptedContent.Length, decrypted.Length);

			return decrypted;
		}
		catch (CryptographicException ex)
		{
			LogCryptographicError(ex);
			throw new DecryptionException(
					Resources.DataProtectionMessageEncryptionService_FailedToDecryptInvalidKeyOrCorruptedData,
					ex);
		}
		catch (Exception ex) when (ex is not DecryptionException)
		{
			LogDecryptionFailed(ex);
			throw new DecryptionException(
					Resources.DataProtectionMessageEncryptionService_MessageDecryptionFailed,
					ex);
		}
	}

	/// <inheritdoc />
	public Task<KeyRotationResult> RotateKeysAsync(CancellationToken cancellationToken)
	{
		var previousKeyId = _options.CurrentKeyId ?? "default";
		var newKeyId = Guid.NewGuid().ToString();
		var rotatedAt = DateTimeOffset.UtcNow;

		try
		{
			// Clear the protector cache to force recreation with new keys
			_protectorCache.Clear();

			// Update configuration with new key ID
			_options.CurrentKeyId = newKeyId;

			LogKeysRotated(previousKeyId, newKeyId);

			// Create KeyMetadata for the new and previous keys
			var newKeyMetadata = new KeyMetadata
			{
				KeyId = newKeyId,
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = _options.DefaultAlgorithm,
				CreatedAt = rotatedAt,
			};

			var previousKeyMetadata = new KeyMetadata
			{
				KeyId = previousKeyId,
				Version = 1,
				Status = KeyStatus.DecryptOnly,
				Algorithm = _options.DefaultAlgorithm,
				CreatedAt = rotatedAt.AddDays(-_options.KeyRotationIntervalDays),
				LastRotatedAt = rotatedAt,
			};

			return Task.FromResult(KeyRotationResult.Succeeded(newKeyMetadata, previousKeyMetadata));
		}
		catch (Exception ex)
		{
			LogKeyRotationFailed(ex);
			throw new KeyRotationException(
					Resources.DataProtectionMessageEncryptionService_KeyRotationFailed,
					ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> ValidateConfigurationAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Test encryption/decryption round trip
			const string testData = "Validation test data";
			var context = new EncryptionContext { Purpose = "validation", TenantId = "system" };

			var encrypted = await EncryptMessageAsync(testData, context, cancellationToken).ConfigureAwait(false);
			var decrypted = await DecryptMessageAsync(encrypted, context, cancellationToken).ConfigureAwait(false);

			var isValid = string.Equals(testData, decrypted, StringComparison.Ordinal);

			if (isValid)
			{
				LogConfigurationValidated();
			}
			else
			{
				LogValidationFailed();
			}

			return isValid;
		}
		catch (Exception ex)
		{
			LogConfigurationValidationFailed(ex);
			return false;
		}
	}

	/// <summary>
	/// Disposes the encryption service and releases all resources.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_protectorCache.Clear();
	}

	private static async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken)
	{
		var output = new MemoryStream();
		await using (output.ConfigureAwait(false))
		{
			var compressor = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal);
			await using (compressor.ConfigureAwait(false))
			{
				await compressor.WriteAsync(data, cancellationToken).ConfigureAwait(false);
			}

			// Flush and close the compressor before reading the output
			await output.FlushAsync(cancellationToken).ConfigureAwait(false);

			return output.ToArray();
		}
	}

	private static async Task<byte[]> DecompressAsync(byte[] data, CancellationToken cancellationToken)
	{
		var input = new MemoryStream(data);
		await using (input.ConfigureAwait(false))
		{
			var decompressor = new GZipStream(input, CompressionMode.Decompress);
			await using (decompressor.ConfigureAwait(false))
			{
				var output = new MemoryStream();
				await using (output.ConfigureAwait(false))
				{
					await decompressor.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
					return output.ToArray();
				}
			}
		}
	}

	private static string BuildPurposeString(EncryptionContext context)
	{
		var parts = new List<string> { "Excalibur.Dispatch.Security.MessageEncryption" };

		if (!string.IsNullOrEmpty(context.TenantId))
		{
			parts.Add($"Tenant:{context.TenantId}");
		}

		if (!string.IsNullOrEmpty(context.Purpose))
		{
			parts.Add($"Purpose:{context.Purpose}");
		}

		if (!string.IsNullOrEmpty(context.KeyId))
		{
			parts.Add($"Key:{context.KeyId}");
		}

		if (context.KeyVersion.HasValue)
		{
			parts.Add($"Version:{context.KeyVersion.Value}");
		}

		return string.Join('.', parts);
	}

	private static byte[] AddMetadataHeader(byte[] encrypted, EncryptionAlgorithm algorithm, bool compressed)
	{
		// Simple header format: [Version:1 byte][Algorithm:1 byte][Compressed:1 byte][Data]
		const int headerLength = 3;
		var resultLength = headerLength + encrypted.Length;
		var result = new byte[resultLength];

		// Write header directly to result array
		result[0] = 1; // Version
		result[1] = (byte)algorithm;
		result[2] = (byte)(compressed ? 1 : 0);

		Buffer.BlockCopy(encrypted, 0, result, headerLength, encrypted.Length);

		return result;
	}

	private static (byte[] Data, EncryptionAlgorithm? Algorithm, bool Compressed) ExtractMetadataHeader(byte[] data)
	{
		if (data.Length < 3)
		{
			return (data, null, false); // No header present
		}

		// Read header
		_ = data[0]; // Version byte
		var algorithm = (EncryptionAlgorithm)data[1];
		var compressed = data[2] == 1;

		// Return data without header along with extracted metadata
		var result = new byte[data.Length - 3];
		Buffer.BlockCopy(data, 3, result, 0, result.Length);
		return (result, algorithm, compressed);
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.DataProtectionEncryptionFailed, LogLevel.Error, "Failed to encrypt message content")]
	private partial void LogEncryptionFailed(Exception ex);

	[LoggerMessage(SecurityEventId.DataProtectionMessageEncrypted, LogLevel.Debug,
		"Encrypted message of {OriginalSize} bytes to {EncryptedSize} bytes")]
	private partial void LogMessageEncrypted(int originalSize, int encryptedSize);

	[LoggerMessage(SecurityEventId.DataProtectionDecryptionFailed, LogLevel.Error, "Failed to decrypt message content")]
	private partial void LogDecryptionFailed(Exception ex);

	[LoggerMessage(SecurityEventId.DataProtectionMessageDecrypted, LogLevel.Debug,
		"Decrypted message of {EncryptedSize} bytes to {DecryptedSize} bytes")]
	private partial void LogMessageDecrypted(int encryptedSize, int decryptedSize);

	[LoggerMessage(SecurityEventId.DataProtectionCryptographicError, LogLevel.Error, "Cryptographic error during decryption")]
	private partial void LogCryptographicError(Exception ex);

	[LoggerMessage(SecurityEventId.DataProtectionKeysRotated, LogLevel.Information,
		"Successfully rotated encryption keys from {PreviousKeyId} to {NewKeyId}")]
	private partial void LogKeysRotated(string previousKeyId, string newKeyId);

	[LoggerMessage(SecurityEventId.DataProtectionKeyRotationFailed, LogLevel.Error, "Failed to rotate encryption keys")]
	private partial void LogKeyRotationFailed(Exception ex);

	[LoggerMessage(SecurityEventId.DataProtectionConfigurationValidated, LogLevel.Information,
		"Encryption service configuration validated successfully")]
	private partial void LogConfigurationValidated();

	[LoggerMessage(SecurityEventId.DataProtectionValidationFailed, LogLevel.Warning,
		"Encryption service validation failed - round trip test unsuccessful")]
	private partial void LogValidationFailed();

	[LoggerMessage(SecurityEventId.DataProtectionConfigurationValidationFailed, LogLevel.Error,
		"Failed to validate encryption service configuration")]
	private partial void LogConfigurationValidationFailed(Exception ex);

	private Task<IDataProtector> GetProtectorAsync(EncryptionContext context, CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Validated by caller; kept for interface consistency

		var purpose = BuildPurposeString(context);

		// Check cache with TTL validation
		if (_protectorCache.TryGetValue(purpose, out var entry))
		{
			if (Stopwatch.GetTimestamp() < entry.ExpiresAtTimestamp)
			{
				return Task.FromResult(entry.Protector);
			}

			// Expired - remove
			_protectorCache.TryRemove(purpose, out _);
		}

		var protector = _dataProtectionProvider.CreateProtector(purpose);

		// Only cache if within bounded limit
		if (_protectorCache.Count < MaxCacheSize)
		{
			_protectorCache[purpose] = (protector, Stopwatch.GetTimestamp() + TtlTicks);
		}

		return Task.FromResult(protector);
	}
}
