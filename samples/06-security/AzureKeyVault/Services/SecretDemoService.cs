// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Security;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace AzureKeyVaultSample.Services;

/// <summary>
/// Demonstrates basic secret retrieval and storage with Azure Key Vault.
/// </summary>
public sealed class SecretDemoService
{
	private readonly ICredentialStore _credentialStore;
	private readonly IWritableCredentialStore _writableStore;
	private readonly ILogger<SecretDemoService> _logger;

	public SecretDemoService(
		ICredentialStore credentialStore,
		IWritableCredentialStore writableStore,
		ILogger<SecretDemoService> logger)
	{
		_credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
		_writableStore = writableStore ?? throw new ArgumentNullException(nameof(writableStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		Console.WriteLine();
		Console.WriteLine("=== Secret Retrieval Demo ===");
		Console.WriteLine();

		// Demo 1: Retrieve a secret
		_logger.LogInformation("Attempting to retrieve 'database-connection-string'...");

		var dbSecret = await _credentialStore.GetCredentialAsync(
			"database-connection-string",
			cancellationToken).ConfigureAwait(false);

		if (dbSecret != null)
		{
			_logger.LogInformation("Successfully retrieved database connection string");
			Console.WriteLine($"  Secret length: {dbSecret.Length} characters");

			// SecureString keeps the value encrypted in memory
			// Only decrypt when absolutely necessary
		}
		else
		{
			_logger.LogWarning("Secret not found (this is expected for demo)");
		}

		// Demo 2: Store a new secret
		Console.WriteLine();
		_logger.LogInformation("Storing a demo API key...");

		var apiKey = CreateSecureString("demo-api-key-value-12345");
		try
		{
			await _writableStore.StoreCredentialAsync(
				"demo-api-key",
				apiKey,
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("API key stored successfully");
			Console.WriteLine("  Secret stored with 90-day auto-expiration");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to store secret (requires write permissions)");
		}
		finally
		{
			apiKey.Dispose();
		}

		// Demo 3: Retrieve the stored secret
		Console.WriteLine();
		_logger.LogInformation("Retrieving the stored API key...");

		var retrievedKey = await _credentialStore.GetCredentialAsync(
			"demo-api-key",
			cancellationToken).ConfigureAwait(false);

		if (retrievedKey != null)
		{
			_logger.LogInformation("Successfully retrieved stored API key");
			Console.WriteLine($"  Retrieved secret length: {retrievedKey.Length} characters");
		}
	}

	private static SecureString CreateSecureString(string value)
	{
		var secure = new SecureString();
		foreach (var c in value)
		{
			secure.AppendChar(c);
		}

		secure.MakeReadOnly();
		return secure;
	}
}
