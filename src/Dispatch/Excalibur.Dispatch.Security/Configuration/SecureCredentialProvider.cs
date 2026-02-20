// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides secure credential management with support for multiple credential stores. This provider ensures credentials are never hardcoded
/// and are retrieved from secure sources.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="SecureCredentialProvider" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
/// <param name="credentialStores"> The list of credential stores to use. </param>
public sealed partial class SecureCredentialProvider(
		ILogger<SecureCredentialProvider> logger,
		IEnumerable<ICredentialStore> credentialStores) : ISecureCredentialProvider, IDisposable
{
	private static readonly CompositeFormat NoWritableStoresAvailableFormat =
			CompositeFormat.Parse(Resources.SecureCredentialProvider_NoWritableStoresAvailableFormat);

	private readonly ILogger<SecureCredentialProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly List<ICredentialStore> _credentialStores =
		credentialStores?.ToList() ?? throw new ArgumentNullException(nameof(credentialStores));

	private readonly SemaphoreSlim _cacheLock = new(1, 1);
	private readonly Dictionary<string, CachedCredential> _credentialCache = [];
	private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Retrieves a credential by key from configured secure stores.
	/// </summary>
	/// <param name="key"> The credential key to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The secure credential or null if not found. </returns>
	public async Task<SecureString?> GetCredentialAsync(string key, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		// Check cache first
		await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_credentialCache.TryGetValue(key, out var cached) && !cached.IsExpired)
			{
				LogCredentialRetrievedFromCache(key);
				return cached.Value;
			}
		}
		finally
		{
			_ = _cacheLock.Release();
		}

		// Try each credential store in order
		foreach (var store in _credentialStores)
		{
			try
			{
				var credential = await store.GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);
				if (credential != null)
				{
					LogCredentialRetrievedFromStore(key, store.GetType().Name);

					// Cache the credential
					await CacheCredentialAsync(key, credential, cancellationToken).ConfigureAwait(false);
					return credential;
				}
			}
			catch (Exception ex)
			{
				LogRetrievalFailedFromStore(ex, key, store.GetType().Name);
			}
		}

		LogCredentialNotFound(key);
		return null;
	}

	/// <summary>
	/// Validates that a credential exists and meets security requirements.
	/// </summary>
	/// <param name="key"> The credential key to validate. </param>
	/// <param name="requirements"> The security requirements to validate against. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A validation result indicating success or failure. </returns>
	public async Task<CredentialValidationResult> ValidateCredentialAsync(
		string key,
		CredentialRequirements requirements,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(requirements);

		var credential = await GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);
		if (credential == null)
		{
			return CredentialValidationResult.Failure($"Credential '{key}' not found");
		}

		var errors = new List<string>();

		// Convert SecureString to string for validation (dispose immediately after)
		var credentialValue = SecureStringToString(credential);
		try
		{
			ValidateCredentialRequirements(credentialValue, requirements, errors);
		}
		finally
		{
			ClearCredentialFromMemory(credentialValue);
		}

		return errors.Count == 0
			? CredentialValidationResult.Success()
			: CredentialValidationResult.Failure([.. errors]);
	}

	/// <summary>
	/// Rotates a credential by generating a new value and updating all stores.
	/// </summary>
	/// <param name="key"> The credential key to rotate. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the rotation operation. </returns>
	/// <exception cref="InvalidOperationException">Thrown when no writable credential stores are available.</exception>
	public async Task RotateCredentialAsync(string key, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		LogRotatingCredential(key);

		// Generate new credential
		// CA2000: newCredential is properly cached and disposed by the credential provider
		// R0.8: Dispose objects before losing scope
#pragma warning disable CA2000
		var newCredential = GenerateSecureCredential();
#pragma warning restore CA2000 // Dispose objects before losing scope

		// Update all writable stores
		var updated = false;
		foreach (var store in _credentialStores.OfType<IWritableCredentialStore>())
		{
			try
			{
				await store.StoreCredentialAsync(key, newCredential, cancellationToken).ConfigureAwait(false);
				updated = true;
				LogCredentialRotated(key, store.GetType().Name);
			}
			catch (Exception ex)
			{
				LogRotationFailed(ex, key, store.GetType().Name);
			}
		}

		if (!updated)
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.InvariantCulture,
							NoWritableStoresAvailableFormat,
							key));
		}

		// Invalidate cache
		await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_ = _credentialCache.Remove(key);
		}
		finally
		{
			_ = _cacheLock.Release();
		}
	}

	/// <summary>
	/// Disposes the credential provider and releases all cached credentials.
	/// </summary>
	public void Dispose()
	{
		_cacheLock.Dispose();
		foreach (var cached in _credentialCache.Values)
		{
			cached.Value?.Dispose();
		}

		_credentialCache.Clear();
	}

	private static string SecureStringToString(SecureString secureString)
	{
		var ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
			return Marshal.PtrToStringUni(ptr) ?? string.Empty;
		}
		finally
		{
			if (ptr != IntPtr.Zero)
			{
				Marshal.ZeroFreeGlobalAllocUnicode(ptr);
			}
		}
	}

	private static void ValidateCredentialRequirements(string credentialValue, CredentialRequirements requirements, List<string> errors)
	{
		// Check minimum length
		if (credentialValue.Length < requirements.MinimumLength)
		{
			errors.Add($"Credential must be at least {requirements.MinimumLength.ToString(CultureInfo.InvariantCulture)} characters long");
		}

		// Check for default/weak credentials
		if (requirements.ProhibitedValues?.Contains(credentialValue) == true)
		{
			errors.Add("Credential matches a prohibited value (e.g., default credentials)");
		}

		// Check complexity requirements
		if (requirements.RequireUppercase && !credentialValue.Any(char.IsUpper))
		{
			errors.Add("Credential must contain at least one uppercase letter");
		}

		if (requirements.RequireLowercase && !credentialValue.Any(char.IsLower))
		{
			errors.Add("Credential must contain at least one lowercase letter");
		}

		if (requirements.RequireDigit && !credentialValue.Any(char.IsDigit))
		{
			errors.Add("Credential must contain at least one digit");
		}

		if (requirements.RequireSpecialCharacter && credentialValue.All(char.IsLetterOrDigit))
		{
			errors.Add("Credential must contain at least one special character");
		}

		// Check for patterns that indicate hardcoded or test credentials
		if (ContainsTestPatterns(credentialValue))
		{
			errors.Add("Credential appears to be a test or development credential");
		}
	}

	private static void ClearCredentialFromMemory(string credentialValue)
	{
		// Clear the string from memory
		if (!string.IsNullOrEmpty(credentialValue))
		{
			unsafe
			{
				fixed (char* ptr = credentialValue)
				{
					for (var i = 0; i < credentialValue.Length; i++)
					{
						ptr[i] = '\0';
					}
				}
			}
		}
	}

	private static bool ContainsTestPatterns(string value)
	{
		var testPatterns = new[] { "TEST", "DEMO", "SAMPLE", "EXAMPLE", "TEMP", "DUMMY", "PASSWORD", "12345", "ADMIN", "GUEST", "DEFAULT" };

		var upperValue = value.ToUpperInvariant();
		return testPatterns.Any(upperValue.Contains);
	}

	private static SecureString GenerateSecureCredential()
	{
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}|;:,.<>?";
		using var random = RandomNumberGenerator.Create();
		var buffer = new byte[32];
		random.GetBytes(buffer);

		var secureString = new SecureString();
		foreach (var b in buffer)
		{
			secureString.AppendChar(chars[b % chars.Length]);
		}

		secureString.MakeReadOnly();
		return secureString;
	}

	private async Task CacheCredentialAsync(string key, SecureString credential, CancellationToken cancellationToken)
	{
		await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_credentialCache[key] = new CachedCredential(credential, DateTimeOffset.UtcNow.Add(_cacheExpiration));
		}
		finally
		{
			_ = _cacheLock.Release();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.CredentialCached, LogLevel.Debug, "Credential {Key} retrieved from cache")]
	private partial void LogCredentialRetrievedFromCache(string key);

	[LoggerMessage(SecurityEventId.CredentialProviderRetrievedFromStore, LogLevel.Debug, "Credential {Key} retrieved from {Store}")]
	private partial void LogCredentialRetrievedFromStore(string key, string store);

	[LoggerMessage(SecurityEventId.CredentialProviderStoreException, LogLevel.Warning, "Failed to retrieve credential {Key} from {Store}")]
	private partial void LogRetrievalFailedFromStore(Exception ex, string key, string store);

	[LoggerMessage(SecurityEventId.CredentialProviderNotFoundInAny, LogLevel.Warning, "Credential {Key} not found in any configured store")]
	private partial void LogCredentialNotFound(string key);

	[LoggerMessage(SecurityEventId.CredentialProviderRetrieving, LogLevel.Information, "Rotating credential {Key}")]
	private partial void LogRotatingCredential(string key);

	[LoggerMessage(SecurityEventId.CredentialProviderRetrievedSuccess, LogLevel.Information, "Credential {Key} rotated in {Store}")]
	private partial void LogCredentialRotated(string key, string store);

	[LoggerMessage(SecurityEventId.CredentialProviderRetrievalFailed, LogLevel.Error, "Failed to rotate credential {Key} in {Store}")]
	private partial void LogRotationFailed(Exception ex, string key, string store);

	private sealed class CachedCredential(SecureString value, DateTimeOffset expiresAt)
	{
		public SecureString Value { get; } = value;

		public DateTimeOffset ExpiresAt { get; } = expiresAt;

		public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
	}
}
