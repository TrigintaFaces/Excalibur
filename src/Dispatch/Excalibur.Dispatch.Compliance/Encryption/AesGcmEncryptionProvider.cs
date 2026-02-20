// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Internal interface for providers that can supply key material to trusted encryption implementations.
/// </summary>
/// <remarks>
/// This interface is internal to prevent external code from accessing key material directly.
/// Only trusted encryption provider implementations should use this interface.
/// </remarks>
internal interface IKeyMaterialProvider
{
	/// <summary>
	/// Retrieves the raw key material for a specific key version.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="version">The key version.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The raw key bytes. Caller is responsible for secure disposal.</returns>
	Task<byte[]> GetKeyMaterialAsync(string keyId, int version, CancellationToken cancellationToken);
}

/// <summary>
/// Provides AES-256-GCM authenticated encryption.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses .NET's <see cref="AesGcm"/> class which provides
/// FIPS 140-2 compliant AES-GCM when running on a FIPS-enabled operating system.
/// </para>
/// <para>
/// Key characteristics:
/// <list type="bullet">
/// <item>256-bit keys (32 bytes)</item>
/// <item>96-bit nonces (12 bytes) - cryptographically random per operation</item>
/// <item>128-bit authentication tags (16 bytes)</item>
/// <item>Support for associated authenticated data (AAD)</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class AesGcmEncryptionProvider : IEncryptionProvider, IDisposable
{
	private const int NonceSizeBytes = 12; // GCM standard nonce
	private const int TagSizeBytes = 16; // 128-bit auth tag

	private static readonly CompositeFormat UnsupportedAlgorithmFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_UnsupportedAlgorithm);

	private static readonly CompositeFormat InvalidNonceSizeFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_InvalidNonceSize);

	private static readonly CompositeFormat KeyStatusNotAllowedForDecryptionFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_KeyStatusNotAllowedForDecryption);

	private static readonly CompositeFormat KeyStatusNotAllowedForEncryptionFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_KeyStatusNotAllowedForEncryption);

	private readonly IKeyManagementProvider _keyManagement;
	private readonly ILogger<AesGcmEncryptionProvider> _logger;
	private readonly AesGcmEncryptionOptions _options;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AesGcmEncryptionProvider"/> class.
	/// </summary>
	/// <param name="keyManagement">The key management provider for key retrieval.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	/// <param name="options">Optional configuration options.</param>
	public AesGcmEncryptionProvider(
		IKeyManagementProvider keyManagement,
		ILogger<AesGcmEncryptionProvider> logger,
		AesGcmEncryptionOptions? options = null)
	{
		_keyManagement = keyManagement ?? throw new ArgumentNullException(nameof(keyManagement));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? new AesGcmEncryptionOptions();
	}

	/// <inheritdoc/>
	public async Task<EncryptedData> EncryptAsync(
		byte[] plaintext,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(plaintext);

		// Get the key to use for encryption
		var keyMetadata = await ResolveEncryptionKeyAsync(context, cancellationToken).ConfigureAwait(false);
		var keyMaterial = await GetKeyMaterialAsync(keyMetadata, cancellationToken).ConfigureAwait(false);

		try
		{
			// Validate FIPS compliance if required
			var requireFipsCompliance =
				context.RequireFipsCompliance || _options.RequireFipsComplianceByDefault;
			if (requireFipsCompliance && !keyMetadata.IsFipsCompliant)
			{
				throw new EncryptionException(Resources.AesGcmEncryptionProvider_FipsComplianceRequired)
				{
					ErrorCode = EncryptionErrorCode.FipsComplianceViolation
				};
			}

			// Generate a cryptographically random nonce
			var nonce = new byte[NonceSizeBytes];
			RandomNumberGenerator.Fill(nonce);

			// Prepare ciphertext and authentication tag buffers
			var ciphertext = new byte[plaintext.Length];
			var tag = new byte[TagSizeBytes];

			// Build AAD from context (tenant isolation + user-provided AAD)
			var aad = BuildAssociatedData(context, keyMetadata.KeyId, keyMetadata.Version);

			// Perform authenticated encryption
			using var aesGcm = new AesGcm(keyMaterial, TagSizeBytes);
			aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, aad);

			LogEncryptionSucceeded(plaintext.Length, keyMetadata.KeyId, keyMetadata.Version);

			return new EncryptedData
			{
				Ciphertext = ciphertext,
				KeyId = keyMetadata.KeyId,
				KeyVersion = keyMetadata.Version,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = nonce,
				AuthTag = tag,
				EncryptedAt = DateTimeOffset.UtcNow,
				TenantId = context.TenantId
			};
		}
		finally
		{
			// Securely clear key material from memory
			CryptographicOperations.ZeroMemory(keyMaterial);
		}
	}

	/// <inheritdoc/>
	public async Task<byte[]> DecryptAsync(
		EncryptedData encryptedData,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(encryptedData);

		// Validate algorithm
		if (encryptedData.Algorithm != EncryptionAlgorithm.Aes256Gcm)
		{
			throw new EncryptionException(
				string.Format(
					CultureInfo.InvariantCulture,
					UnsupportedAlgorithmFormat,
					encryptedData.Algorithm))
			{ ErrorCode = EncryptionErrorCode.UnsupportedAlgorithm };
		}

		// Validate required fields
		if (encryptedData.AuthTag is null || encryptedData.AuthTag.Length != TagSizeBytes)
		{
			throw new EncryptionException(Resources.AesGcmEncryptionProvider_InvalidAuthTag)
			{
				ErrorCode = EncryptionErrorCode.InvalidCiphertext
			};
		}

		if (encryptedData.Iv.Length != NonceSizeBytes)
		{
			throw new EncryptionException(
				string.Format(
					CultureInfo.InvariantCulture,
					InvalidNonceSizeFormat,
					NonceSizeBytes))
			{ ErrorCode = EncryptionErrorCode.InvalidCiphertext };
		}

		// Get the specific key version used for encryption
		var keyMetadata = await _keyManagement.GetKeyVersionAsync(
							  encryptedData.KeyId,
							  encryptedData.KeyVersion,
							  cancellationToken).ConfigureAwait(false)
						  ?? throw new EncryptionException(Resources.AesGcmEncryptionProvider_EncryptionKeyNotFound)
						  {
							  ErrorCode = EncryptionErrorCode.KeyNotFound
						  };

		// Check key status allows decryption
		if (keyMetadata.Status is KeyStatus.Destroyed or KeyStatus.Suspended)
		{
			var errorCode = keyMetadata.Status == KeyStatus.Destroyed
				? EncryptionErrorCode.KeyNotFound
				: EncryptionErrorCode.KeySuspended;

			throw new EncryptionException(
				string.Format(
					CultureInfo.InvariantCulture,
					KeyStatusNotAllowedForDecryptionFormat,
					keyMetadata.Status))
			{ ErrorCode = errorCode };
		}

		// Validate FIPS compliance if required
		var requireFipsCompliance =
			context.RequireFipsCompliance || _options.RequireFipsComplianceByDefault;
		if (requireFipsCompliance && !keyMetadata.IsFipsCompliant)
		{
			throw new EncryptionException(Resources.AesGcmEncryptionProvider_FipsComplianceRequired)
			{
				ErrorCode = EncryptionErrorCode.FipsComplianceViolation
			};
		}

		var keyMaterial = await GetKeyMaterialAsync(keyMetadata, cancellationToken).ConfigureAwait(false);

		try
		{
			// Rebuild AAD for verification
			var aad = BuildAssociatedData(context, encryptedData.KeyId, encryptedData.KeyVersion);

			// Prepare plaintext buffer
			var plaintext = new byte[encryptedData.Ciphertext.Length];

			// Perform authenticated decryption
			using var aesGcm = new AesGcm(keyMaterial, TagSizeBytes);

			try
			{
				aesGcm.Decrypt(encryptedData.Iv, encryptedData.Ciphertext, encryptedData.AuthTag, plaintext, aad);
			}
			catch (AuthenticationTagMismatchException ex)
			{
				throw new EncryptionException(Resources.AesGcmEncryptionProvider_AuthenticationFailed, ex)
				{
					ErrorCode = EncryptionErrorCode.AuthenticationFailed
				};
			}

			LogDecryptionSucceeded(
				encryptedData.Ciphertext.Length,
				encryptedData.KeyId,
				encryptedData.KeyVersion);

			return plaintext;
		}
		finally
		{
			// Securely clear key material from memory
			CryptographicOperations.ZeroMemory(keyMaterial);
		}
	}

	/// <inheritdoc/>
	public Task<bool> ValidateFipsComplianceAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Check if the system is running in FIPS mode
		// On Windows, this checks the registry setting; on Linux, it checks /proc/sys/crypto/fips_enabled
		try
		{
			// .NET's AesGcm uses platform-provided cryptography
			// On FIPS-enabled systems, it uses FIPS-validated modules
			var isFipsEnabled = CryptoConfig.AllowOnlyFipsAlgorithms;

			LogFipsComplianceValidated(isFipsEnabled);

			return Task.FromResult(isFipsEnabled);
		}
		catch (Exception ex)
		{
			LogFipsComplianceValidationFailed(ex);
			return Task.FromResult(false);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	private async Task<KeyMetadata> ResolveEncryptionKeyAsync(
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		KeyMetadata? keyMetadata;

		if (!string.IsNullOrEmpty(context.KeyId))
		{
			// Use specific key if requested
			if (context.KeyVersion.HasValue)
			{
				keyMetadata = await _keyManagement.GetKeyVersionAsync(
					context.KeyId,
					context.KeyVersion.Value,
					cancellationToken).ConfigureAwait(false);
			}
			else
			{
				keyMetadata = await _keyManagement.GetKeyAsync(
					context.KeyId,
					cancellationToken).ConfigureAwait(false);
			}
		}
		else
		{
			// Get the active key for the specified purpose
			var purpose = string.IsNullOrWhiteSpace(context.Purpose)
				? _options.DefaultPurpose
				: context.Purpose;
			keyMetadata = await _keyManagement.GetActiveKeyAsync(
				purpose,
				cancellationToken).ConfigureAwait(false);
		}

		if (keyMetadata is null)
		{
			throw new EncryptionException(Resources.AesGcmEncryptionProvider_NoSuitableKeyFound)
			{
				ErrorCode = EncryptionErrorCode.KeyNotFound
			};
		}

		// Verify key is active for encryption
		if (keyMetadata.Status != KeyStatus.Active)
		{
			throw new EncryptionException(
				string.Format(
					CultureInfo.InvariantCulture,
					KeyStatusNotAllowedForEncryptionFormat,
					keyMetadata.Status))
			{
				ErrorCode = keyMetadata.Status switch
				{
					KeyStatus.DecryptOnly or KeyStatus.PendingDestruction => EncryptionErrorCode.KeyExpired,
					KeyStatus.Suspended => EncryptionErrorCode.KeySuspended,
					_ => EncryptionErrorCode.Unknown
				}
			};
		}

		// Check expiration
		if (keyMetadata.ExpiresAt.HasValue && keyMetadata.ExpiresAt.Value <= DateTimeOffset.UtcNow)
		{
			throw new EncryptionException(Resources.AesGcmEncryptionProvider_KeyExpired) { ErrorCode = EncryptionErrorCode.KeyExpired };
		}

		return keyMetadata;
	}

	private Task<byte[]> GetKeyMaterialAsync(KeyMetadata keyMetadata, CancellationToken cancellationToken)
	{
		// This is a placeholder - in a real implementation, key material would come from:
		// - Cloud KMS (AWS KMS, Azure Key Vault, Google Cloud KMS)
		// - HSM
		// - Secure key store
		//
		// The IKeyManagementProvider interface is intentionally designed to NOT expose
		// key material directly. A separate internal interface or derived type would
		// provide key material access to trusted encryption providers.
		//
		// For the InMemoryKeyManagementProvider used in development/testing,
		// we'll use a derived interface.

		if (_keyManagement is IKeyMaterialProvider keyMaterialProvider)
		{
			return keyMaterialProvider.GetKeyMaterialAsync(keyMetadata.KeyId, keyMetadata.Version, cancellationToken);
		}

		throw new EncryptionException(Resources.AesGcmEncryptionProvider_KeyMaterialUnavailable)
		{
			ErrorCode = EncryptionErrorCode.ServiceUnavailable
		};
	}

	[LoggerMessage(LogLevel.Debug, "Encrypted {PlaintextSize} bytes using key {KeyId} v{Version}")]
	private partial void LogEncryptionSucceeded(int plaintextSize, string keyId, int version);

	[LoggerMessage(LogLevel.Debug, "Decrypted {CiphertextSize} bytes using key {KeyId} v{Version}")]
	private partial void LogDecryptionSucceeded(int ciphertextSize, string keyId, int version);

	[LoggerMessage(LogLevel.Information, "FIPS 140-2 compliance validation: {IsFipsEnabled}")]
	private partial void LogFipsComplianceValidated(bool isFipsEnabled);

	[LoggerMessage(LogLevel.Warning, "Unable to determine FIPS compliance status")]
	private partial void LogFipsComplianceValidationFailed(Exception exception);

	private byte[] BuildAssociatedData(EncryptionContext context, string keyId, int keyVersion)
	{
		// Build AAD from multiple sources for maximum binding
		// Format: [keyId|keyVersion|tenantId?|userAAD?]
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);

		// Always include key identifier for binding
		writer.Write(keyId);
		writer.Write(keyVersion);

		// Include tenant ID if present (multi-tenant isolation)
		if (!string.IsNullOrEmpty(context.TenantId))
		{
			writer.Write(context.TenantId);
		}

		// Include user-provided AAD if present
		if (context.AssociatedData is { Length: > 0 })
		{
			writer.Write(context.AssociatedData.Length);
			writer.Write(context.AssociatedData);
		}

		return ms.ToArray();
	}
}

/// <summary>
/// Configuration options for the AES-GCM encryption provider.
/// </summary>
public sealed class AesGcmEncryptionOptions
{
	/// <summary>
	/// Gets or sets the default purpose for key selection when not specified in context.
	/// </summary>
	public string? DefaultPurpose { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether FIPS compliance is required by default.
	/// </summary>
	public bool RequireFipsComplianceByDefault { get; set; }
}
