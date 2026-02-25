// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Excalibur.Domain.Exceptions;

namespace Excalibur.Domain;

/// <summary>
/// Thread-safe and security-enhanced implementation of ApplicationContext with support for secure string storage.
/// </summary>
public static partial class ApplicationContext
{
	private static readonly CompositeFormat CircularReferenceDetectedFormat =
			CompositeFormat.Parse(Resources.ApplicationContext_CircularReferenceDetected);

	private static readonly CompositeFormat UnresolvedPlaceholderFormat =
			CompositeFormat.Parse(Resources.ApplicationContext_UnresolvedPlaceholder);

	private static readonly CompositeFormat ExpandValueFailedFormat =
			CompositeFormat.Parse(Resources.ApplicationContext_ExpandValueFailed);

	private static readonly Regex ExpansionPattern = ExpansionRegex();
	private static readonly ConcurrentDictionary<string, string?> ContextValues = new(StringComparer.OrdinalIgnoreCase);
#if NET9_0_OR_GREATER
	private static readonly Lock InitLock = new();
#else
	private static readonly object InitLock = new();
#endif
	private static readonly ConcurrentDictionary<string, string> SecureContextValues = new(StringComparer.OrdinalIgnoreCase);
	private static readonly ReaderWriterLockSlim CacheLock = new();
	private static readonly ConcurrentDictionary<string, (string? Value, DateTimeOffset CachedAt)> ValueCache = new(StringComparer.Ordinal);
	private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Sensitive property names that should use SecureString storage.
	/// </summary>
	private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
	{
		"ServiceAccountPrivateKeyPassword",
		"Password",
		"Secret",
		"Key",
		"Token",
		"Credential",
	};

	/// <summary>
	/// Gets the name of the application.
	/// </summary>
	/// <value>
	/// The name of the application.
	/// </value>
	public static string ApplicationName => GetCached(nameof(ApplicationName));

	/// <summary>
	/// Gets the system name of the application.
	/// </summary>
	/// <value>
	/// The system name of the application.
	/// </value>
	public static string ApplicationSystemName => GetCached(nameof(ApplicationSystemName));

	/// <summary>
	/// Gets the display name of the application.
	/// </summary>
	/// <value>
	/// The display name of the application.
	/// </value>
	public static string ApplicationDisplayName => GetCached(nameof(ApplicationDisplayName));

	/// <summary>
	/// Gets the audience value for the authentication service.
	/// </summary>
	/// <value>
	/// The audience value for the authentication service.
	/// </value>
	public static string AuthenticationServiceAudience => GetCached(nameof(AuthenticationServiceAudience));

	/// <summary>
	/// Gets the endpoint for the authentication service.
	/// </summary>
	/// <value>
	/// The endpoint for the authentication service.
	/// </value>
	public static string AuthenticationServiceEndpoint => GetCached(nameof(AuthenticationServiceEndpoint));

	/// <summary>
	/// Gets the path to the public key file for the authentication service.
	/// </summary>
	/// <value>
	/// The path to the public key file for the authentication service.
	/// </value>
	public static string AuthenticationServicePublicKeyPath => GetCached(nameof(AuthenticationServicePublicKeyPath));

	/// <summary>
	/// Gets the endpoint for the authorization service.
	/// </summary>
	/// <value>
	/// The endpoint for the authorization service.
	/// </value>
	public static string AuthorizationServiceEndpoint => GetCached(nameof(AuthorizationServiceEndpoint));

	/// <summary>
	/// Gets the current application context, defaulting to "local" if not explicitly set in the environment variables.
	/// </summary>
	/// <value>
	/// The current application context, defaulting to "local" if not explicitly set in the environment variables.
	/// </value>
	public static string Context => Environment.GetEnvironmentVariable(GetCached("APP_CONTEXT_NAME", "local")) ?? "local";

	/// <summary>
	/// Gets the name of the service account.
	/// </summary>
	/// <value>
	/// The name of the service account.
	/// </value>
	public static string ServiceAccountName => GetCached(nameof(ServiceAccountName));

	/// <summary>
	/// Gets the path to the private key file for the service account.
	/// </summary>
	/// <value>
	/// The path to the private key file for the service account.
	/// </value>
	public static string ServiceAccountPrivateKeyPath => GetCached(nameof(ServiceAccountPrivateKeyPath));

	/// <summary>
	/// Gets the password for the service account's private key.
	/// </summary>
	/// <value>
	/// The password for the service account's private key.
	/// </value>
	public static string ServiceAccountPrivateKeyPasswordSecure => GetSecure(nameof(ServiceAccountPrivateKeyPasswordSecure));

