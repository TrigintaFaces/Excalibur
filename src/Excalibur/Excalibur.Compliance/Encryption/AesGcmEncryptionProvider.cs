// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Encryption;

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
	private const int DataKeySizeBytes = 32; // AES-256 data encryption key

	private static readonly CompositeFormat UnsupportedAlgorithmFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_UnsupportedAlgorithm);

	private static readonly CompositeFormat InvalidNonceSizeFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_InvalidNonceSize);

	private static readonly CompositeFormat KeyStatusNotAllowedForDecryptionFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_KeyStatusNotAllowedForDecryption);

	private static readonly CompositeFormat KeyStatusNotAllowedForEncryptionFormat =
		CompositeFormat.Parse(Resources.AesGcmEncryptionProvider_KeyStatusNotAllowedForEncryption);

	private readonly IKeyManagementProvider _keyManagement;
	private readonly IKeyMaterialProvider? _keyMaterial;
	private readonly IKeyWrappingProvider? _keyWrapping;
	private readonly ILogger<AesGcmEncryptionProvider> _logger;
	private readonly AesGcmEncryptionOptions _options;
	private readonly IFipsDetector _fipsDetector;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AesGcmEncryptionProvider"/> class.
	/// </summary>
	/// <param name="keyManagement">The key management provider for key retrieval.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	/// <param name="options">Optional configuration options.</param>
	/// <param name="fipsDetector">
	/// Optional FIPS detector used to answer <see cref="ValidateFipsComplianceAsync" />. When omitted the
	/// provider uses the default detector, which inspects the host operating system.
	/// </param>
	public AesGcmEncryptionProvider(
		IKeyManagementProvider keyManagement,
		ILogger<AesGcmEncryptionProvider> logger,
		AesGcmEncryptionOptions? options = null,
		IFipsDetector? fipsDetector = null)
	{
		_keyManagement = keyManagement ?? throw new ArgumentNullException(nameof(keyManagement));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? new AesGcmEncryptionOptions();
		_fipsDetector = fipsDetector ?? new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

		// Resolve the two key paths this provider can drive. Raw key material is the direct path; key
		// wrapping is the envelope path, which is the one a cloud KMS or HSM can serve, because it never
		// asks the key service to export key bytes.
		//
		// Material is resolved by cast because it is an internal, same-assembly capability. Wrapping is a
		// public optional capability resolved through GetService, so it stays discoverable when the
		// provider sits behind a decorator that forwards capability queries.
		_keyMaterial = keyManagement as IKeyMaterialProvider;
		_keyWrapping = keyManagement.GetService(typeof(IKeyWrappingProvider)) as IKeyWrappingProvider;

		// Validate at DI construction time that at least one key path exists, so a misconfiguration is a
		// startup failure rather than a cryptic EncryptionException on first use -- or, far worse, a
		// deployment that appears to work until the moment it has to decrypt.
		if (_keyMaterial is null && _keyWrapping is null)
		{
			throw new InvalidOperationException(
				$"The registered IKeyManagementProvider ({keyManagement.GetType().Name}) supplies neither raw " +
				$"key material nor key wrapping, so AesGcmEncryptionProvider has no key to encrypt with. A " +
				$"provider must either expose key material for direct encryption, or implement " +
				$"IKeyWrappingProvider so a locally generated data key can be wrapped by the key service. " +
				$"Cloud KMS and HSM-backed providers supply the latter; they deliberately do not export key " +
				$"bytes. An in-process provider that supplies key material does not survive a restart, so it " +
				$"is not suitable for data that must still be decryptable afterwards.");
		}
	}

	/// <inheritdoc/>
	public async Task<EncryptedData> EncryptAsync(
		byte[] plaintext,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(plaintext);

		// Resolve key and material, then validate status atomically to minimize TOCTOU window.
		// Key status is checked AFTER material retrieval so revocation between resolve and
		// material fetch is caught before any encryption occurs.
		var keyMetadata = await ResolveEncryptionKeyAsync(context, cancellationToken).ConfigureAwait(false);
		var (keyMaterial, wrappedKey) = await AcquireEncryptionKeyAsync(keyMetadata, cancellationToken)
			.ConfigureAwait(false);

		// Re-validate key status after material retrieval to close TOCTOU gap
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

		if (keyMetadata.ExpiresAt.HasValue && keyMetadata.ExpiresAt.Value <= DateTimeOffset.UtcNow)
		{
			throw new EncryptionException(Resources.AesGcmEncryptionProvider_KeyExpired) { ErrorCode = EncryptionErrorCode.KeyExpired };
		}

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
				WrappedKey = wrappedKey,
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

		var keyMaterial = await AcquireDecryptionKeyAsync(encryptedData, keyMetadata, cancellationToken)
			.ConfigureAwait(false);

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

		// Ask the detector, which inspects the host: the Windows FIPS policy in the registry, or the Linux
		// kernel parameter. This deliberately does not consult CryptoConfig.AllowOnlyFipsAlgorithms, which
		// .NET Core and later hardcode to false regardless of the host, so that a genuinely FIPS-enabled
		// deployment is reported as compliant instead of being told it is not.
		try
		{
			var isFipsEnabled = _fipsDetector.IsFipsEnabled;

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

	/// <summary>
	/// Obtains the key to encrypt with, and the wrapped form to persist alongside the ciphertext when the
	/// envelope path is used.
	/// </summary>
	/// <remarks>
	/// The direct path is preferred when the provider exposes key material, so a provider that supplies it
	/// keeps producing exactly what it produced before. The envelope path is used otherwise: a single-use
	/// data key is generated from a cryptographic random source and handed to the key service to be
	/// wrapped, which is the only path a KMS can serve without exporting its key.
	/// </remarks>
	private async Task<(byte[] Material, WrappedDataKey? Wrapped)> AcquireEncryptionKeyAsync(
		KeyMetadata keyMetadata,
		CancellationToken cancellationToken)
	{
		if (_keyMaterial is not null)
		{
			var material = await _keyMaterial
				.GetKeyMaterialAsync(keyMetadata.KeyId, keyMetadata.Version, cancellationToken)
				.ConfigureAwait(false);

			return (material, null);
		}

		if (_keyWrapping is null)
		{
			throw new EncryptionException(Resources.AesGcmEncryptionProvider_KeyMaterialUnavailable)
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}

		// A fresh data key per payload, from the platform CSPRNG. Never a GUID or a general-purpose RNG.
		var dataKey = RandomNumberGenerator.GetBytes(DataKeySizeBytes);

		try
		{
			var wrapped = await _keyWrapping
				.WrapDataKeyAsync(keyMetadata.KeyId, keyMetadata.Version, dataKey, cancellationToken)
				.ConfigureAwait(false);

			// A provider returning no wrapped bytes would leave the payload permanently unreadable, so this
			// fails closed here rather than persisting ciphertext whose key is already gone.
			if (wrapped is null || wrapped.CiphertextBlob.Length == 0)
			{
				throw new EncryptionException(
					"The key wrapping provider returned an empty wrapped data key. Encryption was abandoned " +
					"because the resulting ciphertext could never be decrypted.")
				{
					ErrorCode = EncryptionErrorCode.ServiceUnavailable
				};
			}

			return (dataKey, wrapped);
		}
		catch
		{
			CryptographicOperations.ZeroMemory(dataKey);
			throw;
		}
	}

	/// <summary>
	/// Obtains the key to decrypt with, selecting the scheme recorded on the payload itself.
	/// </summary>
	/// <remarks>
	/// The scheme is read from the data rather than from the current wiring, so a payload stays readable
	/// after the key path is reconfigured.
	/// </remarks>
	private async Task<byte[]> AcquireDecryptionKeyAsync(
		EncryptedData encryptedData,
		KeyMetadata keyMetadata,
		CancellationToken cancellationToken)
	{
		if (encryptedData.WrappedKey is null)
		{
			return await GetKeyMaterialAsync(keyMetadata, cancellationToken).ConfigureAwait(false);
		}

		if (_keyWrapping is null)
		{
			throw new EncryptionException(
				"This payload was encrypted under an envelope, but the registered key management provider " +
				"does not implement IKeyWrappingProvider, so its data key cannot be unwrapped. Register the " +
				"key service that wrapped it.")
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}

		var dataKey = await _keyWrapping
			.UnwrapDataKeyAsync(
				encryptedData.KeyId,
				encryptedData.KeyVersion,
				encryptedData.WrappedKey,
				cancellationToken)
			.ConfigureAwait(false);

		if (dataKey is null || dataKey.Length == 0)
		{
			throw new EncryptionException(
				"The key wrapping provider returned an empty data key when unwrapping. Decryption was " +
				"abandoned rather than attempted with a substituted key.")
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}

		return dataKey;
	}

	private Task<byte[]> GetKeyMaterialAsync(KeyMetadata keyMetadata, CancellationToken cancellationToken)
	{
		if (_keyMaterial is not null)
		{
			return _keyMaterial.GetKeyMaterialAsync(keyMetadata.KeyId, keyMetadata.Version, cancellationToken);
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
		// Format: [keyId|keyVersion|tenantIdLength|tenantId?|userAADLength|userAAD?]
		// All fields are length-prefixed to prevent format ambiguity across tenants.
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);

		// Always include key identifier for binding
		writer.Write(keyId);
		writer.Write(keyVersion);

		// Always include tenant ID field with length prefix for unambiguous AAD format.
		// This prevents cross-tenant AAD ambiguity where null/empty tenant would produce
		// identical AAD as no-tenant, potentially allowing cross-tenant decryption.
		var tenantId = context.TenantId ?? string.Empty;
		writer.Write(tenantId.Length);
		if (tenantId.Length > 0)
		{
			writer.Write(tenantId);
		}

		// Include user-provided AAD if present (always length-prefixed)
		if (context.AssociatedData is { Length: > 0 })
		{
			writer.Write(context.AssociatedData.Length);
			writer.Write(context.AssociatedData);
		}
		else
		{
			writer.Write(0);
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
