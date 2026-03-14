// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// A composite <see cref="IMessageSigningService"/> that delegates to registered
/// <see cref="ISignatureAlgorithmProvider"/> instances based on the requested algorithm.
/// </summary>
/// <remarks>
/// <para>
/// This follows the composite pattern (similar to ASP.NET Core <c>CompositeFileProvider</c>),
/// routing signing and verification requests to the appropriate algorithm provider.
/// </para>
/// <para>
/// For asymmetric algorithms (ECDSA, Ed25519), the service appends <c>:pub</c> to the key ID
/// when resolving keys for verification, enabling asymmetric key pairs to be stored via
/// the existing <see cref="IKeyProvider"/> interface without modification.
/// </para>
/// </remarks>
public sealed partial class CompositeMessageSigningService : IMessageSigningService, IDisposable
{
	private const int MaxCacheSize = 1024;
	private static readonly long TtlTicks = Stopwatch.Frequency * 30 * 60; // 30 minutes

	private readonly IReadOnlyList<ISignatureAlgorithmProvider> _providers;
	private readonly SigningOptions _options;
	private readonly ILogger<CompositeMessageSigningService> _logger;
	private readonly IKeyProvider _keyProvider;
	private readonly ConcurrentDictionary<string, (byte[] Key, long ExpiresAtTimestamp)> _keyCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompositeMessageSigningService"/> class.
	/// </summary>
	/// <param name="providers">The collection of algorithm-specific signing providers.</param>
	/// <param name="options">The signing options.</param>
	/// <param name="keyProvider">The provider supplying signing keys.</param>
	/// <param name="logger">The logger used for diagnostics.</param>
	public CompositeMessageSigningService(
		IEnumerable<ISignatureAlgorithmProvider> providers,
		IOptions<SigningOptions> options,
		IKeyProvider keyProvider,
		ILogger<CompositeMessageSigningService> logger)
	{
		ArgumentNullException.ThrowIfNull(providers);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(keyProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_providers = providers.ToList();
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
		ObjectDisposedException.ThrowIf(_disposed, this);

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
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			var provider = ResolveProvider(context.Algorithm);
			var key = await GetSigningKeyAsync(context, forVerification: false, cancellationToken).ConfigureAwait(false);

			var dataToSign = context.IncludeTimestamp
				? PrepareDataWithTimestamp(content)
				: content;

			var signature = await provider.SignAsync(dataToSign, key, context.Algorithm, cancellationToken).ConfigureAwait(false);

			LogMessageSigned(content.Length, context.Algorithm);

			return signature;
		}
		catch (Exception ex) when (ex is not SigningException and not PlatformNotSupportedException)
		{
			LogSigningFailed(ex);
			throw new SigningException("Message signing failed.", ex);
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
		ObjectDisposedException.ThrowIf(_disposed, this);

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
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			var provider = ResolveProvider(context.Algorithm);
			var key = await GetSigningKeyAsync(context, forVerification: IsAsymmetricAlgorithm(context.Algorithm), cancellationToken).ConfigureAwait(false);

			var dataToVerify = context.IncludeTimestamp
				? PrepareDataWithTimestamp(content)
				: content;

			var isValid = await provider.VerifyAsync(dataToVerify, signature, key, context.Algorithm, cancellationToken).ConfigureAwait(false);

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
		catch (Exception ex) when (ex is not VerificationException and not PlatformNotSupportedException)
		{
			LogVerificationError(ex);
			throw new VerificationException("Signature verification failed.", ex);
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
		ObjectDisposedException.ThrowIf(_disposed, this);

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
		ObjectDisposedException.ThrowIf(_disposed, this);

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

		_disposed = true;

		// Clear sensitive key material
		foreach (var entry in _keyCache.Values)
		{
			Array.Clear(entry.Key, 0, entry.Key.Length);
		}

		_keyCache.Clear();
	}

	private ISignatureAlgorithmProvider ResolveProvider(SigningAlgorithm algorithm)
	{
		for (var i = 0; i < _providers.Count; i++)
		{
			if (_providers[i].SupportsAlgorithm(algorithm))
			{
				return _providers[i];
			}
		}

		LogUnsupportedAlgorithm(algorithm);
		throw new NotSupportedException($"No registered {nameof(ISignatureAlgorithmProvider)} supports algorithm '{algorithm}'.");
	}

	private static bool IsAsymmetricAlgorithm(SigningAlgorithm algorithm)
		=> algorithm is SigningAlgorithm.ECDSASHA256 or SigningAlgorithm.Ed25519
			or SigningAlgorithm.RSASHA256 or SigningAlgorithm.RSAPSSSHA256;

	private async Task<byte[]> GetSigningKeyAsync(SigningContext context, bool forVerification, CancellationToken cancellationToken)
	{
		var keyId = BuildKeyIdentifier(context);

		// For asymmetric verification, append :pub to resolve the public key
		if (forVerification)
		{
			keyId = $"{keyId}:pub";
		}

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

	private static byte[] PrepareDataWithTimestamp(byte[] content)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var result = new byte[content.Length + sizeof(long)];
		BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(0, sizeof(long)), timestamp);
		Buffer.BlockCopy(content, 0, result, sizeof(long), content.Length);
		return result;
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.CompositeMessageSigned, LogLevel.Debug, "Successfully signed message of {ContentSize} bytes using {Algorithm}")]
	private partial void LogMessageSigned(int contentSize, SigningAlgorithm algorithm);

	[LoggerMessage(SecurityEventId.CompositeSigningFailed, LogLevel.Error, "Failed to sign message")]
	private partial void LogSigningFailed(Exception ex);

	[LoggerMessage(SecurityEventId.CompositeVerificationSuccessful, LogLevel.Debug, "Signature verification successful")]
	private partial void LogVerificationSuccessful();

	[LoggerMessage(SecurityEventId.CompositeVerificationFailed, LogLevel.Warning, "Signature verification failed")]
	private partial void LogVerificationFailed();

	[LoggerMessage(SecurityEventId.CompositeVerificationError, LogLevel.Error, "Failed to verify signature")]
	private partial void LogVerificationError(Exception ex);

	[LoggerMessage(SecurityEventId.SignatureExpired, LogLevel.Warning, "Signature expired - age {AgeMinutes} minutes exceeds maximum {MaxMinutes}")]
	private partial void LogSignatureExpired(double ageMinutes, int maxMinutes);

	[LoggerMessage(SecurityEventId.UnsupportedAlgorithmRequested, LogLevel.Error, "No provider registered for algorithm {Algorithm}")]
	private partial void LogUnsupportedAlgorithm(SigningAlgorithm algorithm);
}