	/// <summary>
	/// Initializes the application context with the specified key-value pairs. Sensitive values are automatically stored securely.
	/// </summary>
	/// <param name="context"> A dictionary containing context key-value pairs. </param>
	public static void Init(IDictionary<string, string?> context)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock (InitLock)
		{
			// Clear existing values atomically
			ContextValues.Clear();
			SecureContextValues.Clear();
			ValueCache.Clear();

			foreach (var kvp in context)
			{
				if (IsSensitiveKey(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
				{
					SetSecure(kvp.Key, kvp.Value);
				}
				else
				{
					ContextValues[kvp.Key] = kvp.Value;
				}
			}
		}
	}

	/// <summary>
	/// Resets the application context by clearing all stored values.
	/// </summary>
	public static void Reset()
	{
		lock (InitLock)
		{
			ContextValues.Clear();
			SecureContextValues.Clear();
			ValueCache.Clear();
		}
	}

	/// <summary>
	/// Returns the string value directly. This method exists for backward compatibility
	/// after SecureString removal (Microsoft deprecated SecureString for .NET Core).
	/// </summary>
	public static string ConvertToUnsecureString(string secureString)
	{
		ArgumentNullException.ThrowIfNull(secureString);
		return secureString;
	}

	/// <summary>
	/// Expands placeholders in the specified value using the context and environment variables.
	/// </summary>
	public static string? Expand(string? value) => ExpandInternal(value, new HashSet<string>(StringComparer.Ordinal));

	/// <summary>
	/// Gets a value with the specified key from the context.
	/// </summary>
	public static string Get(string key) => GetCached(key);

	/// <summary>
	/// Gets a value with the specified key from the context, or a default value if not found.
	/// </summary>
	public static string Get(string key, string defaultValue) => GetCached(key, defaultValue);

	/// <summary>
	/// Gets a value from cache if available and not expired, otherwise retrieves and caches it.
	/// </summary>
	/// <exception cref="InvalidConfigurationException"></exception>
	private static string GetCached(string key, string? defaultValue = null)
	{
		var now = DateTimeOffset.UtcNow;

		if (ValueCache.TryGetValue(key, out var cached) && now - cached.CachedAt < CacheExpiration)
		{
			return cached.Value ?? defaultValue ?? throw new InvalidConfigurationException(key);
		}

		CacheLock.EnterWriteLock();
		try
		{
			// Double-check after acquiring lock
			if (ValueCache.TryGetValue(key, out cached) && now - cached.CachedAt < CacheExpiration)
			{
				return cached.Value ?? defaultValue ?? throw new InvalidConfigurationException(key);
			}

			var value = GetInternal(key, new HashSet<string>(StringComparer.Ordinal));
			ValueCache[key] = (value, now);
			return value ?? defaultValue ?? throw new InvalidConfigurationException(key);
		}
		finally
		{
			CacheLock.ExitWriteLock();
		}
	}

	/// <summary>
	/// Gets a sensitive string value.
	/// </summary>
	/// <exception cref="InvalidConfigurationException"></exception>
	private static string GetSecure(string key)
	{
		if (SecureContextValues.TryGetValue(key, out var secureValue))
		{
			return secureValue;
		}

		// Try to get from regular storage
		var value = GetInternal(key, new HashSet<string>(StringComparer.Ordinal));
		if (value != null)
		{
			SecureContextValues[key] = value;
			return value;
		}

		throw new InvalidConfigurationException(key);
	}

	/// <summary>
	/// Sets a sensitive string value.
	/// </summary>
	private static void SetSecure(string key, string value)
	{
		SecureContextValues[key] = value;
		_ = ValueCache.TryRemove(key, out _);
	}

	/// <summary>
	/// Determines if a key should be treated as sensitive.
	/// </summary>
	private static bool IsSensitiveKey(string key) =>
		SensitiveProperties.Any(sensitive => key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));

	private static string? GetInternal(string key, ISet<string> searchPath)
	{
		EnsureNoCircularReferenceExists(key, searchPath);
		_ = searchPath.Add(key);

		try
		{
			// Check environment first
			var envValue = Environment.GetEnvironmentVariable(key);
			if (!string.IsNullOrEmpty(envValue))
			{
				return ExpandInternal(envValue, searchPath);
			}

			// Check secure storage if sensitive
			if (IsSensitiveKey(key) && SecureContextValues.TryGetValue(key, out var secureValue))
			{
				return ExpandInternal(secureValue, searchPath);
			}

			// Fallback to regular context
			if (!ContextValues.TryGetValue(key, out var value))
			{
				return null;
			}

			return ExpandInternal(value, searchPath);
		}
		finally
		{
			_ = searchPath.Remove(key);
		}
	}

	private static void EnsureNoCircularReferenceExists(string key, ICollection<string> searchPath)
	{
		if (searchPath.Contains(key, StringComparer.Ordinal))
		{
			throw new InvalidConfigurationException(
					key,
					message: string.Format(
							CultureInfo.CurrentCulture,
							CircularReferenceDetectedFormat,
							string.Join(" > ", searchPath),
							key));
		}
	}

	private static string? ExpandInternal(string? value, ISet<string> searchPath)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		// First expand our own placeholders
		string expandedValue;
		try
		{
			expandedValue = ExpansionPattern.Replace(
				value,
				match =>
				{
					var placeholderKey = match.Groups["placeholder"].Value;

					// Check for circular reference before calling GetInternal
					if (searchPath.Contains(placeholderKey))
					{
						throw new InvalidConfigurationException(
								placeholderKey,
								message:
								string.Format(
										CultureInfo.CurrentCulture,
										CircularReferenceDetectedFormat,
										string.Join(" > ", searchPath),
										placeholderKey));
					}

					var expanded = GetInternal(placeholderKey, searchPath) ?? throw new InvalidConfigurationException(
							placeholderKey,
							message: string.Format(
									CultureInfo.CurrentCulture,
									UnresolvedPlaceholderFormat,
									placeholderKey));

					return expanded;
				});
		}
		catch (InvalidConfigurationException)
		{
			// Re-throw InvalidConfigurationException as-is
			throw;
		}
		catch (Exception ex)
		{
			// Wrap any other exception that might come from the regex replace
			throw new InvalidConfigurationException(
					value,
					message: string.Format(
							CultureInfo.CurrentCulture,
							ExpandValueFailedFormat,
							value),
					innerException: ex);
		}

		// Then expand environment variables
		return Environment.ExpandEnvironmentVariables(expandedValue);
	}

	/// <summary>
	/// Compiles the regular expression used for expanding placeholders in context values.
	/// </summary>
	[GeneratedRegex("%(?<placeholder>[^%]+)%", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "en-US")]
	private static partial Regex ExpansionRegex();
}
