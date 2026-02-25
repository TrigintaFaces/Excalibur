// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Implements message signing using HMAC algorithms for high-performance integrity verification.
/// </summary>
/// <remarks>
/// This implementation provides:
/// <list type="bullet">
/// <item> HMAC-SHA256 and HMAC-SHA512 support </item>
/// <item> Time-based signature validation to prevent replay attacks </item>
/// <item> Multi-tenant key isolation </item>
/// <item> Secure key storage integration </item>
/// <item> Constant-time signature comparison to prevent timing attacks </item>
/// </list>
/// </remarks>
public sealed partial class HmacMessageSigningService : IMessageSigningService, IDisposable
{
	private static readonly CompositeFormat UnsupportedAlgorithmFormat =
			CompositeFormat.Parse(Resources.HmacMessageSigningService_UnsupportedAlgorithmFormat);
	private static readonly CompositeFormat UnsupportedAlgorithmForVerificationFormat =
			CompositeFormat.Parse(Resources.HmacMessageSigningService_UnsupportedAlgorithmForVerificationFormat);

	private const int MaxCacheSize = 1024;
	private static readonly long TtlTicks = Stopwatch.Frequency * 30 * 60; // 30 minutes

	private readonly SigningOptions _options;
	private readonly ILogger<HmacMessageSigningService> _logger;
	private readonly IKeyProvider _keyProvider;
	private readonly ConcurrentDictionary<string, (byte[] Key, long ExpiresAtTimestamp)> _keyCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="HmacMessageSigningService" /> class.
	/// </summary>
	/// <param name="options">The signing options.</param>
	/// <param name="keyProvider">The provider supplying signing keys.</param>
	/// <param name="logger">The logger used for diagnostics.</param>
	public HmacMessageSigningService(
		IOptions<SigningOptions> options,
		IKeyProvider keyProvider,
		ILogger<HmacMessageSigningService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(keyProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_keyProvider = keyProvider;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<string> SignMessageAsync(
		string content,
		SigningContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(context);

		// Use ArrayPool to reduce allocations for UTF8 encoding
		var byteCount = Encoding.UTF8.GetByteCount(content);
		var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
		try
		{
			var actualBytes = Encoding.UTF8.GetBytes(content, 0, content.Length, buffer, 0);
			var contentBytes = buffer.AsSpan(0, actualBytes).ToArray();
			var signatureBytes = await SignMessageAsync(contentBytes, context, cancellationToken).ConfigureAwait(false);

			return context.Format switch
			{
				SignatureFormat.Base64 => Convert.ToBase64String(signatureBytes),
				SignatureFormat.Hex => Convert.ToHexString(signatureBytes),
				_ => Convert.ToBase64String(signatureBytes),
			};
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> SignMessageAsync(
		byte[] content,
		SigningContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			// Get signing key
			var key = await GetSigningKeyAsync(context, cancellationToken).ConfigureAwait(false);

			// Prepare data to sign (optionally include timestamp)
			var dataToSign = context.IncludeTimestamp
				? PrepareDataWithTimestamp(content)
				: content;

			// Create signature based on algorithm
			var signature = context.Algorithm switch
			{
				SigningAlgorithm.HMACSHA256 => ComputeHmacSha256(dataToSign, key),
				SigningAlgorithm.HMACSHA512 => ComputeHmacSha512(dataToSign, key),
				_ => throw new NotSupportedException(
						string.Format(
								CultureInfo.InvariantCulture,
								UnsupportedAlgorithmFormat,
								context.Algorithm)),
			};

			LogMessageSigned(content.Length, context.Algorithm);

			return signature;
		}
		catch (Exception ex) when (ex is not SigningException)
		{
			LogSigningFailed(ex);
			throw new SigningException(Resources.HmacMessageSigningService_MessageSigningFailed, ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> VerifySignatureAsync(
		string content,
		string signature,
		SigningContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(signature);
		ArgumentNullException.ThrowIfNull(context);

		// Use ArrayPool to reduce allocations for UTF8 encoding
		var byteCount = Encoding.UTF8.GetByteCount(content);
		var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
		try
		{
			var actualBytes = Encoding.UTF8.GetBytes(content, 0, content.Length, buffer, 0);
			var contentBytes = buffer.AsSpan(0, actualBytes).ToArray();
			var signatureBytes = context.Format switch
			{
				SignatureFormat.Base64 => Convert.FromBase64String(signature),
				SignatureFormat.Hex => Convert.FromHexString(signature),
				_ => Convert.FromBase64String(signature),
			};

			return await VerifySignatureAsync(contentBytes, signatureBytes, context, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	/// <inheritdoc />
	public async Task<bool> VerifySignatureAsync(
		byte[] content,
		byte[] signature,
		SigningContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(signature);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			// Get signing key
			var key = await GetSigningKeyAsync(context, cancellationToken).ConfigureAwait(false);

			// Prepare data (with timestamp if needed)
			var dataToVerify = context.IncludeTimestamp
				? PrepareDataWithTimestamp(content)
				: content;

			// Compute expected signature
			var expectedSignature = context.Algorithm switch
			{
				SigningAlgorithm.HMACSHA256 => ComputeHmacSha256(dataToVerify, key),
				SigningAlgorithm.HMACSHA512 => ComputeHmacSha512(dataToVerify, key),
				_ => throw new NotSupportedException(
						string.Format(
								CultureInfo.InvariantCulture,
								UnsupportedAlgorithmForVerificationFormat,
								context.Algorithm)),
			};

			// Use constant-time comparison to prevent timing attacks
			var isValid = CryptographicOperations.FixedTimeEquals(expectedSignature, signature);

			if (isValid)
			{
				LogVerificationSuccessful();
			}
			else
			{
				LogVerificationFailed();
			}

			return isValid;
		}
		catch (Exception ex) when (ex is not VerificationException)
		{
			LogVerificationError(ex);
			throw new VerificationException(
					Resources.HmacMessageSigningService_SignatureVerificationFailed,
					ex);
		}
	}

	/// <inheritdoc />
	public async Task<SignedMessage> CreateSignedMessageAsync(
		string content,
		SigningContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(context);

		var signature = await SignMessageAsync(content, context, cancellationToken).ConfigureAwait(false);

		return new SignedMessage
		{
			Content = content,
			Signature = signature,
			Algorithm = context.Algorithm,
			KeyId = context.KeyId,
			SignedAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>(context.Metadata, StringComparer.Ordinal),
		};
	}

	/// <inheritdoc />
	public async Task<string?> ValidateSignedMessageAsync(
		SignedMessage signedMessage,
		SigningContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(signedMessage);
		ArgumentNullException.ThrowIfNull(context);

		// Check signature age if configured
		if (_options.MaxSignatureAgeMinutes > 0)
		{
			var age = DateTimeOffset.UtcNow - signedMessage.SignedAt;
			if (age.TotalMinutes > _options.MaxSignatureAgeMinutes)
			{
				LogSignatureExpired(age.TotalMinutes, _options.MaxSignatureAgeMinutes);
				return null;
			}
		}

		// Set context from signed message
		context.Algorithm = signedMessage.Algorithm;
		context.KeyId = signedMessage.KeyId;

		// Verify signature
		var isValid = await VerifySignatureAsync(
			signedMessage.Content,
			signedMessage.Signature,
			context,
			cancellationToken).ConfigureAwait(false);

		return isValid ? signedMessage.Content : null;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// Clear sensitive key material
		foreach (var entry in _keyCache.Values)
		{
			Array.Clear(entry.Key, 0, entry.Key.Length);
		}

		_keyCache.Clear();

		_disposed = true;
	}

	private static byte[] PrepareDataWithTimestamp(byte[] content)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var result = new byte[content.Length + sizeof(long)];
		BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(0, sizeof(long)), timestamp);
		Buffer.BlockCopy(content, 0, result, sizeof(long), content.Length);

		return result;
	}

	private static byte[] ComputeHmacSha256(byte[] data, byte[] key)
	{
		using var hmac = new HMACSHA256(key);
		return hmac.ComputeHash(data);
	}

	private static byte[] ComputeHmacSha512(byte[] data, byte[] key)
	{
		using var hmac = new HMACSHA512(key);
		return hmac.ComputeHash(data);
	}

	private async Task<byte[]> GetSigningKeyAsync(SigningContext context, CancellationToken cancellationToken)
	{
		var keyId = BuildKeyIdentifier(context);

		// Check cache with TTL validation
		if (_keyCache.TryGetValue(keyId, out var entry))
		{
			if (Stopwatch.GetTimestamp() < entry.ExpiresAtTimestamp)
			{
				return entry.Key;
			}

			// Expired - remove and clear sensitive data
			if (_keyCache.TryRemove(keyId, out var expired))
			{
				Array.Clear(expired.Key, 0, expired.Key.Length);
			}
		}

		var key = await _keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		// Only cache if within bounded limit
		if (_keyCache.Count < MaxCacheSize)
		{
			_keyCache[keyId] = (key, Stopwatch.GetTimestamp() + TtlTicks);
		}

		return key;
	}

	private string BuildKeyIdentifier(SigningContext context)
	{
		var parts = new List<string> { "signing" };

		if (!string.IsNullOrEmpty(context.TenantId))
		{
			parts.Add(context.TenantId);
		}

		if (!string.IsNullOrEmpty(context.KeyId))
		{
			parts.Add(context.KeyId);
		}
		else if (!string.IsNullOrEmpty(_options.DefaultKeyId))
		{
			parts.Add(_options.DefaultKeyId);
		}

		if (!string.IsNullOrEmpty(context.Purpose))
		{
			parts.Add(context.Purpose);
		}

		return string.Join(':', parts);
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.HmacMessageSigned, LogLevel.Debug, "Successfully signed message of {ContentSize} bytes using {Algorithm}")]
	private partial void LogMessageSigned(int contentSize, SigningAlgorithm algorithm);

	[LoggerMessage(SecurityEventId.HmacSigningFailed, LogLevel.Error, "Failed to sign message")]
	private partial void LogSigningFailed(Exception ex);

	[LoggerMessage(SecurityEventId.HmacVerificationSuccessful, LogLevel.Debug, "Signature verification successful")]
	private partial void LogVerificationSuccessful();

	[LoggerMessage(SecurityEventId.HmacVerificationFailed, LogLevel.Warning, "Signature verification failed - signatures do not match")]
	private partial void LogVerificationFailed();

	[LoggerMessage(SecurityEventId.HmacVerificationError, LogLevel.Error, "Failed to verify signature")]
	private partial void LogVerificationError(Exception ex);

	[LoggerMessage(SecurityEventId.SignatureExpired, LogLevel.Warning, "Signature expired - age {AgeMinutes} minutes exceeds maximum {MaxMinutes}")]
	private partial void LogSignatureExpired(double ageMinutes, int maxMinutes);
}
