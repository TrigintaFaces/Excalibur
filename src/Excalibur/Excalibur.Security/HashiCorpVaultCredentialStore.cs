// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;

using Excalibur.Security.Diagnostics;
using Excalibur.Security.Internal;
using Excalibur.Security.Vault;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Security;

/// <summary>
/// HashiCorp Vault credential store backed by the KV&#160;v2 secrets engine. Reads and writes
/// secrets through the real Vault HTTP API (via an injectable <see cref="IVaultSecretClient"/>
/// seam), so a <see cref="StoreCredentialAsync"/> followed by <see cref="GetCredentialAsync"/>
/// round-trips against Vault — secrets are never sourced from, or silently discarded to,
/// plain configuration.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class HashiCorpVaultCredentialStore : IWritableCredentialStore, IDisposable
{
	private static readonly CompositeFormat FailedToRetrieveSecretFormat =
			CompositeFormat.Parse(Resources.HashiCorpVaultCredentialStore_FailedToRetrieveSecretFormat);
	private static readonly CompositeFormat FailedToStoreSecretFormat =
			CompositeFormat.Parse(Resources.HashiCorpVaultCredentialStore_FailedToStoreSecretFormat);

	private readonly ILogger<HashiCorpVaultCredentialStore> _logger;
	private readonly IVaultSecretClient _client;
	private readonly IDisposable? _ownedTransport;

	/// <summary>
	/// Initializes a new instance of the <see cref="HashiCorpVaultCredentialStore"/> class
	/// that talks to a real Vault server over <paramref name="httpClient"/>.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="configuration"> The configuration supplying <c>Vault:Url</c>, <c>Vault:Token</c>, and optional <c>Vault:MountPath</c>. </param>
	/// <param name="httpClient"> The HTTP client used for Vault API calls. Owned and disposed by this store. </param>
	public HashiCorpVaultCredentialStore(
		ILogger<HashiCorpVaultCredentialStore> logger,
		IConfiguration configuration,
		HttpClient httpClient)
		: this(logger, BuildAdapter(configuration, httpClient), ownedTransport: httpClient)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HashiCorpVaultCredentialStore"/> class
	/// with an explicit <see cref="IVaultSecretClient"/>. Used by tests via
	/// <c>InternalsVisibleTo</c> to drive a fake transport; not part of the public contract.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="client"> The Vault secret client seam. </param>
	internal HashiCorpVaultCredentialStore(
		ILogger<HashiCorpVaultCredentialStore> logger,
		IVaultSecretClient client)
		: this(logger, client, ownedTransport: null)
	{
	}

	private HashiCorpVaultCredentialStore(
		ILogger<HashiCorpVaultCredentialStore> logger,
		IVaultSecretClient client,
		IDisposable? ownedTransport)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_ownedTransport = ownedTransport;
	}

	/// <summary>
	/// Retrieves a credential from HashiCorp Vault.
	/// </summary>
	/// <param name="key"> The secret path/key to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The retrieved secure credential or null if not found. </returns>
	/// <exception cref="ArgumentException">Thrown when key is null, empty, or whitespace.</exception>
	/// <exception cref="InvalidOperationException">Thrown when secret retrieval from HashiCorp Vault fails.</exception>
	public async Task<SecureString?> GetCredentialAsync(string key, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException(
					Resources.HashiCorpVaultCredentialStore_KeyCannotBeNullOrEmpty,
					nameof(key));
		}

		LogRetrievingCredential(key);

		try
		{
			var secretValue = await _client.GetSecretAsync(key, cancellationToken).ConfigureAwait(false);
			if (secretValue is null)
			{
				LogSecretNotFound(key);
				return null;
			}

			// Convert to SecureString
			var secureString = new SecureString();
			foreach (var c in secretValue)
			{
				secureString.AppendChar(c);
			}

			secureString.MakeReadOnly();

			LogCredentialRetrieved(key);
			return secureString;
		}
		catch (Exception ex) when (ex is not ArgumentException)
		{
			LogRetrieveFailed(ex, key);
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.InvariantCulture,
							FailedToRetrieveSecretFormat,
							key),
					ex);
		}
	}

	/// <summary>
	/// Stores a credential in HashiCorp Vault.
	/// </summary>
	/// <param name="key"> The secret path/key to store. </param>
	/// <param name="credential"> The credential to store securely. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A task that represents the asynchronous store operation.</returns>
	/// <exception cref="ArgumentException">Thrown when key is null, empty, or whitespace, or when credential is empty.</exception>
	/// <exception cref="InvalidOperationException">Thrown when storing the secret in HashiCorp Vault fails.</exception>
	public async Task StoreCredentialAsync(string key, SecureString credential,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException(
					Resources.HashiCorpVaultCredentialStore_KeyCannotBeNullOrEmpty,
					nameof(key));
		}

		ArgumentNullException.ThrowIfNull(credential);

		LogStoringCredential(key);

		try
		{
			// Expose the plaintext only inside a pinned, zero-on-exit buffer (never a long-lived
			// unzeroable managed string). Vault's KV v2 HTTP API is string-based, so a transient
			// string is constructed at the call boundary; that copy is the SDK's surface.
			await SecurePlaintextScope.UseAsync(
				credential,
				async (buffer, ct) =>
				{
					if (buffer.Length < 1)
					{
						throw new ArgumentException(
								Resources.HashiCorpVaultCredentialStore_CredentialCannotBeEmpty,
								nameof(credential));
					}

					// Persist to Vault. A backend failure throws here, so success is only logged
					// once the write is durably accepted (never logged-as-success on failure).
					await _client.SetSecretAsync(key, new string(buffer), ct).ConfigureAwait(false);
					return true;
				},
				cancellationToken).ConfigureAwait(false);

			LogCredentialStored(key);
		}
		catch (ArgumentException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogStoreFailed(ex, key);
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.InvariantCulture,
							FailedToStoreSecretFormat,
							key),
					ex);
		}
	}

	/// <inheritdoc/>
	public void Dispose() => _ownedTransport?.Dispose();

	/// <summary>
	/// Builds the real KV&#160;v2 HTTP adapter from configuration, configuring the supplied
	/// <see cref="HttpClient"/> with the Vault base address and authentication token.
	/// </summary>
	private static readonly CompositeFormat VaultUrlMustBeHttpsFormat =
		CompositeFormat.Parse(Resources.HashiCorpVaultCredentialStore_VaultUrlMustBeHttps);

	private static VaultSecretClientAdapter BuildAdapter(IConfiguration configuration, HttpClient httpClient)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(httpClient);

		var vaultUrl = configuration["Vault:Url"] ?? throw new InvalidOperationException(
				Resources.HashiCorpVaultCredentialStore_VaultUrlRequired);
		var token = configuration["Vault:Token"] ?? throw new InvalidOperationException(
				Resources.HashiCorpVaultCredentialStore_VaultTokenRequired);
		var mountPath = configuration["Vault:MountPath"] ?? "secret";

		// Reject a non-https Vault endpoint: an http:// URL transmits the Vault token and every secret in
		// plaintext over the wire. Fail fast at construction rather than silently leaking credentials.
		var vaultUri = new Uri(vaultUrl.TrimEnd('/'));
		if (!vaultUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					VaultUrlMustBeHttpsFormat,
					vaultUri.Scheme));
		}

		httpClient.BaseAddress = vaultUri;
		if (!httpClient.DefaultRequestHeaders.Contains("X-Vault-Token"))
		{
			httpClient.DefaultRequestHeaders.Add("X-Vault-Token", token);
		}

		httpClient.Timeout = TimeSpan.FromSeconds(30);

		return new VaultSecretClientAdapter(httpClient, mountPath);
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.HashiCorpVaultRetrieving, LogLevel.Debug, "Retrieving credential {Key} from HashiCorp Vault")]
	private partial void LogRetrievingCredential(string key);

	[LoggerMessage(SecurityEventId.HashiCorpVaultSecretNotFound, LogLevel.Warning, "Secret {Key} not found in HashiCorp Vault")]
	private partial void LogSecretNotFound(string key);

	[LoggerMessage(SecurityEventId.HashiCorpVaultRetrieved, LogLevel.Information, "Successfully retrieved credential {Key} from HashiCorp Vault")]
	private partial void LogCredentialRetrieved(string key);

	[LoggerMessage(SecurityEventId.HashiCorpVaultRetrieveFailed, LogLevel.Error, "Failed to retrieve credential {Key} from HashiCorp Vault")]
	private partial void LogRetrieveFailed(Exception ex, string key);

	[LoggerMessage(SecurityEventId.HashiCorpVaultStoring, LogLevel.Debug, "Storing credential {Key} in HashiCorp Vault")]
	private partial void LogStoringCredential(string key);

	[LoggerMessage(SecurityEventId.HashiCorpVaultStored, LogLevel.Information,
		"Successfully stored credential {Key} in HashiCorp Vault")]
	private partial void LogCredentialStored(string key);

	[LoggerMessage(SecurityEventId.HashiCorpVaultStoreFailed, LogLevel.Error, "Failed to store credential {Key} in HashiCorp Vault")]
	private partial void LogStoreFailed(Exception ex, string key);
}
