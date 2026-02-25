// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureFunctionsColdStartOptimizerShould : UnitTestBase
{
	private readonly AzureFunctionsColdStartOptimizer _sut = new(A.Fake<IServiceProvider>(), EnabledTestLogger.Create<AzureFunctionsColdStartOptimizer>());

	[Fact]
	public void IsEnabled_ReturnFalse_WhenNotInAzureFunctionsEnvironment()
	{
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
		_sut.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnTrue_WhenInAzureFunctionsEnvironment()
	{
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
		_sut.IsEnabled.ShouldBeTrue();
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
	}

	[Fact]
	public async Task OptimizeAndWarmup_CompleteWithoutException()
	{
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
		Environment.SetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", "ikey");
		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "orders-func");
		Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "instance-1");
		Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
		Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true");

		try
		{
			await _sut.OptimizeAsync();
			await _sut.WarmupAsync();
		}
		finally
		{
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
			Environment.SetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", null);
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
			Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
			Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);
			Environment.SetEnvironmentVariable("AzureWebJobsStorage", null);
		}
	}
}
