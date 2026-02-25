// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Azure Key Vault Security Sample
// ===============================
// This sample demonstrates how to use Azure Key Vault for secret management
// with Dispatch messaging, including credential retrieval, caching, and rotation.
//
// Prerequisites:
// 1. Azure subscription with Key Vault created
// 2. Azure CLI installed: https://docs.microsoft.com/cli/azure/install-azure-cli
// 3. Authenticate: az login
//
// For local development, use Azure CLI authentication (DefaultAzureCredential).
// For production, use Managed Identity.

using AzureKeyVaultSample.Services;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("Azure Key Vault Security Sample");
Console.WriteLine("================================");
Console.WriteLine();

var host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration((context, config) =>
	{
		_ = config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
		_ = config.AddEnvironmentVariables();
	})
	.ConfigureServices((context, services) =>
	{
		// Configure logging
		_ = services.AddLogging(builder =>
		{
			_ = builder.SetMinimumLevel(LogLevel.Information);
			_ = builder.AddConsole();
		});

		// Configure Dispatch messaging
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
			_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
		});

		// Add Azure Key Vault credential store
		// This registers ICredentialStore and IWritableCredentialStore
		_ = services.AddAzureKeyVaultCredentialStore(context.Configuration);

		// Register sample services
		_ = services.AddSingleton<SecretDemoService>();
		_ = services.AddSingleton<SecretCachingDemo>();
		_ = services.AddSingleton<SecretRotationDemo>();
	})
	.Build();

// Run demonstrations
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

// Check if Key Vault is configured
var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
var vaultUri = configuration["AzureKeyVault:VaultUri"];

if (string.IsNullOrEmpty(vaultUri))
{
	logger.LogWarning("Azure Key Vault not configured. Running in demo mode.");
	Console.WriteLine();
	Console.WriteLine("To configure Azure Key Vault:");
	Console.WriteLine("1. Create a Key Vault in Azure Portal");
	Console.WriteLine("2. Set 'AzureKeyVault:VaultUri' in appsettings.json or environment");
	Console.WriteLine("3. Authenticate with: az login");
	Console.WriteLine();
	Console.WriteLine("Example appsettings.json:");
	Console.WriteLine(@"{
  ""AzureKeyVault"": {
    ""VaultUri"": ""https://your-vault.vault.azure.net/"",
    ""KeyPrefix"": ""dispatch-""
  }
}");
	Console.WriteLine();

	// Run demo without actual Key Vault
	await RunDemoModeAsync().ConfigureAwait(false);
}
else
{
	logger.LogInformation("Azure Key Vault configured: {VaultUri}", vaultUri);

	// Run actual Key Vault demonstrations
	var secretDemo = scope.ServiceProvider.GetRequiredService<SecretDemoService>();
	await secretDemo.RunAsync().ConfigureAwait(false);

	var cachingDemo = scope.ServiceProvider.GetRequiredService<SecretCachingDemo>();
	await cachingDemo.RunAsync().ConfigureAwait(false);

	var rotationDemo = scope.ServiceProvider.GetRequiredService<SecretRotationDemo>();
	await rotationDemo.RunAsync().ConfigureAwait(false);
}

Console.WriteLine();
Console.WriteLine("Demo complete. Press any key to exit.");
Console.ReadKey();

static async Task RunDemoModeAsync()
{
	Console.WriteLine("=== Demo Mode (No Key Vault) ===");
	Console.WriteLine();

	// Demonstrate the patterns without actual Key Vault
	Console.WriteLine("1. Secret Retrieval Pattern:");
	Console.WriteLine("   var secret = await credentialStore.GetCredentialAsync(\"api-key\", ct);");
	Console.WriteLine("   // Returns SecureString for memory safety");
	Console.WriteLine();

	Console.WriteLine("2. Secret Storage Pattern:");
	Console.WriteLine("   await credentialStore.StoreCredentialAsync(\"api-key\", secureValue, ct);");
	Console.WriteLine("   // Auto-expires after 90 days for rotation");
	Console.WriteLine();

	Console.WriteLine("3. DefaultAzureCredential Authentication:");
	Console.WriteLine("   - Azure CLI (az login) for local development");
	Console.WriteLine("   - Managed Identity in production");
	Console.WriteLine("   - Visual Studio / VS Code authentication");
	Console.WriteLine("   - Environment variables (AZURE_CLIENT_ID, etc.)");
	Console.WriteLine();

	Console.WriteLine("4. Required Azure RBAC Permissions:");
	Console.WriteLine("   - Key Vault Secrets User (read secrets)");
	Console.WriteLine("   - Key Vault Secrets Officer (read/write secrets)");
	Console.WriteLine();

	await Task.CompletedTask.ConfigureAwait(false);
}
