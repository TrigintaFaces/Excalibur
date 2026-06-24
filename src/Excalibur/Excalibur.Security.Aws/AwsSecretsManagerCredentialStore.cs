// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;

using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Security.Aws;

/// <summary>
/// AWS Secrets Manager credential store. Reads and writes secrets through the real AWS
/// Secrets Manager SDK (via an injectable <see cref="IAmazonSecretsManager"/> seam), so a
/// <see cref="StoreCredentialAsync"/> followed by <see cref="GetCredentialAsync"/> round-trips
/// against the service — secrets are never sourced from, or silently discarded to, plain
/// configuration.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class AwsSecretsManagerCredentialStore : IWritableCredentialStore, IDisposable
{
	private readonly ILogger<AwsSecretsManagerCredentialStore> _logger;
	private readonly IAmazonSecretsManager _client;
	private readonly bool _ownsClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSecretsManagerCredentialStore"/> class
	/// that talks to the real AWS Secrets Manager service. The client is built from the
	/// configured region (<c>AWS:SecretsManager:Region</c> or <c>AWS:Region</c>), falling back
	/// to the default AWS region-resolution chain when unset.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="configuration"> The configuration supplying the AWS region. </param>
	public AwsSecretsManagerCredentialStore(
		ILogger<AwsSecretsManagerCredentialStore> logger,
		IConfiguration configuration)
		: this(logger, BuildClient(configuration), ownsClient: true)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSecretsManagerCredentialStore"/> class
	/// with an explicit <see cref="IAmazonSecretsManager"/>. Used by tests via
	/// <c>InternalsVisibleTo</c> to drive a fake client, and by the DI registration to supply a
	/// region-configured client; not part of the public contract. The supplied client is not
	/// disposed by this store (the caller owns its lifetime).
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <param name="client"> The AWS Secrets Manager client seam. </param>
	internal AwsSecretsManagerCredentialStore(
		ILogger<AwsSecretsManagerCredentialStore> logger,
		IConfiguration configuration,
		IAmazonSecretsManager client)
		: this(logger, client, ownsClient: false)
	{
		ArgumentNullException.ThrowIfNull(configuration);
	}

	private AwsSecretsManagerCredentialStore(
		ILogger<AwsSecretsManagerCredentialStore> logger,
		IAmazonSecretsManager client,
		bool ownsClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_ownsClient = ownsClient;
	}

	/// <summary>
	/// Retrieves a credential from AWS Secrets Manager.
	/// </summary>
	/// <param name="key"> The secret name/key to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The retrieved secure credential or null if not found. </returns>
	/// <exception cref="ArgumentException">Thrown when key is null, empty, or whitespace.</exception>
	/// <exception cref="InvalidOperationException">Thrown when secret retrieval from AWS Secrets Manager fails.</exception>
	public async Task<SecureString?> GetCredentialAsync(string key, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Key cannot be null or empty", nameof(key));
		}

		LogRetrievingCredential(key);

		try
		{
			var response = await _client.GetSecretValueAsync(
				new GetSecretValueRequest { SecretId = key }, cancellationToken).ConfigureAwait(false);

			var secretValue = response.SecretString;
			if (string.IsNullOrEmpty(secretValue))
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
		catch (ResourceNotFoundException)
		{
			// A missing secret is a normal, non-error outcome — distinct from a backend failure.
			LogSecretNotFound(key);
			return null;
		}
		catch (Exception ex) when (ex is not ArgumentException)
		{
			LogRetrievalFailed(ex, key);
			throw new InvalidOperationException($"Failed to retrieve secret '{key}' from AWS Secrets Manager", ex);
		}
	}

	/// <summary>
	/// Stores a credential in AWS Secrets Manager, creating the secret if it does not yet exist.
	/// </summary>
	/// <param name="key"> The secret name/key to store. </param>
	/// <param name="credential"> The credential to store securely. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <exception cref="ArgumentException">Thrown when key is null, empty, or whitespace, or when credential length is invalid (must be between 1 and 65536 characters).</exception>
	/// <exception cref="ArgumentNullException">Thrown when credential is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when storing the secret in AWS Secrets Manager fails.</exception>
	/// <returns>A task that represents the asynchronous store operation.</returns>
	public async Task StoreCredentialAsync(string key, SecureString credential,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Key cannot be null or empty", nameof(key));
		}

		ArgumentNullException.ThrowIfNull(credential);

		LogStoringCredential(key);

		// Convert SecureString to string for the AWS API
		var credentialValue = SecureStringToString(credential);

		try
		{
			if (credentialValue.Length is < 1 or > 65536)
			{
				throw new ArgumentException("Credential length must be between 1 and 65536 characters", nameof(credential));
			}

			try
			{
				_ = await _client.PutSecretValueAsync(
					new PutSecretValueRequest { SecretId = key, SecretString = credentialValue },
					cancellationToken).ConfigureAwait(false);
			}
			catch (ResourceNotFoundException)
			{
				// The secret does not exist yet — create it (idempotent store-or-update).
				_ = await _client.CreateSecretAsync(
					new CreateSecretRequest { Name = key, SecretString = credentialValue },
					cancellationToken).ConfigureAwait(false);
			}

			// Only logged once the write is durably accepted (never logged-as-success on failure).
			LogCredentialStored(key);
		}
		catch (ArgumentException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogStorageFailed(ex, key);
			throw new InvalidOperationException($"Failed to store secret '{key}' in AWS Secrets Manager", ex);
		}
		finally
		{
			// Clear the transient plaintext copy from memory.
			Array.Fill(credentialValue.ToCharArray(), '\0');
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_ownsClient)
		{
			_client.Dispose();
		}
	}

	private static IAmazonSecretsManager BuildClient(IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var region = configuration["AWS:SecretsManager:Region"] ?? configuration["AWS:Region"];
		return string.IsNullOrWhiteSpace(region)
			? new AmazonSecretsManagerClient()
			: new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
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

	// Source-generated logging methods
	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerRetrieving, LogLevel.Debug, "Retrieving credential {Key} from AWS Secrets Manager")]
	private partial void LogRetrievingCredential(string key);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerSecretNotFound, LogLevel.Warning, "Secret {Key} not found in AWS Secrets Manager")]
	private partial void LogSecretNotFound(string key);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerRetrieved, LogLevel.Information,
		"Successfully retrieved credential {Key} from AWS Secrets Manager")]
	private partial void LogCredentialRetrieved(string key);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerRetrieveFailed, LogLevel.Error, "Failed to retrieve credential {Key} from AWS Secrets Manager")]
	private partial void LogRetrievalFailed(Exception ex, string key);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerStoring, LogLevel.Debug, "Storing credential {Key} in AWS Secrets Manager")]
	private partial void LogStoringCredential(string key);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerStored, LogLevel.Information, "Successfully stored credential {Key} in AWS Secrets Manager")]
	private partial void LogCredentialStored(string key);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerRequestFailed, LogLevel.Error, "Failed to store credential {Key} in AWS Secrets Manager")]
	private partial void LogStorageFailed(Exception ex, string key);
}
