// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Security;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace AzureKeyVaultSample.Services;

/// <summary>
/// Demonstrates secret rotation patterns with Azure Key Vault.
/// </summary>
/// <remarks>
/// Secret rotation is critical for security. Azure Key Vault supports:
/// - Automatic rotation with Event Grid notifications
/// - Manual rotation via API
/// - Version history for rollback
/// </remarks>
public sealed class SecretRotationDemo
{
	private readonly IWritableCredentialStore _writableStore;
	private readonly ICredentialStore _credentialStore;
	private readonly ILogger<SecretRotationDemo> _logger;

	public SecretRotationDemo(
		IWritableCredentialStore writableStore,
		ICredentialStore credentialStore,
		ILogger<SecretRotationDemo> logger)
	{
		_writableStore = writableStore ?? throw new ArgumentNullException(nameof(writableStore));
		_credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		Console.WriteLine();
		Console.WriteLine("=== Secret Rotation Demo ===");
		Console.WriteLine();

		const string secretKey = "rotatable-api-key";

		// Step 1: Create initial secret
		_logger.LogInformation("Creating initial secret...");
		var initialValue = CreateSecureString($"initial-value-{Guid.NewGuid():N}");
		try
		{
			await _writableStore.StoreCredentialAsync(secretKey, initialValue, cancellationToken).ConfigureAwait(false);
			Console.WriteLine("  Initial secret created");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not store initial secret (demo continues)");
		}
		finally
		{
			initialValue.Dispose();
		}

		// Step 2: Simulate rotation by storing new version
		Console.WriteLine();
		_logger.LogInformation("Rotating secret to new value...");
		var rotatedValue = CreateSecureString($"rotated-value-{Guid.NewGuid():N}");
		try
		{
			await _writableStore.StoreCredentialAsync(secretKey, rotatedValue, cancellationToken).ConfigureAwait(false);
			Console.WriteLine("  Secret rotated successfully");
			Console.WriteLine("  New version created in Key Vault");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not rotate secret (demo continues)");
		}
		finally
		{
			rotatedValue.Dispose();
		}

		// Step 3: Verify rotation
		Console.WriteLine();
		_logger.LogInformation("Verifying rotated secret...");
		var verifiedSecret = await _credentialStore.GetCredentialAsync(secretKey, cancellationToken).ConfigureAwait(false);
		if (verifiedSecret != null)
		{
			Console.WriteLine($"  Retrieved rotated secret: {verifiedSecret.Length} characters");
		}

		// Show rotation best practices
		Console.WriteLine();
		Console.WriteLine("Rotation Best Practices:");
		Console.WriteLine("  1. Use Azure Key Vault automatic rotation when possible");
		Console.WriteLine("  2. Subscribe to Event Grid for rotation notifications");
		Console.WriteLine("  3. Implement graceful handling during rotation window");
		Console.WriteLine("  4. Keep previous versions for rollback (Key Vault does this automatically)");
		Console.WriteLine("  5. Clear local caches after rotation");
		Console.WriteLine();
		Console.WriteLine("Event Grid Integration:");
		Console.WriteLine("  - SecretNewVersionCreated event triggers on rotation");
		Console.WriteLine("  - Use Azure Function or Logic App to handle events");
		Console.WriteLine("  - Notify dependent services to refresh cached secrets");
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
