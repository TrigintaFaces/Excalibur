// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Security;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace AwsSecretsManagerSample.Services;

/// <summary>
/// Demonstrates using LocalStack for local AWS development.
/// </summary>
/// <remarks>
/// LocalStack provides a fully functional local AWS cloud stack for development
/// and testing without incurring AWS charges.
///
/// Start LocalStack: docker run -d -p 4566:4566 localstack/localstack
/// </remarks>
public sealed class LocalStackDemo
{
	private readonly ICredentialStore _credentialStore;
	private readonly IWritableCredentialStore _writableStore;
	private readonly ILogger<LocalStackDemo> _logger;

	public LocalStackDemo(
		ICredentialStore credentialStore,
		IWritableCredentialStore writableStore,
		ILogger<LocalStackDemo> logger)
	{
		_credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
		_writableStore = writableStore ?? throw new ArgumentNullException(nameof(writableStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		Console.WriteLine();
		Console.WriteLine("=== LocalStack Demo ===");
		Console.WriteLine();

		_logger.LogInformation("Running with LocalStack...");

		// Create a test secret
		Console.WriteLine("Creating test secret in LocalStack...");
		var testSecret = CreateSecureString("localstack-test-secret-value");

		try
		{
			await _writableStore.StoreCredentialAsync(
				"localstack-test-secret",
				testSecret,
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Test secret created successfully");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not create secret (LocalStack may not be running)");
			Console.WriteLine();
			Console.WriteLine("To start LocalStack:");
			Console.WriteLine("  docker run -d -p 4566:4566 localstack/localstack");
			return;
		}
		finally
		{
			testSecret.Dispose();
		}

		// Retrieve the secret
		Console.WriteLine();
		_logger.LogInformation("Retrieving test secret from LocalStack...");

		var retrieved = await _credentialStore.GetCredentialAsync(
			"localstack-test-secret",
			cancellationToken).ConfigureAwait(false);

		if (retrieved != null)
		{
			_logger.LogInformation("Successfully retrieved secret from LocalStack");
			Console.WriteLine($"  Secret length: {retrieved.Length} characters");
		}

		// Show LocalStack tips
		Console.WriteLine();
		Console.WriteLine("LocalStack Tips:");
		Console.WriteLine("  - Secrets persist until container restart");
		Console.WriteLine("  - Use docker-compose for consistent local environment");
		Console.WriteLine("  - LocalStack Pro supports more AWS services");
		Console.WriteLine("  - Configure CI/CD to use LocalStack for integration tests");
		Console.WriteLine();
		Console.WriteLine("docker-compose.yml example:");
		Console.WriteLine(@"  services:
    localstack:
      image: localstack/localstack
      ports:
        - ""4566:4566""
      environment:
        - SERVICES=secretsmanager
        - DEBUG=1");
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
