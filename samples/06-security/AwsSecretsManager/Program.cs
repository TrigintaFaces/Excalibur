// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// AWS Secrets Manager Security Sample
// ====================================
// This sample demonstrates how to use AWS Secrets Manager for secret management
// with Dispatch messaging, including credential retrieval, caching, and rotation.
//
// Prerequisites:
// 1. AWS account with Secrets Manager access
// 2. AWS CLI installed: https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html
// 3. Configure credentials: aws configure
//
// For local development, use LocalStack:
// docker run -d -p 4566:4566 localstack/localstack

using AwsSecretsManagerSample.Services;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("AWS Secrets Manager Security Sample");
Console.WriteLine("====================================");
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

		// Add AWS Secrets Manager credential store
		// This registers ICredentialStore and IWritableCredentialStore
		_ = services.AddAwsSecretsManagerCredentialStore(context.Configuration);

		// Register sample services
		_ = services.AddSingleton<AwsSecretDemoService>();
		_ = services.AddSingleton<LocalStackDemo>();
	})
	.Build();

// Run demonstrations
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

// Check if AWS is configured
var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
var awsRegion = configuration["AWS:Region"];

if (string.IsNullOrEmpty(awsRegion))
{
	logger.LogWarning("AWS region not configured. Running in demo mode.");
	Console.WriteLine();
	Console.WriteLine("To configure AWS Secrets Manager:");
	Console.WriteLine("1. Set AWS credentials: aws configure");
	Console.WriteLine("2. Set 'AWS:Region' in appsettings.json or environment");
	Console.WriteLine();
	Console.WriteLine("For local development with LocalStack:");
	Console.WriteLine("1. docker run -d -p 4566:4566 localstack/localstack");
	Console.WriteLine("2. Set 'AWS:ServiceURL' to 'http://localhost:4566'");
	Console.WriteLine();
	Console.WriteLine("Example appsettings.json:");
	Console.WriteLine(@"{
  ""AWS"": {
    ""Region"": ""us-east-1"",
    ""ServiceURL"": ""http://localhost:4566""
  }
}");
	Console.WriteLine();

	// Run demo without actual AWS
	await RunDemoModeAsync().ConfigureAwait(false);
}
else
{
	logger.LogInformation("AWS region configured: {Region}", awsRegion);

	// Check for LocalStack configuration
	var serviceUrl = configuration["AWS:ServiceURL"];
	if (!string.IsNullOrEmpty(serviceUrl))
	{
		logger.LogInformation("Using LocalStack at: {ServiceURL}", serviceUrl);
		var localStackDemo = scope.ServiceProvider.GetRequiredService<LocalStackDemo>();
		await localStackDemo.RunAsync().ConfigureAwait(false);
	}
	else
	{
		var secretDemo = scope.ServiceProvider.GetRequiredService<AwsSecretDemoService>();
		await secretDemo.RunAsync().ConfigureAwait(false);
	}
}

Console.WriteLine();
Console.WriteLine("Demo complete. Press any key to exit.");
Console.ReadKey();

static async Task RunDemoModeAsync()
{
	Console.WriteLine("=== Demo Mode (No AWS) ===");
	Console.WriteLine();

	// Demonstrate the patterns without actual AWS
	Console.WriteLine("1. Secret Retrieval Pattern:");
	Console.WriteLine("   var secret = await credentialStore.GetCredentialAsync(\"api-key\", ct);");
	Console.WriteLine("   // Returns SecureString for memory safety");
	Console.WriteLine();

	Console.WriteLine("2. AWS SDK Authentication Chain:");
	Console.WriteLine("   - Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)");
	Console.WriteLine("   - Shared credentials file (~/.aws/credentials)");
	Console.WriteLine("   - IAM Role (EC2 instance profile, ECS task role, Lambda)");
	Console.WriteLine();

	Console.WriteLine("3. Required IAM Permissions:");
	Console.WriteLine("   - secretsmanager:GetSecretValue");
	Console.WriteLine("   - secretsmanager:CreateSecret (for write)");
	Console.WriteLine("   - secretsmanager:PutSecretValue (for rotation)");
	Console.WriteLine();

	Console.WriteLine("4. LocalStack for Local Development:");
	Console.WriteLine("   - docker run -d -p 4566:4566 localstack/localstack");
	Console.WriteLine("   - Set AWS:ServiceURL=http://localhost:4566");
	Console.WriteLine("   - No credentials required for LocalStack");
	Console.WriteLine();

	await Task.CompletedTask.ConfigureAwait(false);
}
