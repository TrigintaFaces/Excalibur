// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Security;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace AwsSecretsManagerSample.Services;

/// <summary>
/// Demonstrates basic secret retrieval and storage with AWS Secrets Manager.
/// </summary>
public sealed class AwsSecretDemoService
{
	private readonly ICredentialStore _credentialStore;
	private readonly IWritableCredentialStore _writableStore;
	private readonly ILogger<AwsSecretDemoService> _logger;

	public AwsSecretDemoService(
		ICredentialStore credentialStore,
		IWritableCredentialStore writableStore,
		ILogger<AwsSecretDemoService> logger)
	{
		_credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
		_writableStore = writableStore ?? throw new ArgumentNullException(nameof(writableStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		Console.WriteLine();
		Console.WriteLine("=== AWS Secrets Manager Demo ===");
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
		}
		else
		{
			_logger.LogWarning("Secret not found (this is expected for demo)");
		}

		// Demo 2: Store a new secret
		Console.WriteLine();
		_logger.LogInformation("Storing a demo API key...");

		var apiKey = CreateSecureString("demo-aws-api-key-12345");
		try
		{
			await _writableStore.StoreCredentialAsync(
				"demo-api-key",
				apiKey,
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("API key stored successfully");
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

		// Show best practices
		Console.WriteLine();
		Console.WriteLine("AWS Secrets Manager Best Practices:");
		Console.WriteLine("  - Use IAM roles instead of access keys when possible");
		Console.WriteLine("  - Enable automatic rotation for supported secrets");
		Console.WriteLine("  - Use resource-based policies for cross-account access");
		Console.WriteLine("  - Tag secrets for organization and cost allocation");
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
