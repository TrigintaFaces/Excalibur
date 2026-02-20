// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides encryption with automatic key rotation support and backward compatibility.
/// </summary>
/// <remarks>
/// <para>
/// This provider wraps an underlying <see cref="IEncryptionProvider"/> and adds:
/// <list type="bullet">
/// <item>Automatic key rotation via the key management provider</item>
/// <item>Backward compatibility - decrypts with any previous key version</item>
/// <item>Transparent re-encryption during read operations (optional)</item>
/// </list>
/// </para>
/// <para>
/// For encryption, always uses the current active key. For decryption, uses the key version
/// embedded in the <see cref="EncryptedData"/> metadata.
/// </para>
/// </remarks>
public sealed partial class RotatingEncryptionProvider : IEncryptionProvider, IDisposable
{
	private readonly IEncryptionProvider _inner;
	private readonly IKeyManagementProvider _keyManagement;
	private readonly ILogger<RotatingEncryptionProvider> _logger;
	private readonly RotatingEncryptionOptions _options;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RotatingEncryptionProvider"/> class.
	/// </summary>
	/// <param name="inner">The underlying encryption provider.</param>
	/// <param name="keyManagement">The key management provider for rotation operations.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	/// <param name="options">Optional configuration options.</param>
	public RotatingEncryptionProvider(
		IEncryptionProvider inner,
		IKeyManagementProvider keyManagement,
		ILogger<RotatingEncryptionProvider> logger,
		RotatingEncryptionOptions? options = null)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_keyManagement = keyManagement ?? throw new ArgumentNullException(nameof(keyManagement));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? new RotatingEncryptionOptions();
	}

	/// <inheritdoc/>
	public async Task<EncryptedData> EncryptAsync(
		byte[] plaintext,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(plaintext);

		// Check if rotation is needed before encryption
		if (_options.AutoRotateBeforeEncryption)
		{
			await TryRotateIfNeededAsync(context.Purpose, cancellationToken).ConfigureAwait(false);
		}

		// Delegate to inner provider (which will use the active key)
		return await _inner.EncryptAsync(plaintext, context, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<byte[]> DecryptAsync(
		EncryptedData encryptedData,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(encryptedData);

		// The inner provider will use the key version from EncryptedData
		// This provides automatic backward compatibility
		var plaintext = await _inner.DecryptAsync(encryptedData, context, cancellationToken).ConfigureAwait(false);

		// Check if we should re-encrypt with current key
		if (_options.ReEncryptOnRead && await ShouldReEncryptAsync(encryptedData, cancellationToken).ConfigureAwait(false))
		{
			LogOldKeyVersionReencryptionHint(encryptedData.KeyId, encryptedData.KeyVersion);

			// Note: Actual re-encryption would require the caller to persist the new ciphertext
			// This is informational - the caller decides whether to re-encrypt and save
		}

		return plaintext;
	}

	/// <inheritdoc/>
	public Task<bool> ValidateFipsComplianceAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _inner.ValidateFipsComplianceAsync(cancellationToken);
	}

	/// <summary>
	/// Rotates the encryption key for the specified purpose.
	/// </summary>
	/// <param name="keyId">The key identifier to rotate.</param>
	/// <param name="algorithm">The encryption algorithm for the new key.</param>
	/// <param name="purpose">Optional purpose for the key.</param>
	/// <param name="expiresAt">Optional expiration date for the new key.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result of the rotation operation.</returns>
	public Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		EncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _keyManagement.RotateKeyAsync(keyId, algorithm, purpose, expiresAt, cancellationToken);
	}

	/// <summary>
	/// Re-encrypts data with the current active key.
	/// </summary>
	/// <param name="encryptedData">The existing encrypted data.</param>
	/// <param name="context">The encryption context.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The data re-encrypted with the current active key, or the original data if already using the active key.
	/// </returns>
	public async Task<EncryptedData> ReEncryptAsync(
		EncryptedData encryptedData,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(encryptedData);

		// Check if re-encryption is needed
		if (!await ShouldReEncryptAsync(encryptedData, cancellationToken).ConfigureAwait(false))
		{
			LogAlreadyActiveKey();
			return encryptedData;
		}

		// Decrypt with old key
		var plaintext = await _inner.DecryptAsync(encryptedData, context, cancellationToken).ConfigureAwait(false);

		try
		{
			// Re-encrypt with current active key
			var newEncryptedData = await _inner.EncryptAsync(plaintext, context, cancellationToken).ConfigureAwait(false);

			LogReencryptedData(encryptedData.KeyId, encryptedData.KeyVersion, newEncryptedData.KeyId, newEncryptedData.KeyVersion);

			return newEncryptedData;
		}
		finally
		{
			// Securely clear plaintext from memory
			Array.Clear(plaintext);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		if (_inner is IDisposable disposable)
		{
			disposable.Dispose();
		}

		_disposed = true;
	}

	private async Task TryRotateIfNeededAsync(string? purpose, CancellationToken cancellationToken)
	{
		var activeKey = await _keyManagement.GetActiveKeyAsync(purpose, cancellationToken).ConfigureAwait(false);

		if (activeKey is null)
		{
			return;
		}

		// Check if key should be rotated based on age or policy
		var keyAge = DateTimeOffset.UtcNow - activeKey.CreatedAt;

		if (keyAge > _options.MaxKeyAge)
		{
			LogKeyAgeExceedsMax(activeKey.KeyId, keyAge.TotalDays, _options.MaxKeyAge.TotalDays);

			_ = await _keyManagement.RotateKeyAsync(
				activeKey.KeyId,
				activeKey.Algorithm,
				purpose,
				expiresAt: null,
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task<bool> ShouldReEncryptAsync(EncryptedData encryptedData, CancellationToken cancellationToken)
	{
		var activeKey = await _keyManagement.GetActiveKeyAsync(purpose: null, cancellationToken: cancellationToken).ConfigureAwait(false);

		if (activeKey is null)
		{
			return false;
		}

		// Re-encrypt if using different key or older version
		return encryptedData.KeyId != activeKey.KeyId || encryptedData.KeyVersion < activeKey.Version;
	}

	[LoggerMessage(ComplianceEventId.EncryptionReencryptionHint, LogLevel.Debug,
		"Data encrypted with old key version {KeyId} v{Version}, consider re-encryption")]
	private partial void LogOldKeyVersionReencryptionHint(string keyId, int version);

	[LoggerMessage(ComplianceEventId.EncryptionAlreadyActiveKey, LogLevel.Debug,
		"Data already encrypted with active key, skipping re-encryption")]
	private partial void LogAlreadyActiveKey();

	[LoggerMessage(ComplianceEventId.EncryptionReencrypted, LogLevel.Information,
		"Re-encrypted data from key {OldKeyId} v{OldVersion} to {NewKeyId} v{NewVersion}")]
	private partial void LogReencryptedData(string oldKeyId, int oldVersion, string newKeyId, int newVersion);

	[LoggerMessage(ComplianceEventId.EncryptionKeyAgeExceedsMax, LogLevel.Information,
		"Key {KeyId} age ({KeyAgeDays:F1} days) exceeds max age ({MaxAgeDays} days), initiating rotation")]
	private partial void LogKeyAgeExceedsMax(string keyId, double keyAgeDays, double maxAgeDays);
}

/// <summary>
/// Configuration options for the rotating encryption provider.
/// </summary>
public sealed class RotatingEncryptionOptions
{
	/// <summary>
	/// Gets or sets the maximum age of a key before automatic rotation.
	/// Default is 90 days per compliance requirements.
	/// </summary>
	public TimeSpan MaxKeyAge { get; set; } = TimeSpan.FromDays(90);

	/// <summary>
	/// Gets or sets a value indicating whether to check and perform rotation before each encryption.
	/// Default is false (rotation should be managed separately).
	/// </summary>
	public bool AutoRotateBeforeEncryption { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to log when decrypting data with an old key version.
	/// This can help identify data that should be re-encrypted.
	/// Default is true.
	/// </summary>
	public bool ReEncryptOnRead { get; set; } = true;
}
