// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Retrieves credentials from Azure Key Vault. This provides enterprise-grade secret management with audit logging and access control.
/// </summary>
public sealed partial class AzureKeyVaultCredentialStore : IWritableCredentialStore
{
	private readonly ILogger<AzureKeyVaultCredentialStore> _logger;
	private readonly SecretClient _secretClient;
	private readonly string _keyPrefix;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultCredentialStore" /> class.
	/// </summary>
	/// <param name="configuration"> The configuration containing Key Vault settings. </param>
	/// <param name="logger"> The logger instance. </param>
	public AzureKeyVaultCredentialStore(
		IConfiguration configuration,
		ILogger<AzureKeyVaultCredentialStore> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(configuration);

		var vaultUri = configuration["AzureKeyVault:VaultUri"];
		if (string.IsNullOrEmpty(vaultUri))
		{
			throw new InvalidOperationException("Azure Key Vault URI not configured. Set 'AzureKeyVault:VaultUri' in configuration.");
		}

		_keyPrefix = configuration["AzureKeyVault:KeyPrefix"] ?? "dispatch-";

		// Use DefaultAzureCredential which works with multiple authentication methods including managed identity, Azure CLI, Visual Studio, etc.
		var credential = new DefaultAzureCredential();
		_secretClient = new SecretClient(new Uri(vaultUri), credential);

		LogKeyVaultInitialized(vaultUri);
	}

	/// <summary>
	/// Retrieves a credential from Azure Key Vault.
	/// </summary>
	/// <param name="key"> The credential key. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The secure credential or null if not found. </returns>
	public async Task<SecureString?> GetCredentialAsync(string key, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		var secretName = NormalizeKeyForKeyVault(key);

		try
		{
			var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (response?.Value?.Value == null)
			{
				LogSecretNotFound(key);
				return null;
			}

			LogSecretRetrieved(key);

			var secureString = new SecureString();
			foreach (var c in response.Value.Value)
			{
				secureString.AppendChar(c);
			}

			secureString.MakeReadOnly();

			return secureString;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogSecretNotFound404(key);
			return null;
		}
		catch (Exception ex)
		{
			LogRetrievalFailed(ex, key);
			throw;
		}
	}

	/// <summary>
	/// Stores a credential in Azure Key Vault.
	/// </summary>
	/// <param name="key"> The credential key. </param>
	/// <param name="credential"> The credential to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the storage operation. </returns>
	public async Task StoreCredentialAsync(string key, SecureString credential, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(credential);

		var secretName = NormalizeKeyForKeyVault(key);
		var value = SecureStringToString(credential);

		try
		{
			var secret = new KeyVaultSecret(secretName, value)
			{
				Properties =
				{
					ExpiresOn = DateTimeOffset.UtcNow.AddDays(90), // Auto-expire after 90 days for rotation
					Tags = { ["ManagedBy"] = "Excalibur.Dispatch", ["CreatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["Purpose"] = "Credential" },
				},
			};

			_ = await _secretClient.SetSecretAsync(secret, cancellationToken).ConfigureAwait(false);
			LogSecretStored(key);
		}
		catch (Exception ex)
		{
			LogStorageFailed(ex, key);
			throw;
		}
		finally
		{
			// Clear the value from memory
			if (!string.IsNullOrEmpty(value))
			{
				unsafe
				{
					fixed (char* ptr = value)
					{
						for (var i = 0; i < value.Length; i++)
						{
							ptr[i] = '\0';
						}
					}
				}
			}
		}
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

	[GeneratedRegex("[^a-zA-Z0-9-]", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
	private static partial Regex InvalidKeyVaultCharsRegex();

	/// <summary>
	/// Normalizes a key for Azure Key Vault naming requirements. Key Vault secret names must be 1-127 characters and contain only
	/// alphanumeric characters and hyphens.
	/// </summary>
	/// <param name="key">The key to normalize.</param>
	/// <returns>The normalized key.</returns>
	private string NormalizeKeyForKeyVault(string key)
	{
		// Replace invalid characters with hyphens
		var normalized = InvalidKeyVaultCharsRegex().Replace(key, "-");

		// Add prefix
		normalized = _keyPrefix + normalized;

		// Ensure it doesn't start or end with a hyphen
		normalized = normalized.Trim('-');

		// Truncate if too long
		if (normalized.Length > 127)
		{
			normalized = normalized.Substring(0, 127);
		}

		return normalized;
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.AzureKeyVaultCredentialStoreCreated, LogLevel.Information, "Azure Key Vault credential store initialized with vault {VaultUri}")]
	private partial void LogKeyVaultInitialized(string vaultUri);

	[LoggerMessage(SecurityEventId.AzureKeyVaultSecretNotFound, LogLevel.Debug, "Secret {Key} not found in Azure Key Vault")]
	private partial void LogSecretNotFound(string key);

	[LoggerMessage(SecurityEventId.AzureKeyVaultRetrieved, LogLevel.Debug, "Secret {Key} retrieved from Azure Key Vault")]
	private partial void LogSecretRetrieved(string key);

	[LoggerMessage(SecurityEventId.AzureKeyVaultRequestFailed, LogLevel.Debug, "Secret {Key} not found in Azure Key Vault (404)")]
	private partial void LogSecretNotFound404(string key);

	[LoggerMessage(SecurityEventId.AzureKeyVaultRetrieveFailed, LogLevel.Error, "Failed to retrieve secret {Key} from Azure Key Vault")]
	private partial void LogRetrievalFailed(Exception ex, string key);

	[LoggerMessage(SecurityEventId.AzureKeyVaultStored, LogLevel.Information, "Secret {Key} stored in Azure Key Vault")]
	private partial void LogSecretStored(string key);

	[LoggerMessage(SecurityEventId.AzureKeyVaultStoring, LogLevel.Error, "Failed to store secret {Key} in Azure Key Vault")]
	private partial void LogStorageFailed(Exception ex, string key);
}
