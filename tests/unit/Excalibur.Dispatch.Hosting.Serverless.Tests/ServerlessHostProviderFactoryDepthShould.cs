// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Depth tests for <see cref="ServerlessHostProviderFactory"/> covering
/// environment-variable-based platform detection, fallback behavior, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Collection("EnvironmentVariableTests")]
public sealed class ServerlessHostProviderFactoryDepthShould : UnitTestBase
{
	#region DetectPlatform — AWS Lambda Environment Variables

	[Fact]
	public void DetectPlatform_WithAwsLambdaFunctionName_ReturnsAwsLambda()
	{
		// Arrange
		var originalValue = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "my-function");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AwsLambda);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalValue);
		}
	}

	[Fact]
	public void DetectPlatform_WithAwsExecutionEnv_ReturnsAwsLambda()
	{
		// Arrange
		var originalFuncName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		var originalExecEnv = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "dotnet8");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AwsLambda);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalFuncName);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", originalExecEnv);
		}
	}

	#endregion

	#region DetectPlatform — Azure Functions Environment Variables

	[Fact]
	public void DetectPlatform_WithAzureFunctionsEnvironment_ReturnsAzureFunctions()
	{
		// Arrange
		var originalAwsFunc = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		var originalAwsExec = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
		var originalAzureFunc = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Production");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AzureFunctions);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalAwsFunc);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", originalAwsExec);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", originalAzureFunc);
		}
	}

	[Fact]
	public void DetectPlatform_WithWebsiteSiteName_ReturnsAzureFunctions()
	{
		// Arrange
		var originalAwsFunc = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		var originalAwsExec = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
		var originalAzureFunc = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
		var originalSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "my-azure-function");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AzureFunctions);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalAwsFunc);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", originalAwsExec);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", originalAzureFunc);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", originalSiteName);
		}
	}

	#endregion

	#region DetectPlatform — Google Cloud Functions Environment Variables

	[Fact]
	public void DetectPlatform_WithGoogleFunctionName_ReturnsGoogleCloudFunctions()
	{
		// Arrange
		var originalAwsFunc = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		var originalAwsExec = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
		var originalAzureFunc = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
		var originalSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
		var originalGoogleFunc = Environment.GetEnvironmentVariable("FUNCTION_NAME");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
			Environment.SetEnvironmentVariable("FUNCTION_NAME", "my-google-function");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalAwsFunc);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", originalAwsExec);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", originalAzureFunc);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", originalSiteName);
			Environment.SetEnvironmentVariable("FUNCTION_NAME", originalGoogleFunc);
		}
	}

	[Fact]
	public void DetectPlatform_WithKServiceEnvVar_ReturnsGoogleCloudFunctions()
	{
		// Arrange
		var originalAwsFunc = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		var originalAwsExec = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
		var originalAzureFunc = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
		var originalSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
		var originalGoogleFunc = Environment.GetEnvironmentVariable("FUNCTION_NAME");
		var originalKService = Environment.GetEnvironmentVariable("K_SERVICE");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
			Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("K_SERVICE", "my-cloud-run-service");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalAwsFunc);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", originalAwsExec);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", originalAzureFunc);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", originalSiteName);
			Environment.SetEnvironmentVariable("FUNCTION_NAME", originalGoogleFunc);
			Environment.SetEnvironmentVariable("K_SERVICE", originalKService);
		}
	}

	#endregion

	#region DetectPlatform — Priority Order

	[Fact]
	public void DetectPlatform_WithBothAwsAndAzureEnvVars_PrefersAws()
	{
		// Arrange — AWS is checked first, so it should win
		var originalAwsFunc = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
		var originalAzureFunc = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
		try
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "my-lambda");
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Production");
			var factory = CreateFactory();

			// Act
			var result = factory.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AwsLambda);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", originalAwsFunc);
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", originalAzureFunc);
		}
	}

	#endregion

	#region CreateProvider — DetectPlatform Fallback

	[Fact]
	public void CreateProvider_WithNullPreference_UsesDetectPlatform()
	{
		// Arrange
		var aws = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[aws]);

		// Act — null preference should trigger DetectPlatform() fallback
		// Since we're not in a cloud env, it detects Unknown, falls back to any available
		var result = factory.CreateProvider(null);

		// Assert
		result.ShouldBe(aws);
	}

	#endregion

	#region CreateProvider — Preferred Not Registered But Others Available

	[Fact]
	public void CreateProvider_WithPreferredNotRegistered_FallsBackToAnyAvailable()
	{
		// Arrange
		var google = CreateFakeProvider(ServerlessPlatform.GoogleCloudFunctions, isAvailable: true);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[google]);

		// Act — request Azure, but only Google is registered and available
		var result = factory.CreateProvider(ServerlessPlatform.AzureFunctions);

		// Assert
		result.ShouldBe(google);
	}

	#endregion

	#region AvailableProviders — Empty When All Unavailable

	[Fact]
	public void AvailableProviders_ReturnsEmpty_WhenAllUnavailable()
	{
		// Arrange
		var aws = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: false);
		var azure = CreateFakeProvider(ServerlessPlatform.AzureFunctions, isAvailable: false);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[aws, azure]);

		// Assert
		factory.AvailableProviders.ShouldBeEmpty();
	}

	#endregion

	#region AvailableProviders — Multiple Available

	[Fact]
	public void AvailableProviders_ReturnsAllAvailable()
	{
		// Arrange
		var aws = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var azure = CreateFakeProvider(ServerlessPlatform.AzureFunctions, isAvailable: true);
		var google = CreateFakeProvider(ServerlessPlatform.GoogleCloudFunctions, isAvailable: false);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[aws, azure, google]);

		// Assert
		factory.AvailableProviders.Count().ShouldBe(2);
		factory.AvailableProviders.ShouldContain(aws);
		factory.AvailableProviders.ShouldContain(azure);
		factory.AvailableProviders.ShouldNotContain(google);
	}

	#endregion

	#region Helpers

	private static ServerlessHostProviderFactory CreateFactory()
	{
		return new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());
	}

	private static IServerlessHostProvider CreateFakeProvider(
		ServerlessPlatform platform,
		bool isAvailable)
	{
		var provider = A.Fake<IServerlessHostProvider>();
		A.CallTo(() => provider.Platform).Returns(platform);
		A.CallTo(() => provider.IsAvailable).Returns(isAvailable);
		return provider;
	}

	#endregion
}
